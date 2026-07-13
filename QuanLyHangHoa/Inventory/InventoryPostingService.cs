using System.Linq;
using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Inventory;

public sealed class InventoryPostingService
{
    private readonly IInventoryUnitOfWork _unitOfWork;
    private readonly IDefaultWarehouseProvider _warehouseProvider;
    private readonly IClock _clock;

    public InventoryPostingService(
        IInventoryUnitOfWork unitOfWork,
        IDefaultWarehouseProvider warehouseProvider,
        IClock clock)
    {
        _unitOfWork = unitOfWork;
        _warehouseProvider = warehouseProvider;
        _clock = clock;
    }

    public void PostStockIn(PostStockInCommand command)
    {
        // Allow Approved or Posted for transitionary states
        if (command.Status != StockDocumentStatus.Approved && command.Status != StockDocumentStatus.Posted)
        {
            throw new InventoryDomainException("Only approved or ready-to-post stock-in documents can be posted.");
        }

        if (command.Quantity <= 0)
        {
            throw new InventoryDomainException("Stock-in quantity must be greater than zero.");
        }

        var warehouseId = command.WarehouseId;
        var product = _unitOfWork.GetProduct(command.ProductId);
        var serialNumbers = command.SerialNumbers
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToArray();

        EnsureNoDuplicateSerials(serialNumbers);

        if (product.IsSerialTracked && serialNumbers.Length != command.Quantity)
        {
            throw new InventoryDomainException("Serial count must match stock-in quantity.");
        }

        if (!product.IsSerialTracked && serialNumbers.Length > 0)
        {
            throw new InventoryDomainException("Non-serial products cannot receive serial numbers.");
        }

        foreach (var serialNumber in serialNumbers)
        {
            if (_unitOfWork.SerialExists(serialNumber))
            {
                throw new InventoryDomainException($"Serial {serialNumber} already exists.");
            }
        }

        var balance = _unitOfWork.GetOrCreateBalance(command.ProductId, warehouseId);
        _unitOfWork.SaveBalance(balance with
        {
            OnHandQuantity = balance.OnHandQuantity + command.Quantity,
            AvailableQuantity = balance.AvailableQuantity + command.Quantity
        });

        foreach (var serialNumber in serialNumbers)
        {
            _unitOfWork.SaveSerial(new ProductSerialSnapshot(
                serialNumber,
                command.ProductId,
                warehouseId,
                SerialStatus.InStock));
        }

        _unitOfWork.AddLedger(new StockLedgerEntry(
            command.DocumentId,
            "StockIn",
            command.ProductId,
            warehouseId,
            StockLedgerDirection.In,
            command.Quantity,
            _clock.Now,
            command.PostedByUserId));

        _unitOfWork.AddAudit(new AuditLogEntry(
            command.DocumentId,
            AuditActionCode.PostStockIn,
            _clock.Now,
            command.PostedByUserId));

        // Note: Marking document posted should ideally be handled by the caller or a higher-level orchestration
        // but we keep it here for now as part of the atomic posting action.
        _unitOfWork.MarkDocumentPosted(command.DocumentId, "StockIn");
        _unitOfWork.Commit();
    }

    public void PostStockOut(PostStockOutCommand command)
    {
        if (command.Kind != StockOutKind.Sale && command.Kind != StockOutKind.WarrantyReplacement)
        {
            throw new InventoryDomainException("Only sale or warranty-replacement stock-out can be posted by this service.");
        }

        if (command.Status != StockDocumentStatus.Approved && command.Status != StockDocumentStatus.Posted)
        {
            throw new InventoryDomainException("Only approved or ready-to-post stock-out documents can be posted.");
        }

        if (command.Quantity <= 0)
        {
            throw new InventoryDomainException("Stock-out quantity must be greater than zero.");
        }

        var warehouseId = command.WarehouseId;
        var product = _unitOfWork.GetProduct(command.ProductId);
        var serialNumbers = command.SerialNumbers
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToArray();

        EnsureNoDuplicateSerials(serialNumbers);

        if (product.IsSerialTracked && serialNumbers.Length != command.Quantity)
        {
            throw new InventoryDomainException("Serial count must match stock-out quantity.");
        }

        if (!product.IsSerialTracked && serialNumbers.Length > 0)
        {
            throw new InventoryDomainException("Non-serial products cannot be issued with serial numbers.");
        }

        var balance = _unitOfWork.FindBalance(command.ProductId, warehouseId);
        if (balance is null || balance.AvailableQuantity < command.Quantity)
        {
            throw new InventoryDomainException("Insufficient available stock.");
        }

        foreach (var serialNumber in serialNumbers)
        {
            var serial = _unitOfWork.GetSerial(serialNumber);
            if (serial.ProductId != command.ProductId)
            {
                throw new InventoryDomainException($"Serial {serialNumber} does not belong to product {command.ProductId}.");
            }

            if (serial.CurrentWarehouseId != warehouseId)
            {
                throw new InventoryDomainException($"Serial {serialNumber} is not in the specified warehouse.");
            }

            if (serial.Status != SerialStatus.InStock)
            {
                throw new InventoryDomainException($"Serial {serialNumber} is not available.");
            }
        }

        _unitOfWork.SaveBalance(balance with
        {
            OnHandQuantity = balance.OnHandQuantity - command.Quantity,
            AvailableQuantity = balance.AvailableQuantity - command.Quantity
        });

        foreach (var serialNumber in serialNumbers)
        {
            var serial = _unitOfWork.GetSerial(serialNumber);
            _unitOfWork.SaveSerial(serial with
            {
                Status = SerialStatus.Sold,
                CurrentWarehouseId = null
            });
        }

        _unitOfWork.AddLedger(new StockLedgerEntry(
            command.DocumentId,
            "StockOut",
            command.ProductId,
            warehouseId,
            StockLedgerDirection.Out,
            command.Quantity,
            _clock.Now,
            command.PostedByUserId));

        _unitOfWork.AddAudit(new AuditLogEntry(
            command.DocumentId,
            AuditActionCode.PostStockOut,
            _clock.Now,
            command.PostedByUserId));

        _unitOfWork.MarkDocumentPosted(command.DocumentId, "StockOut");
        _unitOfWork.Commit();
    }

    public void PostStockTransfer(PostStockTransferCommand command)
    {
        if (command.Status != StockDocumentStatus.Approved && command.Status != StockDocumentStatus.Posted)
        {
            throw new InventoryDomainException("Only approved or ready-to-post stock-transfer documents can be posted.");
        }

        if (command.Quantity <= 0)
        {
            throw new InventoryDomainException("Transfer quantity must be greater than zero.");
        }

        if (command.FromWarehouseId == command.ToWarehouseId)
        {
            throw new InventoryDomainException("Source and destination warehouses must be different.");
        }

        var product = _unitOfWork.GetProduct(command.ProductId);
        var serialNumbers = command.SerialNumbers
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToArray();

        EnsureNoDuplicateSerials(serialNumbers);

        if (product.IsSerialTracked && serialNumbers.Length != command.Quantity)
        {
            throw new InventoryDomainException("Serial count must match transfer quantity.");
        }

        // 1. Process From Warehouse (Stock Out)
        var fromBalance = _unitOfWork.FindBalance(command.ProductId, command.FromWarehouseId);
        if (fromBalance is null || fromBalance.AvailableQuantity < command.Quantity)
        {
            throw new InventoryDomainException("Insufficient available stock in source warehouse.");
        }

        _unitOfWork.SaveBalance(fromBalance with
        {
            OnHandQuantity = fromBalance.OnHandQuantity - command.Quantity,
            AvailableQuantity = fromBalance.AvailableQuantity - command.Quantity
        });

        // 2. Process To Warehouse (Stock In)
        var toBalance = _unitOfWork.GetOrCreateBalance(command.ProductId, command.ToWarehouseId);
        _unitOfWork.SaveBalance(toBalance with
        {
            OnHandQuantity = toBalance.OnHandQuantity + command.Quantity,
            AvailableQuantity = toBalance.AvailableQuantity + command.Quantity
        });

        // 3. Update Serials
        foreach (var serialNumber in serialNumbers)
        {
            var serial = _unitOfWork.GetSerial(serialNumber);
            if (serial.ProductId != command.ProductId)
            {
                throw new InventoryDomainException($"Serial {serialNumber} does not belong to product {command.ProductId}.");
            }

            if (serial.CurrentWarehouseId != command.FromWarehouseId)
            {
                throw new InventoryDomainException($"Serial {serialNumber} is not in the source warehouse.");
            }

            if (serial.Status != SerialStatus.InStock)
            {
                throw new InventoryDomainException($"Serial {serialNumber} is not available for transfer.");
            }

            _unitOfWork.SaveSerial(serial with
            {
                CurrentWarehouseId = command.ToWarehouseId
            });
        }

        // 4. Ledger Entries
        _unitOfWork.AddLedger(new StockLedgerEntry(
            command.DocumentId,
            "StockTransfer",
            command.ProductId,
            command.FromWarehouseId,
            StockLedgerDirection.Out,
            command.Quantity,
            _clock.Now,
            command.PostedByUserId));

        _unitOfWork.AddLedger(new StockLedgerEntry(
            command.DocumentId,
            "StockTransfer",
            command.ProductId,
            command.ToWarehouseId,
            StockLedgerDirection.In,
            command.Quantity,
            _clock.Now,
            command.PostedByUserId));

        _unitOfWork.AddAudit(new AuditLogEntry(
            command.DocumentId,
            AuditActionCode.PostStockTransfer,
            _clock.Now,
            command.PostedByUserId));

        _unitOfWork.MarkDocumentPosted(command.DocumentId, "StockTransfer");
        _unitOfWork.Commit();
    }

    private static void EnsureNoDuplicateSerials(string[] serialNumbers)
    {
        if (serialNumbers.Length != serialNumbers.Distinct(System.StringComparer.OrdinalIgnoreCase).Count())
        {
            throw new InventoryDomainException("Duplicate serials are not allowed.");
        }
    }
}
