using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services.DataImport;

namespace QuanLyHangHoa.Services
{
    public class OpeningBalanceImportService
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly ExcelImportService _excelImportService = new();
        private readonly CsvImportService _csvImportService = new();

        public OpeningBalanceImportService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
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

            var result = ImportRows(parsed.ImportedItems, postedByUserId);
            result.Errors.InsertRange(0, parsed.Errors);
            return result;
        }

        public ImportResult<OpeningBalanceImportRow> ImportRows(IEnumerable<OpeningBalanceImportRow> rows, int postedByUserId)
        {
            var result = new ImportResult<OpeningBalanceImportRow>();

            int stockInId = 0;
            try
            {
                using var db = _contextFactory();
                var warehouseProvider = new DbDefaultWarehouseProvider(db);
                var warehouseId = warehouseProvider.GetDefaultWarehouseId();
                var stockIn = new StockIn
                {
                    DocumentCode = $"SI-OB-{DateTime.Now:yyyyMMddHHmmss}",
                    WarehouseId = warehouseId,
                    PurposeCode = "OpeningBalance",
                    Status = DocumentStatus.Posted,
                    ImportDate = DateTime.Now,
                    Notes = $"Import tồn đầu kỳ từ Excel/CSV",
                    CreatedBy = postedByUserId,
                    CreatedAt = DateTime.Now,
                    PostedBy = postedByUserId,
                    PostedAt = DateTime.Now
                };
                db.StockIns.Add(stockIn);
                db.SaveChanges();
                stockInId = stockIn.Id;
            }
            catch (Exception ex)
            {
                result.Errors.Add(new RowError
                {
                    RowNumber = 0,
                    Data = "Khởi tạo chứng từ nhập đầu kỳ",
                    ErrorMessage = $"Không thể khởi tạo chứng từ nhập đầu kỳ: {ex.Message}"
                });
                return result;
            }

            foreach (var row in rows)
            {
                try
                {
                    using var db = _contextFactory();
                    using var transaction = db.Database.BeginTransaction();
                    var warehouseProvider = new DbDefaultWarehouseProvider(db);
                    var warehouseId = warehouseProvider.GetDefaultWarehouseId();
                    var postingService = new InventoryPostingService(
                        new EfInventoryUnitOfWork(db),
                        warehouseProvider,
                        new SystemClock());

                    var product = db.Products.Find(row.ProductId);
                    var unitId = db.ProductUnits
                        .Where(pu => pu.ProductId == row.ProductId && pu.IsBaseUnit)
                        .Select(pu => pu.UnitId)
                        .FirstOrDefault();
                    if (unitId == 0 && product != null) unitId = product.DefaultUnitId;
                    if (unitId == 0) unitId = 1;

                    var line = new StockInLine
                    {
                        StockInId = stockInId,
                        ProductId = row.ProductId,
                        UnitId = unitId,
                        Quantity = row.Quantity,
                        BaseQuantity = row.Quantity,
                        UnitPrice = product?.DefaultPrice ?? 0,
                        DraftSerials = string.IsNullOrWhiteSpace(row.SerialNumbers) ? null : row.SerialNumbers
                    };
                    db.StockInLines.Add(line);
                    db.SaveChanges();

                    postingService.PostStockIn(new PostStockInCommand(
                        stockInId,
                        warehouseId,
                        StockInKind.OpeningBalance,
                        StockDocumentStatus.Approved,
                        row.ProductId,
                        row.Quantity,
                        StockInService.ParseSerialRange(row.SerialNumbers),
                        postedByUserId));

                    var sns = StockInService.ParseSerialRange(row.SerialNumbers);
                    if (sns.Any())
                    {
                        var dbSerials = db.ProductSerials.Where(ps => sns.Contains(ps.SerialNumber)).ToList();
                        foreach (var s in dbSerials)
                        {
                            s.LastStockInLineId = line.Id;
                        }
                        db.SaveChanges();
                    }

                    transaction.Commit();
                    result.ImportedItems.Add(row);
                    result.SuccessCount++;
                }
                catch (Exception ex) when (ex is InventoryDomainException or InvalidOperationException)
                {
                    result.Errors.Add(new RowError
                    {
                        RowNumber = row.RowNumber,
                        Data = $"ProductId={row.ProductId}; Quantity={row.Quantity}; SerialNumbers={row.SerialNumbers}",
                        ErrorMessage = ex.Message
                    });
                }
            }

            if (result.SuccessCount == 0 && stockInId > 0)
            {
                try
                {
                    using var db = _contextFactory();
                    var stockIn = db.StockIns.Find(stockInId);
                    if (stockIn != null)
                    {
                        db.StockIns.Remove(stockIn);
                        db.SaveChanges();
                    }
                }
                catch
                {
                    // Ignore
                }
            }

            return result;
        }

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
                    .FirstOrDefault(warehouse => warehouse.IsDefault && warehouse.IsActive);

                return warehouse?.Id ?? 1;
            }
        }

        private sealed class SystemClock : IClock
        {
            public DateTime Now => DateTime.Now;
        }
    }
}
