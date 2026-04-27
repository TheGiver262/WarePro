using System.Linq;

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
        if (command.Status != StockDocumentStatus.Approved)
        {
            throw new InventoryDomainException("Only approved stock-in documents can be posted.");
        }

        if (command.Quantity <= 0)
        {
            throw new InventoryDomainException("Stock-in quantity must be greater than zero.");
        }

        var warehouseId = _warehouseProvider.GetDefaultWarehouseId();
        var product = _unitOfWork.GetProduct(command.ProductId);
        var serialNumbers = command.SerialNumbers
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToArray();

        EnsureNoDuplicateSerials(serialNumbers);

        if (product.IsSerialManaged && serialNumbers.Length != command.Quantity)
        {
            throw new InventoryDomainException("Serial count must match stock-in quantity.");
        }

        if (!product.IsSerialManaged && serialNumbers.Length > 0)
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

        _unitOfWork.MarkDocumentPosted(command.DocumentId);
        _unitOfWork.Commit();
    }

    public void PostStockOut(PostStockOutCommand command)
    {
        if (command.Kind != StockOutKind.Sale)
        {
            throw new InventoryDomainException("Only sale stock-out can be posted by this service.");
        }

        if (command.Status != StockDocumentStatus.Approved)
        {
            throw new InventoryDomainException("Only approved stock-out documents can be posted.");
        }

        if (command.Quantity <= 0)
        {
            throw new InventoryDomainException("Stock-out quantity must be greater than zero.");
        }

        var warehouseId = _warehouseProvider.GetDefaultWarehouseId();
        var product = _unitOfWork.GetProduct(command.ProductId);
        var serialNumbers = command.SerialNumbers
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToArray();

        EnsureNoDuplicateSerials(serialNumbers);

        if (product.IsSerialManaged && serialNumbers.Length != command.Quantity)
        {
            throw new InventoryDomainException("Serial count must match stock-out quantity.");
        }

        if (!product.IsSerialManaged && serialNumbers.Length > 0)
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
                throw new InventoryDomainException($"Serial {serialNumber} is not in the default warehouse.");
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

        _unitOfWork.MarkDocumentPosted(command.DocumentId);
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
