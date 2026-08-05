using System.Linq;
using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Inventory;

/// <summary>
/// kiểm tra quyền và invariant tồn kho trước khi ghi balance, serial, ledger, audit và trạng thái trong một commit.
/// </summary>
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

    // số lượng của command đã ở đơn vị tồn kho; serial được chuẩn hóa trước mọi thay đổi dữ liệu.
    public void PostStockIn(PostStockInCommand command)
    {
        EnsureApprovedAndAuthorized(command.Status, command.PostedByUserId, "stock-in",
            allowWarrantyPermission: command.Kind == StockInKind.WarrantyReceive);

        if (command.Quantity <= 0)
        {
            throw new InventoryDomainException("Stock-in quantity must be greater than zero.");
        }

        var warehouseId = command.WarehouseId;
        var product = _unitOfWork.GetProduct(command.ProductId);
        var serialNumbers = QuanLyHangHoa.Helpers.SerialNumberNormalizer.NormalizeAll(command.SerialNumbers).ToArray();

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

        // entity balance vẫn được EF track với rowversion gốc để phát hiện client khác vừa đổi tồn.
        // balance là số hiện tại; ledger phía dưới giữ lịch sử phát sinh dẫn tới số mới.
        var balance = _unitOfWork.GetOrCreateBalance(command.ProductId, warehouseId);
        _unitOfWork.SaveBalance(balance with
        {
            OnHandQuantity = balance.OnHandQuantity + command.Quantity,
            AvailableQuantity = balance.AvailableQuantity + command.Quantity
        });

        // ProductSerial là nguồn chuẩn vị trí từng thiết bị; balance chỉ giữ tổng lượng theo kho.
        foreach (var serialNumber in serialNumbers)
        {
            _unitOfWork.SaveSerial(new ProductSerialSnapshot(
                serialNumber,
                command.ProductId,
                warehouseId,
                SerialStatus.InStock,
                StockInLineId: command.StockInLineId));
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

        // trạng thái Posted được lưu cùng balance, serial, ledger và audit để không có chứng từ nửa ghi sổ.
        _unitOfWork.MarkDocumentPosted(command.DocumentId, "StockIn");
        _unitOfWork.Commit();
    }

    // chỉ các loại xuất kho đã định nghĩa mới được dùng chung luồng trừ tồn và chuyển trạng thái serial.
    public void PostStockOut(PostStockOutCommand command)
    {
        EnsureApprovedAndAuthorized(command.Status, command.PostedByUserId, "stock-out",
            allowWarrantyPermission: command.Kind == StockOutKind.WarrantyReplacement);
        if (command.Kind != StockOutKind.Sale &&
            command.Kind != StockOutKind.Adjustment &&
            command.Kind != StockOutKind.WarrantyReplacement)
        {
            throw new InventoryDomainException("Only sale, adjustment or warranty-replacement stock-out can be posted by this service.");
        }

        if (command.Quantity <= 0)
        {
            throw new InventoryDomainException("Stock-out quantity must be greater than zero.");
        }

        var warehouseId = command.WarehouseId;
        var product = _unitOfWork.GetProduct(command.ProductId);
        var serialNumbers = QuanLyHangHoa.Helpers.SerialNumberNormalizer.NormalizeAll(command.SerialNumbers).ToArray();

        EnsureNoDuplicateSerials(serialNumbers);

        if (product.IsSerialTracked && serialNumbers.Length != command.Quantity)
        {
            throw new InventoryDomainException("Serial count must match stock-out quantity.");
        }

        if (!product.IsSerialTracked && serialNumbers.Length > 0)
        {
            throw new InventoryDomainException("Non-serial products cannot be issued with serial numbers.");
        }

        // AvailableQuantity là cổng chống âm kho; OnHand và Available cùng giảm khi chưa có reservation riêng.
        var balance = _unitOfWork.FindBalance(command.ProductId, warehouseId);
        if (balance is null || balance.AvailableQuantity < command.Quantity)
        {
            throw new InventoryDomainException("Insufficient available stock.");
        }

        // kiểm hết serial trước khi stage số tồn để một serial sai không tạo trạng thái dở dang trong unit of work.
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

        // dùng đúng snapshot vừa kiểm available để phép kiểm và phép trừ dựa trên cùng một phiên bản dữ liệu.
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
                Status = command.Kind == StockOutKind.Adjustment ? SerialStatus.Inactive : SerialStatus.Sold,
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

    // chuyển kho là một giao dịch kép: trừ kho nguồn, cộng kho đích và giữ tổng tồn toàn hệ thống.
    public void PostStockTransfer(PostStockTransferCommand command)
    {
        EnsureApprovedAndAuthorized(command.Status, command.PostedByUserId, "stock-transfer", allowWarrantyPermission: false);

        if (command.Quantity <= 0)
        {
            throw new InventoryDomainException("Transfer quantity must be greater than zero.");
        }

        if (command.FromWarehouseId == command.ToWarehouseId)
        {
            throw new InventoryDomainException("Source and destination warehouses must be different.");
        }

        var product = _unitOfWork.GetProduct(command.ProductId);
        var serialNumbers = QuanLyHangHoa.Helpers.SerialNumberNormalizer.NormalizeAll(command.SerialNumbers).ToArray();

        EnsureNoDuplicateSerials(serialNumbers);

        if (product.IsSerialTracked && serialNumbers.Length != command.Quantity)
        {
            throw new InventoryDomainException("Serial count must match transfer quantity.");
        }

        // kho nguồn phải đủ available trước khi tạo snapshot số lượng mới.
        var fromBalance = _unitOfWork.FindBalance(command.ProductId, command.FromWarehouseId);
        if (fromBalance is null || fromBalance.AvailableQuantity < command.Quantity)
        {
            throw new InventoryDomainException("Insufficient available stock in source warehouse.");
        }

        // stage kho nguồn trước nhưng chỉ commit sau khi kho đích, serial và hai ledger đều hoàn tất.
        _unitOfWork.SaveBalance(fromBalance with
        {
            OnHandQuantity = fromBalance.OnHandQuantity - command.Quantity,
            AvailableQuantity = fromBalance.AvailableQuantity - command.Quantity
        });

        // kho đích nhận đúng lượng đã trừ ở nguồn.
        var toBalance = _unitOfWork.GetOrCreateBalance(command.ProductId, command.ToWarehouseId);
        _unitOfWork.SaveBalance(toBalance with
        {
            OnHandQuantity = toBalance.OnHandQuantity + command.Quantity,
            AvailableQuantity = toBalance.AvailableQuantity + command.Quantity
        });

        // serial giữ nguyên trạng thái InStock, chỉ đổi kho hiện tại.
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

        // hai ledger Out/In cùng document id tạo dấu vết cân bằng cho lần chuyển.
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

        // Posted và rowversion được lưu cùng commit; lần gọi sau đọc Posted, còn race song song bị concurrency conflict rollback.
        _unitOfWork.MarkDocumentPosted(command.DocumentId, "StockTransfer");
        _unitOfWork.Commit();
    }

    // lifecycle và permission đều phải đạt; quyền warranty chỉ mở cho đúng loại chứng từ bảo hành.
    private void EnsureApprovedAndAuthorized(
        StockDocumentStatus status,
        int userId,
        string documentType,
        bool allowWarrantyPermission)
    {
        if (status != StockDocumentStatus.Approved)
        {
            throw new InventoryDomainException($"Only approved {documentType} documents can be posted.");
        }

        var isAuthorized = allowWarrantyPermission
            ? _unitOfWork.CanProcessWarrantyStock(userId)
            : _unitOfWork.CanApproveStock(userId);
        if (!isAuthorized)
        {
            throw new InventoryDomainException("You are not authorized to approve stock documents.");
        }
    }

    // so sánh không phân biệt hoa thường vì serial khác casing vẫn là cùng một định danh vật lý.
    private static void EnsureNoDuplicateSerials(string[] serialNumbers)
    {
        if (serialNumbers.Length != serialNumbers.Distinct(System.StringComparer.OrdinalIgnoreCase).Count())
        {
            throw new InventoryDomainException("Duplicate serials are not allowed.");
        }
    }
}
