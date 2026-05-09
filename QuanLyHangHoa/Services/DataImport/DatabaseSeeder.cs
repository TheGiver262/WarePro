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
                await SeedTableWithMappingAsync<Unit>(workbook, "\u0110\u01A1n v\u1ECB t\u00EDnh", "UnitCode", "id", (row, item) =>
                {
                    item.UnitCode = row.GetString("UnitCode") ?? "UNT";
                    item.DisplayName = row.GetString("DisplayName") ?? "\u0110\u01A1n v\u1ECB";
                    item.IsActive = true;
                }, _unitMap, log);

                // 2. Categories
                await SeedTableWithMappingAsync<Category>(workbook, "Lo\u1EA1i h\u00E0ng", "CategoryCode", "id", (row, item) =>
                {
                    item.CategoryCode = row.GetString("CategoryCode") ?? "CAT";
                    item.DisplayName = row.GetString("DisplayName") ?? "Nh\u00F3m h\u00E0ng";
                    item.IsActive = true;
                }, _categoryMap, log);

                // 3. Brands
                await SeedTableWithMappingAsync<Brand>(workbook, "Th\u01B0\u01A1ng hi\u1EC7u", "BrandCode", "id", (row, item) =>
                {
                    item.BrandCode = row.GetString("BrandCode") ?? "BRD";
                    item.DisplayName = row.GetString("DisplayName") ?? "Th\u01B0\u01A1ng hi\u1EC7u";
                    item.OriginCountry = TranslateOrigin(row.GetString("Origin") ?? row.GetString("OriginCountry") ?? row.GetString("XuatXu"));
                    item.IsActive = true;
                }, _brandMap, log);

                // 4. Suppliers
                await SeedTableWithMappingAsync<Supplier>(workbook, "Nh\u00E0 cung c\u1EA5p", "SupplierCode", "id", (row, item) =>
                {
                    item.SupplierCode = row.GetString("SupplierCode") ?? "SUP";
                    item.DisplayName = row.GetString("DisplayName") ?? "Nh\u00E0 cung c\u1EA5p";
                    item.IsActive = true;
                }, _supplierMap, log);

                // 5. Customers
                await SeedTableWithMappingAsync<Customer>(workbook, "Kh\u00E1ch h\u00E0ng", "CustomerCode", "id", (row, item) =>
                {
                    item.CustomerCode = row.GetString("CustomerCode") ?? "CUS";
                    item.DisplayName = row.GetString("DisplayName") ?? "Kh\u00E1ch h\u00E0ng";
                    item.IsActive = true;
                }, _customerMap, log);

                // Warehouse
                var warehouse = await _context.Warehouses.FirstOrDefaultAsync();
                if (warehouse == null)
                {
                    warehouse = new Warehouse { WarehouseCode = "WH001", DisplayName = "\u004B\u0068\u00F4\u0020\u0063\u0068\u00ED\u006E\u0068", IsActive = true, IsDefault = true }; // Kho chính
                    _context.Warehouses.Add(warehouse);
                    await _context.SaveChangesAsync();
                }

                // 6. Products
                await SeedTableWithMappingAsync<Product>(workbook, "S\u1EA3n ph\u1EA9m", "ProductCode", "id", (row, item) =>
                {
                    item.ProductCode = row.GetString("ProductCode") ?? "PROD";
                    item.DisplayName = row.GetString("DisplayName") ?? "S\u1EA3n ph\u1EA9m";
                    item.Description = row.GetString("Description");
                    item.CostPrice = row.GetDecimal("CostPrice");
                    item.DefaultPrice = row.GetDecimal("SalePrice") ?? 0;
                    item.IsActive = row.GetString("IsActive")?.ToLower() == "true";
                    item.IsSerialTracked = row.GetString("TrackSerial")?.ToLower() == "true";
                    item.WarrantyPeriodMonths = (int)(row.GetDouble("WarrantyMonths") ?? 12);
                    item.OriginCountry = TranslateOrigin(row.GetString("Origin") ?? row.GetString("OriginCountry") ?? row.GetString("XuatXu"));
                    
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
                await SeedTableWithMappingAsync<StockIn>(workbook, "Phi\u1EBFu nh\u1EADp kho", "VoucherCode", "id", (row, item) =>
                {
                    item.DocumentCode = row.GetString("DocumentCode") ?? row.GetString("VoucherCode") ?? "PNK";
                    item.CreatedAt = row.GetDateTime("Ng\u00E0y nh\u1EADp") ?? row.GetDateTime("VoucherDate") ?? DateTime.Now;
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
                        var justCreatedLines = new HashSet<int>();
                        var justCreatedOutLines = new HashSet<int>();

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
                                        // We need to increment BEFORE saving to satisfy the > 0 constraint
                                        line.Quantity = 1;
                                        line.BaseQuantity = 1;
                                        _context.StockInLines.Add(line);
                                        await _context.SaveChangesAsync();
                                        
                                        // Mark as already incremented for this first serial
                                        lineMap[(stockInId, productId)] = line;
                                        // We'll use a local set to track which lines were JUST created in this loop
                                        // to avoid double-incrementing them at the end of the loop
                                        justCreatedLines.Add(line.Id);
                                    }
                                    else
                                    {
                                        lineMap[(stockInId, productId)] = line;
                                    }
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
                                            sol = new StockOutLine { StockOutId = stockOutId, ProductId = productId, Quantity = 1, BaseQuantity = 1, UnitId = 1 };
                                            _context.StockOutLines.Add(sol);
                                            await _context.SaveChangesAsync();
                                            justCreatedOutLines.Add(sol.Id);
                                        }
                                        ps.LastStockOutLineId = sol.Id;
                                        if (!justCreatedOutLines.Contains(sol.Id))
                                        {
                                            sol.Quantity++;
                                            sol.BaseQuantity++;
                                        }
                                    }

                                    _context.ProductSerials.Add(ps);
                                    if (!justCreatedLines.Contains(line!.Id))
                                    {
                                        line!.Quantity++;
                                        line!.BaseQuantity++;
                                    }
                                    imported++;
                                }
                            }
                        }
                        await _context.SaveChangesAsync();
                        log.AppendLine($"Đã nạp {imported} số Serial vào hệ thống.");
                    }
                }

                // 9. StockOut
                await SeedTableWithMappingAsync<StockOut>(workbook, "Phi\u1EBFu xu\u1EA5t kho", "VoucherCode", "id", (row, item) =>
                {
                    item.DocumentCode = row.GetString("DocumentCode") ?? row.GetString("VoucherCode") ?? "PXK";
                    item.CreatedAt = row.GetDateTime("Ng\u00E0y xu\u1EA5t") ?? row.GetDateTime("VoucherDate") ?? DateTime.Now;
                    item.WarehouseId = warehouse.Id;
                    item.Status = row.GetString("Status") ?? "Completed";
                    
                    // Map Excel 'StockOutType' to DB 'PurposeCode'
                    string type = row.GetString("StockOutType") ?? row.GetString("PurposeCode") ?? "Sale";
                    if (type == "Sales" || type == "Sale") item.PurposeCode = "Sale";
                    else if (type == "WarrantyReplacement") item.PurposeCode = "WarrantyReplacement";
                    else item.PurposeCode = "Sale"; // Fallback to Sale for other types to satisfy constraint
                    item.CreatedBy = (int)(row.GetDouble("CreatedBy") ?? 1);
                    var custRef = row.GetString("CustomerId");
                    if (!string.IsNullOrEmpty(custRef)) item.CustomerId = _customerMap.GetValueOrDefault(custRef);
                    if (item.CustomerId == 0) item.CustomerId = _customerMap.Values.FirstOrDefault();
                }, _stockOutMap, log);

                // 10. Purchase Invoices
                await SeedTableWithMappingAsync<PurchaseInvoice>(workbook, "H\u00F3a \u0111\u01A1n mua", "InvoiceCode", "id", (row, item) =>
                {
                    item.InvoiceCode = row.GetString("InvoiceCode") ?? row.GetString("Code") ?? "PIV";
                    item.InvoiceDate = row.GetDateTime("Ng\u00E0y h\u00F3a \u0111\u01A1n") ?? row.GetDateTime("InvoiceDate") ?? DateTime.Now;
                    item.GrandTotal = row.GetDecimal("TotalAmount") ?? 0;
                    item.CreatedAt = DateTime.Now;
                    var supRef = row.GetString("SupplierId");
                    if (!string.IsNullOrEmpty(supRef)) item.SupplierId = _supplierMap.GetValueOrDefault(supRef);
                }, _purchaseInvoiceMap, log);

                // 11. Sales Invoices
                await SeedTableWithMappingAsync<SalesInvoice>(workbook, "H\u00F3a \u0111\u01A1n b\u00E1n", "InvoiceCode", "id", (row, item) =>
                {
                    item.InvoiceCode = row.GetString("InvoiceCode") ?? row.GetString("Code") ?? "SIV";
                    item.InvoiceDate = row.GetDateTime("Ng\u00E0y h\u00F3a \u0111\u01A1n") ?? row.GetDateTime("InvoiceDate") ?? DateTime.Now;
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
                log.AppendLine($"B\u1ECF qua sheet '{sheetName}': Kh\u00F4ng t\u00ECm th\u1EA5y.");
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
            log.AppendLine($"\u0110\u00E3 \u0111\u1ED3ng b\u1ED9/n\u1EA1p b\u1EA3ng '{typeof(T).Name}'.");
        }

        private string? TranslateOrigin(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            string normalized = input.Trim();
            
            // Dictionary for translation
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "USA", "M\u1EF9" },
                { "United States", "M\u1EF9" },
                { "Japan", "Nh\u1EADt" },
                { "South Korea", "H\u00E0n Qu\u1ED1c" },
                { "Korea", "H\u00E0n Qu\u1ED1c" },
                { "China", "Trung Qu\u1ED1c" },
                { "Taiwan", "\u0110\u00E0i Loan" },
                { "Switzerland", "Th\u1EE5y S\u0129" },
                { "Netherlands", "H\u00E0 Lan" },
                { "Germany", "\u0110\u1EE9c" },
                { "UK", "Anh" },
                { "United Kingdom", "Anh" },
                { "France", "Ph\u00E1p" },
                { "Italy", "\u00DD" },
                { "Thailand", "Th\u00E1i Lan" },
                { "Vietnam", "Vi\u1EC7t Nam" },
                { "Trung qu\u1ED1c", "Trung Qu\u1ED1c" },
                { "\u0110\u00E0i loan", "\u0110\u00E0i Loan" },
                { "H\u00E0n qu\u1ED1c", "H\u00E0n Qu\u1ED1c" }
            };

            if (map.TryGetValue(normalized, out var translated))
            {
                return translated;
            }

            // Heuristic for corrupted Vietnamese characters or specific mappings
            if (normalized.Contains("?Ai Loan") || (normalized.Contains("A") && normalized.Contains("i Loan"))) return "Đài Loan";
            if (normalized.Contains("HA Lan") || (normalized.StartsWith("H") && normalized.EndsWith(" Lan"))) return "Hà Lan";
            if (normalized.Contains("Qu`c") || normalized.Contains("Quoc") || normalized.Contains("Qu`c"))
            {
                if (normalized.Contains("HAn") || (normalized.Contains("H") && normalized.Contains("n"))) return "Hàn Quốc";
                if (normalized.Contains("Trung")) return "Trung Quốc";
            }
            if (normalized.StartsWith("M") && normalized.Length <= 3) return "Mỹ";
            if (normalized.StartsWith("Nh") && normalized.Length <= 5) return "Nhật";
            if (normalized.Contains("Th") && normalized.Contains("S")) return "Thụy Sĩ";
            if (normalized.StartsWith("D") && normalized.EndsWith("c")) return "Đức";

            // Fallback: Standardize casing (Title Case)
            if (normalized.Length > 0)
            {
                return char.ToUpper(normalized[0]) + normalized.Substring(1).ToLower();
            }

            return normalized;
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
