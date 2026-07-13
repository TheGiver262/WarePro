using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Inventory;

namespace QuanLyHangHoa.Services
{
    public interface IProductSerialImportService
    {
        Task<(int SuccessCount, string Message)> ImportFromExcelAsync(string filePath, int actorId);
    }

    public class ProductSerialImportService : IProductSerialImportService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public ProductSerialImportService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<(int SuccessCount, string Message)> ImportFromExcelAsync(string filePath, int actorId)
        {
            using var _context = _contextFactory();
            AuthorizationService.RequireFreshActor(_context, actorId, PermissionAction.ManageMasterData);
            if (!File.Exists(filePath))
                return (0, $"Lỗi: Không tìm thấy file Excel tại {filePath}");

            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var workbook = new XLWorkbook(stream);
                
                // 1. Prepare Product Mapping (Mongo ID -> ProductCode)
                Console.WriteLine("[IMPORT] Reading 'Sản phẩm' sheet...");
                var productSheet = workbook.Worksheet("Sản phẩm");
                if (productSheet == null) {
                    Console.WriteLine("[IMPORT] Error: 'Sản phẩm' sheet not found!");
                    return (0, "Lỗi: Không tìm thấy sheet 'Sản phẩm'");
                }

                var mongoToCodeMap = new Dictionary<string, string>();
                var productRows = productSheet.RangeUsed()!.RowsUsed().Skip(1);
                var pHeaders = productSheet.Row(1).CellsUsed().ToDictionary(c => c.Value.ToString(), c => c.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);

                foreach (var row in productRows)
                {
                    string mongoId = pHeaders.TryGetValue("id", out int idCol) ? row.Cell(idCol).GetString() : "";
                    string code = pHeaders.TryGetValue("ProductCode", out int codeCol) ? row.Cell(codeCol).GetString() : "";
                    if (!string.IsNullOrEmpty(mongoId) && !string.IsNullOrEmpty(code))
                    {
                        mongoToCodeMap[mongoId] = code;
                    }
                }
                Console.WriteLine($"[IMPORT] Mapped {mongoToCodeMap.Count} products.");

                // 2. Prepare Serial Sheet
                Console.WriteLine("[IMPORT] Reading 'Serial' sheet...");
                var serialSheet = workbook.Worksheet("Serial");
                if (serialSheet == null) {
                    Console.WriteLine("[IMPORT] Error: 'Serial' sheet not found!");
                    return (0, "Lỗi: Không tìm thấy sheet 'Serial'");
                }

                var sHeaders = serialSheet.Row(1).CellsUsed().ToDictionary(c => c.Value.ToString(), c => c.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);
                var serialRows = serialSheet.RangeUsed()!.RowsUsed().Skip(1);
                Console.WriteLine($"[IMPORT] Found {serialRows.Count()} rows in Serial sheet.");

                // 3. Load Existing Data from DB
                var dbProducts = await _context.Products.ToDictionaryAsync(p => p.ProductCode, p => p.Id);
                var defaultWarehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.IsDefault && w.IsActive) 
                                       ?? await _context.Warehouses.FirstOrDefaultAsync();

                if (defaultWarehouse == null)
                {
                    defaultWarehouse = new Warehouse { WarehouseCode = "WH001", DisplayName = "Kho chính", IsActive = true, IsDefault = true };
                    _context.Warehouses.Add(defaultWarehouse);
                    await _context.SaveChangesAsync();
                }

                // 4. Create a StockIn document for the import
                var stockIn = new StockIn
                {
                    DocumentCode = $"IMPORT_SR_{DateTime.Now:yyyyMMdd_HHmm}",
                    Status = "Posted",
                    PurposeCode = "OpeningBalance",
                    WarehouseId = defaultWarehouse.Id,
                    CreatedAt = DateTime.Now,
                    PostedAt = DateTime.Now,
                    CreatedBy = actorId,
                    PostedBy = actorId
                };
                _context.StockIns.Add(stockIn);
                await _context.SaveChangesAsync();

                int successCount = 0;
                int skipCount = 0;
                var productGroups = new Dictionary<int, List<string>>();

                foreach (var row in serialRows)
                {
                    string serialNumber = sHeaders.TryGetValue("SerialCode", out int scCol) ? row.Cell(scCol).GetString() : "";
                    string mongoProductId = sHeaders.TryGetValue("ProductId", out int piCol) ? row.Cell(piCol).GetString() : "";

                    if (string.IsNullOrEmpty(serialNumber) || string.IsNullOrEmpty(mongoProductId)) 
                    {
                        Console.WriteLine($"[IMPORT] Skipping row: Serial={serialNumber}, MongoId={mongoProductId}");
                        skipCount++;
                        continue;
                    }

                    // Map Mongo ProductId to SQL ProductId via ProductCode
                    if (mongoToCodeMap.TryGetValue(mongoProductId, out string? productCode) && productCode != null)
                    {
                        var matchedProduct = dbProducts.FirstOrDefault(p => string.Equals(p.Key, productCode, StringComparison.OrdinalIgnoreCase));
                        
                        if (matchedProduct.Key != null)
                        {
                            int productId = matchedProduct.Value;
                            if (!productGroups.ContainsKey(productId))
                                productGroups[productId] = new List<string>();
                            
                            productGroups[productId].Add(serialNumber);
                        }
                        else
                        {
                            Console.WriteLine($"[IMPORT] No DB match for ProductCode: {productCode}");
                            skipCount++;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[IMPORT] No Mongo mapping for ID: {mongoProductId}");
                        skipCount++;
                    }
                }

                // 5. Create StockInLines and ProductSerials
                foreach (var group in productGroups)
                {
                    int productId = group.Key;
                    var serials = group.Value;

                    var product = await _context.Products.FindAsync(productId);
                    if (product == null) continue;

                    var line = new StockInLine
                    {
                        StockInId = stockIn.Id,
                        ProductId = productId,
                        Quantity = serials.Count,
                        BaseQuantity = serials.Count,
                        UnitPrice = product.DefaultPrice,
                        UnitId = product.DefaultUnitId
                    };
                    _context.StockInLines.Add(line);
                    await _context.SaveChangesAsync(); 

                    Console.WriteLine($"[IMPORT] Processing product ID {productId} with {serials.Count} serials...");

                    foreach (var sn in serials)
                    {
                        if (await _context.ProductSerials.AnyAsync(s => s.SerialNumber == sn)) continue;

                        var ps = new ProductSerial
                        {
                            SerialNumber = sn,
                            ProductId = productId,
                            CurrentStatus = "InStock",
                            CurrentWarehouseId = defaultWarehouse.Id,
                            LastStockInLineId = line.Id
                        };
                        _context.ProductSerials.Add(ps);
                        successCount++;
                    }
                }

                Console.WriteLine($"[IMPORT] Finalizing changes. Saving {successCount} serials...");
                await _context.SaveChangesAsync();
                string summaryMessage = $"Đã nạp thành công {successCount} số serial.";
                if (skipCount > 0) summaryMessage += $" (Bỏ qua {skipCount} dòng không hợp lệ hoặc thiếu sản phẩm).";
                return (successCount, summaryMessage);
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                if (ex.InnerException != null) msg += $" Inner: {ex.InnerException.Message}";
                Console.WriteLine($"[IMPORT] FATAL ERROR: {msg}");
                return (0, $"Lỗi Import: {msg}");
            }
        }
    }
}
