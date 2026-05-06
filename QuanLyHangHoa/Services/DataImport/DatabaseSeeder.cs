using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services.DataImport
{
    public class DatabaseSeeder
    {
        private readonly AppDbContext _context;
        private readonly string _excelPath;
        private readonly Dictionary<string, int> _categoryMap = new();
        private readonly Dictionary<string, int> _brandMap = new();
        private readonly Dictionary<string, int> _unitMap = new();
        private readonly Dictionary<string, int> _supplierMap = new();
        private readonly Dictionary<string, int> _customerMap = new();
        private readonly Dictionary<string, int> _productMap = new();
        private readonly Dictionary<string, int> _stockInMap = new();
        private readonly Dictionary<string, int> _stockOutMap = new();
        private readonly Dictionary<string, int> _purchaseInvoiceMap = new();
        private readonly Dictionary<string, int> _salesInvoiceMap = new();

        public DatabaseSeeder(AppDbContext context, string excelPath)
        {
            _context = context;
            _excelPath = excelPath;
        }

        public async Task<string> SeedAsync()
        {
            if (!File.Exists(_excelPath))
                return $"Lỗi: Không tìm thấy file Excel tại {_excelPath}";

            try
            {
                using var workbook = new XLWorkbook(_excelPath);
                var log = new System.Text.StringBuilder();

                // 1. Units
                await SeedTableAsync<Unit>(workbook, "Đơn vị tính", async (row, item) => {
                    item.UnitCode = row.Cell("UnitCode").GetString();
                    item.DisplayName = row.Cell("DisplayName").GetString();
                    item.IsActive = row.Cell("IsActive").GetBoolean();
                }, _unitMap, log);

                // 2. Categories
                await SeedTableAsync<Category>(workbook, "Loại hàng", async (row, item) => {
                    item.CategoryCode = row.Cell("CategoryCode").GetString();
                    item.DisplayName = row.Cell("DisplayName").GetString();
                    item.IsActive = row.Cell("IsActive").GetBoolean();
                }, _categoryMap, log);

                // 3. Brands
                await SeedTableAsync<Brand>(workbook, "Thương hiệu", async (row, item) => {
                    item.BrandCode = row.Cell("BrandCode").GetString();
                    item.DisplayName = row.Cell("DisplayName").GetString();
                    item.OriginCountry = row.Cell("OriginCountry").GetString();
                    item.IsActive = row.Cell("IsActive").GetBoolean();
                }, _brandMap, log);

                // 4. Suppliers
                await SeedTableAsync<Supplier>(workbook, "Nhà cung cấp", async (row, item) => {
                    item.SupplierCode = row.Cell("SupplierCode").GetString();
                    item.DisplayName = row.Cell("DisplayName").GetString();
                    item.Email = row.Cell("Email").GetString();
                    item.Phone = row.Cell("Phone").GetString();
                    item.IsActive = row.Cell("IsActive").GetBoolean();
                }, _supplierMap, log);

                // 5. Customers
                await SeedTableAsync<Customer>(workbook, "Khách hàng", async (row, item) => {
                    item.CustomerCode = row.Cell("CustomerCode").GetString();
                    item.DisplayName = row.Cell("DisplayName").GetString();
                    item.Email = row.Cell("Email").GetString();
                    item.Phone = row.Cell("Phone").GetString();
                    item.IsActive = row.Cell("IsActive").GetBoolean();
                }, _customerMap, log);

                // 6. Products
                await SeedTableAsync<Product>(workbook, "Sản phẩm", async (row, item) => {
                    item.ProductCode = row.Cell("ProductCode").GetString();
                    item.DisplayName = row.Cell("DisplayName").GetString();
                    item.DefaultPrice = (decimal)row.Cell("SalePrice").GetDouble();
                    item.IsActive = row.Cell("IsActive").GetBoolean();
                    item.IsSerialTracked = row.Cell("TrackSerial").GetBoolean();
                    item.WarrantyPeriodMonths = (int)row.Cell("WarrantyMonths").GetDouble();
                    item.OriginCountry = row.Cell("OriginCountry").GetString();

                    var excelCatId = row.Cell("CategoryId").GetString();
                    if (_categoryMap.TryGetValue(excelCatId, out int catId)) item.CategoryId = catId;

                    var excelBrandId = row.Cell("BrandId").GetString();
                    if (_brandMap.TryGetValue(excelBrandId, out int brandId)) item.BrandId = brandId;

                    var excelUnitId = row.Cell("UnitId").GetString();
                    if (_unitMap.TryGetValue(excelUnitId, out int unitId)) item.DefaultUnitId = unitId;
                }, _productMap, log);

                // Warehouse check - needed for StockIn/Out
                var defaultWarehouse = await _context.Warehouses.FirstOrDefaultAsync();
                if (defaultWarehouse == null)
                {
                    defaultWarehouse = new Warehouse { WarehouseCode = "WH001", DisplayName = "Kho chính", IsActive = true, IsDefault = true };
                    _context.Warehouses.Add(defaultWarehouse);
                    await _context.SaveChangesAsync();
                }

                // 7. StockIn
                var stockInLineMap = new Dictionary<string, int>(); // excelStockInId_productId -> dbLineId
                await SeedTableAsync<StockIn>(workbook, "Phiếu nhập kho", async (row, item) => {
                    item.DocumentCode = row.Cell("VoucherCode").GetString();
                    item.Status = row.Cell("Status").GetString();
                    item.PurposeCode = row.Cell("StockInType").GetString();
                    item.WarehouseId = defaultWarehouse.Id;
                    item.CreatedBy = 1;
                    
                    var excelSupId = row.Cell("SupplierId").GetString();
                    if (_supplierMap.TryGetValue(excelSupId, out int supId)) item.SupplierId = supId;

                    var linesJson = row.Cell("Lines").GetString();
                    if (!string.IsNullOrEmpty(linesJson))
                    {
                        var lines = JsonSerializer.Deserialize<List<ExcelStockLine>>(linesJson);
                        if (lines != null)
                        {
                            foreach (var line in lines)
                            {
                                if (_productMap.TryGetValue(line.ProductId, out int prodId))
                                {
                                    var newLine = new StockInLine {
                                        ProductId = prodId,
                                        Quantity = line.Quantity,
                                        BaseQuantity = line.Quantity,
                                        UnitPrice = line.UnitCost,
                                        UnitId = (await _context.Products.FindAsync(prodId))?.DefaultUnitId ?? 1
                                    };
                                    item.Lines.Add(newLine);
                                }
                            }
                        }
                    }
                }, _stockInMap, log, async (excelId, item) => {
                    // Map lines after save
                    foreach (var line in item.Lines)
                    {
                        stockInLineMap[$"{excelId}_{line.ProductId}"] = line.Id;
                    }
                });

                // 8. StockOut
                var stockOutLineMap = new Dictionary<string, int>();
                await SeedTableAsync<StockOut>(workbook, "Phiếu xuất kho", async (row, item) => {
                    item.DocumentCode = row.Cell("VoucherCode").GetString();
                    item.Status = row.Cell("Status").GetString();
                    item.PurposeCode = row.Cell("StockOutType").GetString();
                    item.WarehouseId = defaultWarehouse.Id;
                    item.CreatedBy = 1;

                    var excelCustId = row.Cell("CustomerId").GetString();
                    if (_customerMap.TryGetValue(excelCustId, out int custId)) item.CustomerId = custId;

                    var linesJson = row.Cell("Lines").GetString();
                    if (!string.IsNullOrEmpty(linesJson))
                    {
                        var lines = JsonSerializer.Deserialize<List<ExcelStockLine>>(linesJson);
                        if (lines != null)
                        {
                            foreach (var line in lines)
                            {
                                if (_productMap.TryGetValue(line.ProductId, out int prodId))
                                {
                                    var newLine = new StockOutLine {
                                        ProductId = prodId,
                                        Quantity = line.Quantity,
                                        BaseQuantity = line.Quantity,
                                        UnitPrice = line.UnitPrice,
                                        UnitId = (await _context.Products.FindAsync(prodId))?.DefaultUnitId ?? 1
                                    };
                                    item.Lines.Add(newLine);
                                }
                            }
                        }
                    }
                }, _stockOutMap, log, async (excelId, item) => {
                    foreach (var line in item.Lines)
                    {
                        stockOutLineMap[$"{excelId}_{line.ProductId}"] = line.Id;
                    }
                });

                // 9. ProductSerial
                await SeedTableAsync<ProductSerial>(workbook, "Serial", async (row, item) => {
                    item.SerialNumber = row.Cell("SerialCode").GetString();
                    item.CurrentStatus = row.Cell("Status").GetString();
                    item.CurrentWarehouseId = defaultWarehouse.Id;

                    var excelProdId = row.Cell("ProductId").GetString();
                    if (_productMap.TryGetValue(excelProdId, out int prodId)) item.ProductId = prodId;

                    var excelStockInId = row.Cell("StockInId").GetString();
                    if (string.IsNullOrEmpty(excelStockInId))
                    {
                        // Try to find any stock in line for this product if not specified
                        var firstLine = await _context.StockInLines.FirstOrDefaultAsync(l => l.ProductId == prodId);
                        if (firstLine != null) item.LastStockInLineId = firstLine.Id;
                    }
                    else if (stockInLineMap.TryGetValue($"{excelStockInId}_{prodId}", out int lineId))
                    {
                        item.LastStockInLineId = lineId;
                    }

                    var excelStockOutId = row.Cell("StockOutId").GetString();
                    if (!string.IsNullOrEmpty(excelStockOutId) && stockOutLineMap.TryGetValue($"{excelStockOutId}_{prodId}", out int outLineId))
                    {
                        item.LastStockOutLineId = outLineId;
                    }
                }, new Dictionary<string, int>(), log);

                return log.ToString();
            }
            catch (Exception ex)
            {
                return $"Lỗi trong quá trình seeding: {ex.Message}\n{ex.StackTrace}";
            }
        }

        private async Task SeedTableAsync<T>(XLWorkbook workbook, string sheetName, Func<ExcelRowWrapper, T, Task> mapAction, Dictionary<string, int> idMap, System.Text.StringBuilder log, Func<string, T, Task>? afterSaveAction = null) where T : class, new()
        {
            var sheet = workbook.Worksheet(sheetName);
            if (sheet == null)
            {
                log.AppendLine($"Bỏ qua sheet '{sheetName}': Không tìm thấy.");
                return;
            }

            var count = await _context.Set<T>().CountAsync();
            if (count > 10)
            {
                log.AppendLine($"Bỏ qua bảng '{typeof(T).Name}': Đã có {count} dòng (> 10).");
                return;
            }

            var rows = sheet.RangeUsed().RowsUsed().Skip(1);
            var headers = sheet.Row(1).CellsUsed().ToDictionary(c => c.Value.ToString(), c => c.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);

            int imported = 0;
            foreach (var row in rows)
            {
                var item = new T();
                await mapAction(new ExcelRowWrapper(row, headers), item);
                _context.Set<T>().Add(item);
                await _context.SaveChangesAsync();

                // Get the generated ID and map it to the Excel ID
                var idProp = typeof(T).GetProperty("Id");
                if (idProp != null && headers.TryGetValue("id", out int idCol))
                {
                    int dbId = (int)idProp.GetValue(item);
                    string excelId = row.Cell(idCol).GetString();
                    if (!string.IsNullOrEmpty(excelId))
                    {
                        idMap[excelId] = dbId;
                        
                        if (afterSaveAction != null)
                        {
                            await afterSaveAction(excelId, item);
                        }
                    }
                }
                imported++;
            }

            log.AppendLine($"Đã nạp {imported} dòng vào bảng '{typeof(T).Name}'.");
        }

        private class ExcelRowWrapper
        {
            private readonly IXLRangeRow _row;
            private readonly Dictionary<string, int> _headers;

            public ExcelRowWrapper(IXLRangeRow row, Dictionary<string, int> headers)
            {
                _row = row;
                _headers = headers;
            }

            public IXLCell Cell(string headerName)
            {
                if (_headers.TryGetValue(headerName, out int col))
                    return _row.Cell(col);
                
                // Return an empty cell if header not found
                return _row.Worksheet.Cell(1, 16384); // Use the last possible cell as a dummy empty cell
            }
        }

        private class ExcelStockLine
        {
            public string ProductId { get; set; } = "";
            public decimal Quantity { get; set; }
            public decimal UnitCost { get; set; } // For StockIn
            public decimal UnitPrice { get; set; } // For StockOut
        }
    }
}
