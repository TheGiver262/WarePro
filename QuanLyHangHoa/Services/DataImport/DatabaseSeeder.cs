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
        
        // Maps to store Excel ID (hex string) -> Database ID (int)
        private readonly Dictionary<string, int> _unitMap = new();
        private readonly Dictionary<string, int> _categoryMap = new();
        private readonly Dictionary<string, int> _brandMap = new();
        private readonly Dictionary<string, int> _supplierMap = new();
        private readonly Dictionary<string, int> _customerMap = new();
        private readonly Dictionary<string, int> _productMap = new();
        private readonly Dictionary<string, int> _stockInMap = new();
        private readonly Dictionary<string, int> _stockOutMap = new();
        private readonly Dictionary<string, int> _purchaseInvoiceMap = new();
        private readonly Dictionary<string, int> _salesInvoiceMap = new();
        private readonly Dictionary<string, int> _warrantyClaimMap = new();

        public DatabaseSeeder(AppDbContext context, string excelPath)
        {
            _context = context;
            _excelPath = excelPath;
        }

        public async Task<string> SeedAsync()
        {
            Console.WriteLine($"[SEED] Starting seed from: {Path.GetFullPath(_excelPath)}");
            if (!File.Exists(_excelPath))
            {
                return $"Lỗi: Không tìm thấy file Excel tại {_excelPath}";
            }

            try
            {
                using var stream = new FileStream(_excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var workbook = new XLWorkbook(stream);
                var log = new System.Text.StringBuilder();

                // 1. Units
                await SeedTableWithMappingAsync<Unit>(workbook, "Đơn vị tính", "UnitCode", "id", (row, item) =>
                {
                    item.UnitCode = row.GetString("UnitCode") ?? "UNT";
                    item.DisplayName = row.GetString("DisplayName") ?? "Đơn vị";
                    item.IsActive = true;
                }, _unitMap, log);

                // 2. Categories
                await SeedTableWithMappingAsync<Category>(workbook, "Loại hàng", "CategoryCode", "id", (row, item) =>
                {
                    item.CategoryCode = row.GetString("CategoryCode") ?? "CAT";
                    item.DisplayName = row.GetString("DisplayName") ?? "Nhóm hàng";
                    item.IsActive = true;
                }, _categoryMap, log);

                // 3. Brands
                await SeedTableWithMappingAsync<Brand>(workbook, "Thương hiệu", "BrandCode", "id", (row, item) =>
                {
                    item.BrandCode = row.GetString("BrandCode") ?? "BRD";
                    item.DisplayName = row.GetString("DisplayName") ?? "Thương hiệu";
                    item.IsActive = true;
                }, _brandMap, log);

                // 4. Suppliers
                await SeedTableWithMappingAsync<Supplier>(workbook, "Nhà cung cấp", "SupplierCode", "id", (row, item) =>
                {
                    item.SupplierCode = row.GetString("SupplierCode") ?? "SUP";
                    item.DisplayName = row.GetString("DisplayName") ?? "Nhà cung cấp";
                    item.IsActive = true;
                }, _supplierMap, log);

                // 5. Customers
                await SeedTableWithMappingAsync<Customer>(workbook, "Khách hàng", "CustomerCode", "id", (row, item) =>
                {
                    item.CustomerCode = row.GetString("CustomerCode") ?? "CUS";
                    item.DisplayName = row.GetString("DisplayName") ?? "Khách hàng";
                    item.IsActive = true;
                }, _customerMap, log);

                // Warehouse
                var warehouse = await _context.Warehouses.FirstOrDefaultAsync();
                if (warehouse == null)
                {
                    warehouse = new Warehouse { WarehouseCode = "WH001", DisplayName = "Kho chính", IsActive = true, IsDefault = true };
                    _context.Warehouses.Add(warehouse);
                    await _context.SaveChangesAsync();
                }

                // 6. Products
                await SeedTableWithMappingAsync<Product>(workbook, "Sản phẩm", "ProductCode", "id", (row, item) =>
                {
                    item.ProductCode = row.GetString("ProductCode") ?? "PROD";
                    item.DisplayName = row.GetString("DisplayName") ?? "Sản phẩm";
                    item.Description = row.GetString("Description");
                    item.CostPrice = row.GetDecimal("CostPrice");
                    item.DefaultPrice = row.GetDecimal("SalePrice") ?? 0;
                    item.IsActive = row.GetString("IsActive")?.ToLower() == "true";
                    item.IsSerialTracked = row.GetString("TrackSerial")?.ToLower() == "true";
                    item.WarrantyPeriodMonths = (int)(row.GetDouble("WarrantyMonths") ?? 12);
                    
                    var catRef = row.GetString("CategoryId");
                    item.CategoryId = _categoryMap.GetValueOrDefault(catRef ?? "");
                    
                    var brandRef = row.GetString("BrandId");
                    item.BrandId = _brandMap.GetValueOrDefault(brandRef ?? "");
                    
                    var unitRef = row.GetString("UnitId");
                    item.DefaultUnitId = _unitMap.GetValueOrDefault(unitRef ?? "");
                    
                    if (item.CategoryId == 0) item.CategoryId = _categoryMap.Values.FirstOrDefault();
                    if (item.BrandId == 0) item.BrandId = _brandMap.Values.FirstOrDefault();
                    if (item.DefaultUnitId == 0) item.DefaultUnitId = _unitMap.Values.FirstOrDefault();
                }, _productMap, log);

                // 7. StockIn (Opening Balances)
                await SeedTableWithMappingAsync<StockIn>(workbook, "Phiếu nhập kho", "DocumentCode", "id", (row, item) =>
                {
                    item.DocumentCode = row.GetString("DocumentCode") ?? row.GetString("VoucherCode") ?? "PNK";
                    item.CreatedAt = row.GetDateTime("Ngày nhập") ?? DateTime.Now;
                    item.WarehouseId = warehouse.Id;
                    item.Status = "Completed";
                    item.PurposeCode = "OpeningBalance";
                    item.CreatedBy = 1;
                    var supRef = row.GetString("SupplierId");
                    if (!string.IsNullOrEmpty(supRef)) item.SupplierId = _supplierMap.GetValueOrDefault(supRef);
                }, _stockInMap, log);

                // 8. Serials (and implied lines)
                if (workbook.Worksheets.TryGetWorksheet("Serial", out var serialSheet))
                {
                    var serialCount = await _context.ProductSerials.CountAsync();
                    if (serialCount == 0)
                    {
                        var rows = serialSheet.RangeUsed()!.RowsUsed().Skip(1);
                        var headers = serialSheet.Row(1).CellsUsed().ToDictionary(c => c.Value.ToString().Trim(), c => c.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);
                        int imported = 0;

                        // To avoid duplicate StockInLines, we'll keep a map of (StockInId, ProductId)
                        var lineMap = new Dictionary<(int, int), StockInLine>();

                        foreach (var row in rows)
                        {
                            var wrapper = new ExcelRowWrapper(row, headers);
                            string sn = wrapper.GetString("SerialCode") ?? "";
                            string prodRef = wrapper.GetString("ProductId") ?? "";
                            string stockInRef = wrapper.GetString("StockInId") ?? "";
                            string stockOutRef = wrapper.GetString("StockOutId") ?? "";

                            int productId = _productMap.GetValueOrDefault(prodRef);
                            int stockInId = _stockInMap.GetValueOrDefault(stockInRef);
                            int stockOutId = _stockOutMap.GetValueOrDefault(stockOutRef);

                            if (stockInId == 0) stockInId = _stockInMap.Values.FirstOrDefault();

                            if (productId > 0 && stockInId > 0 && !string.IsNullOrEmpty(sn))
                            {
                                if (!lineMap.TryGetValue((stockInId, productId), out var line))
                                {
                                    line = await _context.StockInLines.FirstOrDefaultAsync(l => l.StockInId == stockInId && l.ProductId == productId);
                                    if (line == null)
                                    {
                                        line = new StockInLine { StockInId = stockInId, ProductId = productId, Quantity = 0, BaseQuantity = 0, UnitId = 1 };
                                        _context.StockInLines.Add(line);
                                        await _context.SaveChangesAsync();
                                    }
                                    lineMap[(stockInId, productId)] = line;
                                }

                                var existingSerial = await _context.ProductSerials.FirstOrDefaultAsync(s => s.SerialNumber == sn);
                                if (existingSerial == null)
                                {
                                    var ps = new ProductSerial
                                    {
                                        SerialNumber = sn,
                                        ProductId = productId,
                                        CurrentStatus = wrapper.GetString("Status") ?? (stockOutId > 0 ? "Sold" : "InStock"),
                                        Note = wrapper.GetString("Note"),
                                        CurrentWarehouseId = warehouse.Id,
                                        LastStockInLineId = line!.Id
                                    };

                                    if (stockOutId > 0)
                                    {
                                        // Find or create StockOutLine
                                        var sol = await _context.StockOutLines.FirstOrDefaultAsync(l => l.StockOutId == stockOutId && l.ProductId == productId);
                                        if (sol == null)
                                        {
                                            sol = new StockOutLine { StockOutId = stockOutId, ProductId = productId, Quantity = 0, BaseQuantity = 0, UnitId = 1 };
                                            _context.StockOutLines.Add(sol);
                                            await _context.SaveChangesAsync();
                                        }
                                        ps.LastStockOutLineId = sol.Id;
                                        sol.Quantity++;
                                        sol.BaseQuantity++;
                                    }

                                    _context.ProductSerials.Add(ps);
                                    line!.Quantity++;
                                    line!.BaseQuantity++;
                                    imported++;
                                }
                            }
                        }
                        await _context.SaveChangesAsync();
                        log.AppendLine($"Đã nạp {imported} số Serial vào hệ thống.");
                    }
                }

                // 9. StockOut
                await SeedTableWithMappingAsync<StockOut>(workbook, "Phiếu xuất kho", "DocumentCode", "id", (row, item) =>
                {
                    item.DocumentCode = row.GetString("DocumentCode") ?? "PXK";
                    item.CreatedAt = row.GetDateTime("Ngày xuất") ?? DateTime.Now;
                    item.WarehouseId = warehouse.Id;
                    item.Status = row.GetString("Status") ?? "Completed";
                    item.PurposeCode = row.GetString("PurposeCode") ?? "Sales";
                    item.CreatedBy = (int)(row.GetDouble("CreatedBy") ?? 1);
                    var custRef = row.GetString("CustomerId");
                    if (!string.IsNullOrEmpty(custRef)) item.CustomerId = _customerMap.GetValueOrDefault(custRef);
                }, _stockOutMap, log);

                // 10. Purchase Invoices
                await SeedTableWithMappingAsync<PurchaseInvoice>(workbook, "Hóa đơn mua", "InvoiceCode", "id", (row, item) =>
                {
                    item.InvoiceCode = row.GetString("Code") ?? "PIV";
                    item.InvoiceDate = row.GetDateTime("Date") ?? DateTime.Now;
                    item.GrandTotal = row.GetDecimal("TotalAmount") ?? 0;
                    item.CreatedAt = DateTime.Now;
                    var supRef = row.GetString("SupplierId");
                    if (!string.IsNullOrEmpty(supRef)) item.SupplierId = _supplierMap.GetValueOrDefault(supRef);
                }, _purchaseInvoiceMap, log);

                // 11. Sales Invoices
                await SeedTableWithMappingAsync<SalesInvoice>(workbook, "Hóa đơn bán", "InvoiceCode", "id", (row, item) =>
                {
                    item.InvoiceCode = row.GetString("Code") ?? "SIV";
                    item.InvoiceDate = row.GetDateTime("Date") ?? DateTime.Now;
                    item.GrandTotal = row.GetDecimal("TotalAmount") ?? 0;
                    item.CreatedAt = DateTime.Now;
                    var custRef = row.GetString("CustomerId");
                    if (!string.IsNullOrEmpty(custRef)) item.CustomerId = _customerMap.GetValueOrDefault(custRef);
                }, _salesInvoiceMap, log);

                // 12. Warranty Claims
                await SeedTableWithMappingAsync<WarrantyClaim>(workbook, "Yêu cầu bảo hành", "ClaimCode", "id", (row, item) =>
                {
                    item.ClaimCode = row.GetString("ClaimCode") ?? "WRN";
                    item.ReceivedDate = row.GetDateTime("ReceivedDate") ?? DateTime.Now;
                    item.ProblemDescription = row.GetString("ProblemDescription");
                    item.ResolutionType = row.GetString("ResolutionType");
                    item.Status = row.GetString("Status") ?? "Pending";
                    item.ProcessedBy = (int)(row.GetDouble("ProcessedBy") ?? 1);
                }, _warrantyClaimMap, log);

                return log.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SEED ERROR] {ex.Message}");
                if (ex.InnerException != null) Console.WriteLine($"[INNER] {ex.InnerException.Message}");
                return $"Lỗi Seeding: {ex.Message}";
            }
        }

        private async Task SeedTableWithMappingAsync<T>(XLWorkbook workbook, string sheetName, string codeHeader, string idHeader, Action<ExcelRowWrapper, T> mapAction, Dictionary<string, int> idMap, System.Text.StringBuilder log) where T : class, new()
        {
            if (!workbook.Worksheets.TryGetWorksheet(sheetName, out var sheet))
            {
                log.AppendLine($"Bỏ qua sheet '{sheetName}': Không tìm thấy.");
                return;
            }

            var headers = sheet.Row(1).CellsUsed().ToDictionary(c => c.Value.ToString().Trim(), c => c.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);
            var rows = sheet.RangeUsed()!.RowsUsed().Skip(1);
            
            // Build idMap from existing data if possible
            // We use the 'codeHeader' to match Excel rows with DB rows
            var existingItems = await _context.Set<T>().ToListAsync();
            var codeProp = typeof(T).GetProperty(codeHeader.Contains("Code") ? codeHeader : (typeof(T).Name + "Code"));
            if (codeProp == null) codeProp = typeof(T).GetProperty("DocumentCode") ?? typeof(T).GetProperty("DisplayName");

            foreach (var row in rows)
            {
                var wrapper = new ExcelRowWrapper(row, headers);
                string? code = wrapper.GetString(codeHeader) ?? wrapper.GetString(codeProp?.Name ?? "");
                string? excelId = wrapper.GetString(idHeader);

                var existing = existingItems.FirstOrDefault(i => code != null && string.Equals(codeProp?.GetValue(i)?.ToString(), code, StringComparison.OrdinalIgnoreCase));
                
                if (existing != null)
                {
                    if (!string.IsNullOrEmpty(excelId))
                    {
                        var idVal = (int)typeof(T).GetProperty("Id")!.GetValue(existing)!;
                        idMap[excelId] = idVal;
                    }
                }
                else
                {
                    var item = new T();
                    mapAction(wrapper, item);
                    _context.Set<T>().Add(item);
                    await _context.SaveChangesAsync();
                    
                    if (!string.IsNullOrEmpty(excelId))
                    {
                        var idVal = (int)typeof(T).GetProperty("Id")!.GetValue(item)!;
                        idMap[excelId] = idVal;
                    }
                    existingItems.Add(item);
                }
            }
            log.AppendLine($"Đã đồng bộ/nạp bảng '{typeof(T).Name}'.");
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

            public string? GetString(string header)
            {
                if (string.IsNullOrEmpty(header)) return null;
                if (_headers.TryGetValue(header, out int col))
                {
                    var val = _row.Cell(col).Value.ToString();
                    return string.IsNullOrWhiteSpace(val) ? null : val.Trim();
                }
                return null;
            }

            public decimal? GetDecimal(string header) => decimal.TryParse(GetString(header), out decimal d) ? d : null;
            public double? GetDouble(string header) => double.TryParse(GetString(header), out double d) ? d : null;
            public DateTime? GetDateTime(string header)
            {
                if (_headers.TryGetValue(header, out int col))
                {
                    if (_row.Cell(col).TryGetValue(out DateTime dt)) return dt;
                }
                return null;
            }
        }
    }
}
