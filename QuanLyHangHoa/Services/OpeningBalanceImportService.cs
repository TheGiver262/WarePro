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

                    postingService.PostStockIn(new PostStockInCommand(
                        0,
                        warehouseId,
                        StockInKind.OpeningBalance,
                        StockDocumentStatus.Approved,
                        row.ProductId,
                        row.Quantity,
                        StockInService.ParseSerialRange(row.SerialNumbers),
                        postedByUserId));

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
