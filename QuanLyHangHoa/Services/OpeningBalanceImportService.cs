using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services.DataImport;

namespace QuanLyHangHoa.Services;

public sealed class OpeningBalanceImportService
{
    private readonly Func<AppDbContext> _contextFactory;
    private readonly ExcelImportService _excelImportService = new();
    private readonly CsvImportService _csvImportService = new();

    public OpeningBalanceImportService(Func<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public ImportResult<OpeningBalanceImportRow> ImportFile(string filePath, int postedByUserId)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var parsed = extension switch
        {
            ".xlsx" or ".xls" => _excelImportService.Import<OpeningBalanceImportRow>(filePath),
            ".csv" => _csvImportService.Import<OpeningBalanceImportRow>(filePath),
            _ => throw new NotSupportedException("Dinh dang file khong duoc ho tro.")
        };

        if (parsed.Errors.Count > 0)
        {
            return parsed;
        }

        return ImportRows(parsed.ImportedItems, postedByUserId);
    }

    public ImportResult<OpeningBalanceImportRow> ImportRows(
        IEnumerable<OpeningBalanceImportRow> rows,
        int postedByUserId)
    {
        var result = new ImportResult<OpeningBalanceImportRow>();
        var sourceRows = rows.ToList();
        if (sourceRows.Count == 0)
        {
            result.Errors.Add(new RowError
            {
                RowNumber = 0,
                ErrorMessage = "Không có dữ liệu tồn đầu kỳ để import."
            });
            return result;
        }

        using var db = _contextFactory();
        var warehouseProvider = new DbDefaultWarehouseProvider(db);
        int warehouseId;
        try
        {
            warehouseId = warehouseProvider.GetDefaultWarehouseId();
        }
        catch (InventoryDomainException ex)
        {
            result.Errors.Add(new RowError { RowNumber = 0, ErrorMessage = ex.Message });
            return result;
        }

        var preparedRows = PrepareRows(db, sourceRows, result);
        if (result.Errors.Count > 0)
        {
            return result;
        }

        using var transaction = db.Database.BeginTransaction();
        try
        {
            var now = DateTime.UtcNow;
            var stockIn = new StockIn
            {
                DocumentCode = $"SI-OB-{now:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}"[..35],
                WarehouseId = warehouseId,
                PurposeCode = "OpeningBalance",
                Status = DocumentStatus.Posted,
                ImportDate = now,
                Notes = "Import tồn đầu kỳ từ Excel/CSV",
                CreatedBy = postedByUserId,
                CreatedAt = now,
                PostedBy = postedByUserId,
                PostedAt = now
            };
            db.StockIns.Add(stockIn);

            var persistedLines = new List<(PreparedOpeningBalanceRow Prepared, StockInLine Line)>();
            foreach (var prepared in preparedRows)
            {
                var line = new StockInLine
                {
                    StockIn = stockIn,
                    ProductId = prepared.Product.Id,
                    UnitId = prepared.UnitId,
                    Quantity = prepared.Source.Quantity,
                    BaseQuantity = prepared.Source.Quantity,
                    UnitPrice = prepared.Product.CostPrice ?? prepared.Product.DefaultPrice,
                    DraftSerials = prepared.SerialNumbers.Length == 0
                        ? null
                        : string.Join(",", prepared.SerialNumbers)
                };
                db.StockInLines.Add(line);
                persistedLines.Add((prepared, line));
            }

            db.SaveChanges();

            var postingService = new InventoryPostingService(
                new EfInventoryUnitOfWork(db),
                warehouseProvider,
                new SystemClock());

            foreach (var item in persistedLines)
            {
                postingService.PostStockIn(new PostStockInCommand(
                    stockIn.Id,
                    warehouseId,
                    StockInKind.OpeningBalance,
                    StockDocumentStatus.Posted,
                    item.Prepared.Product.Id,
                    item.Prepared.Source.Quantity,
                    item.Prepared.SerialNumbers,
                    postedByUserId));

                if (item.Prepared.SerialNumbers.Length > 0)
                {
                    var serials = db.ProductSerials
                        .Where(serial => item.Prepared.SerialNumbers.Contains(serial.SerialNumber))
                        .ToList();
                    foreach (var serial in serials)
                    {
                        serial.LastStockInLineId = item.Line.Id;
                    }
                }
            }

            db.SaveChanges();
            transaction.Commit();
            result.ImportedItems.AddRange(sourceRows);
            result.SuccessCount = sourceRows.Count;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            result.Errors.Add(new RowError
            {
                RowNumber = 0,
                Data = "Chứng từ nhập tồn đầu kỳ",
                ErrorMessage = ex.Message
            });
        }

        return result;
    }

    private static List<PreparedOpeningBalanceRow> PrepareRows(
        AppDbContext db,
        IReadOnlyCollection<OpeningBalanceImportRow> rows,
        ImportResult<OpeningBalanceImportRow> result)
    {
        var prepared = new List<PreparedOpeningBalanceRow>();
        var documentSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            try
            {
                var product = db.Products.SingleOrDefault(item => item.Id == row.ProductId && item.IsActive)
                    ?? throw new InventoryDomainException($"Product {row.ProductId} does not exist.");
                if (row.Quantity <= 0)
                {
                    throw new InventoryDomainException("Stock-in quantity must be greater than zero.");
                }

                var serialNumbers = StockInService.ParseSerialRange(row.SerialNumbers)
                    .Select(serial => serial.Trim())
                    .Where(serial => serial.Length > 0)
                    .ToArray();
                if (serialNumbers.Length != serialNumbers.Distinct(StringComparer.OrdinalIgnoreCase).Count())
                {
                    throw new InventoryDomainException("Duplicate serials are not allowed.");
                }

                if (serialNumbers.Any(serial => !documentSerials.Add(serial)))
                {
                    throw new InventoryDomainException("Duplicate serials are not allowed.");
                }

                if (product.IsSerialTracked &&
                    (row.Quantity != decimal.Truncate(row.Quantity) || serialNumbers.Length != (int)row.Quantity))
                {
                    throw new InventoryDomainException("Serial count must match stock-in quantity.");
                }

                if (!product.IsSerialTracked && serialNumbers.Length > 0)
                {
                    throw new InventoryDomainException("Non-serial products cannot receive serial numbers.");
                }

                if (serialNumbers.Length > 0 &&
                    db.ProductSerials.Any(serial => serialNumbers.Contains(serial.SerialNumber)))
                {
                    throw new InventoryDomainException("One or more serial numbers already exist.");
                }

                var unitId = db.ProductUnits
                    .Where(unit => unit.ProductId == product.Id && unit.IsBaseUnit)
                    .Select(unit => unit.UnitId)
                    .FirstOrDefault();
                if (unitId == 0)
                {
                    unitId = product.DefaultUnitId;
                }

                prepared.Add(new PreparedOpeningBalanceRow(row, product, unitId, serialNumbers));
            }
            catch (InventoryDomainException ex)
            {
                result.Errors.Add(new RowError
                {
                    RowNumber = row.RowNumber,
                    Data = $"ProductId={row.ProductId}; Quantity={row.Quantity}; SerialNumbers={row.SerialNumbers}",
                    ErrorMessage = ex.Message
                });
            }
        }

        return prepared;
    }

    private sealed record PreparedOpeningBalanceRow(
        OpeningBalanceImportRow Source,
        Product Product,
        int UnitId,
        string[] SerialNumbers);

    private sealed class DbDefaultWarehouseProvider : IDefaultWarehouseProvider
    {
        private readonly AppDbContext _context;

        public DbDefaultWarehouseProvider(AppDbContext context)
        {
            _context = context;
        }

        public int GetDefaultWarehouseId()
        {
            var warehouse = _context.Warehouses
                .Where(item => item.IsActive)
                .OrderByDescending(item => item.IsDefault)
                .ThenBy(item => item.Id)
                .FirstOrDefault();

            return warehouse?.Id
                ?? throw new InventoryDomainException("Không tìm thấy kho đang hoạt động để nhập tồn đầu kỳ.");
        }
    }

    private sealed class SystemClock : IClock
    {
        public DateTime Now => DateTime.UtcNow;
    }
}
