using System.Linq;

namespace QuanLyHangHoa.Inventory;

public sealed class InventoryAdjustmentService
{
    private readonly IInventoryUnitOfWork _unitOfWork;
    private readonly IDefaultWarehouseProvider _warehouseProvider;
    private readonly IClock _clock;

    public InventoryAdjustmentService(
        IInventoryUnitOfWork unitOfWork,
        IDefaultWarehouseProvider warehouseProvider,
        IClock clock)
    {
        _unitOfWork = unitOfWork;
        _warehouseProvider = warehouseProvider;
        _clock = clock;
    }

    public void PostAdjustment(PostStockAdjustmentCommand command)
    {
        if (command.Status != StockDocumentStatus.Approved)
        {
            throw new InventoryDomainException("Only approved stock adjustments can be posted.");
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            throw new InventoryDomainException("Stock adjustment reason is required.");
        }

        if (command.Lines.Count == 0)
        {
            throw new InventoryDomainException("Stock adjustment must contain at least one line.");
        }

        var warehouseId = _warehouseProvider.GetDefaultWarehouseId();
        foreach (var line in command.Lines)
        {
            if (line.Quantity <= 0)
            {
                throw new InventoryDomainException("Stock adjustment quantity must be greater than zero.");
            }

            var product = _unitOfWork.GetProduct(line.ProductId);
            var serialNumbers = line.SerialNumbers
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToArray();

            EnsureNoDuplicateSerials(serialNumbers);

            if (product.IsSerialTracked && serialNumbers.Length != line.Quantity)
            {
                throw new InventoryDomainException("Serial count must match stock adjustment quantity.");
            }

            if (!product.IsSerialTracked && serialNumbers.Length > 0)
            {
                throw new InventoryDomainException("Non-serial products cannot be adjusted with serial numbers.");
            }

            var balance = _unitOfWork.GetOrCreateBalance(line.ProductId, warehouseId);
            if (line.Direction == StockLedgerDirection.Out && balance.AvailableQuantity < line.Quantity)
            {
                throw new InventoryDomainException("Insufficient available stock.");
            }

            if (line.Direction == StockLedgerDirection.In)
            {
                foreach (var serialNumber in serialNumbers)
                {
                    if (_unitOfWork.SerialExists(serialNumber))
                    {
                        throw new InventoryDomainException($"Serial {serialNumber} already exists.");
                    }
                }
            }
            else
            {
                foreach (var serialNumber in serialNumbers)
                {
                    var serial = _unitOfWork.GetSerial(serialNumber);
                    if (serial.ProductId != line.ProductId)
                    {
                        throw new InventoryDomainException($"Serial {serialNumber} does not belong to product {line.ProductId}.");
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
            }

            var signedQuantity = line.Direction == StockLedgerDirection.In ? line.Quantity : -line.Quantity;
            _unitOfWork.SaveBalance(balance with
            {
                OnHandQuantity = balance.OnHandQuantity + signedQuantity,
                AvailableQuantity = balance.AvailableQuantity + signedQuantity
            });

            foreach (var serialNumber in serialNumbers)
            {
                if (line.Direction == StockLedgerDirection.In)
                {
                    _unitOfWork.SaveSerial(new ProductSerialSnapshot(
                        serialNumber,
                        line.ProductId,
                        warehouseId,
                        SerialStatus.InStock));
                }
                else
                {
                    var serial = _unitOfWork.GetSerial(serialNumber);
                    _unitOfWork.SaveSerial(serial with
                    {
                        CurrentWarehouseId = null,
                        Status = SerialStatus.Inactive
                    });
                }
            }

            _unitOfWork.AddLedger(new StockLedgerEntry(
                command.DocumentId,
                line.ProductId,
                warehouseId,
                line.Direction,
                line.Quantity,
                _clock.Now,
                command.PostedByUserId));
        }

        _unitOfWork.AddAudit(new AuditLogEntry(
            command.DocumentId,
            AuditActionCode.PostStockAdjustment,
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
