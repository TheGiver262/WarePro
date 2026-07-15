using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                // Tự động kiểm tra và sửa dữ liệu mồ côi cho StockInLine -> StockIn trước khi seed
                var orphanStockInIds = await _context.StockInLines
                    .Select(l => l.StockInId)
                    .Distinct()
                    .Where(id => !_context.StockIns.Any(s => s.Id == id))
                    .ToListAsync();

                if (orphanStockInIds.Any())
                {
                    await _context.Database.OpenConnectionAsync();
                    try
                    {
                        await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT StockIn ON");
                        foreach (var id in orphanStockInIds)
                        {
                            await _context.Database.ExecuteSqlRawAsync(
                                "INSERT INTO StockIn (Id, DocumentCode, WarehouseId, PurposeCode, Status, CreatedBy, CreatedAt) " +
                                $"VALUES ({id}, 'SI-ORPHAN-{id}', 1, 'OpeningBalance', 'Posted', 1, '2026-05-01 00:00:00')"
                            );
                        }
                        await _context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT StockIn OFF");
                    }
                    finally
                    {
                        await _context.Database.CloseConnectionAsync();
                    }
                }

                using var stream = new FileStream(_excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var workbook = new XLWorkbook(stream);
                var log = new System.Text.StringBuilder();

                // 1. Units
                await SeedTableWithMappingAsync<Unit>(workbook, "Unit", "UnitCode", "Id", (row, item) =>
                {
                    item.UnitCode = row.GetString("UnitCode") ?? "UNT";
                    item.DisplayName = row.GetString("DisplayName") ?? "Đơn vị";
                    item.IsActive = true;
                }, _unitMap, log);

                // 2. Categories
                await SeedTableWithMappingAsync<Category>(workbook, "Category", "CategoryCode", "Id", (row, item) =>
                {
                    item.CategoryCode = row.GetString("CategoryCode") ?? "CAT";
                    item.DisplayName = row.GetString("DisplayName") ?? "Nhóm hàng";
                    item.IsActive = true;
                }, _categoryMap, log);

                // 3. Brands
                await SeedTableWithMappingAsync<Brand>(workbook, "Brand", "BrandCode", "Id", (row, item) =>
                {
                    item.BrandCode = row.GetString("BrandCode") ?? "BRD";
                    item.DisplayName = row.GetString("DisplayName") ?? "Thương hiệu";
                    item.OriginCountry = TranslateOrigin(row.GetString("Origin") ?? row.GetString("OriginCountry") ?? row.GetString("XuatXu"));
                    item.IsActive = true;
                }, _brandMap, log);

                // 4. Suppliers
                await SeedTableWithMappingAsync<Supplier>(workbook, "Supplier", "SupplierCode", "Id", (row, item) =>
                {
                    item.SupplierCode = row.GetString("SupplierCode") ?? "SUP";
                    item.DisplayName = row.GetString("DisplayName") ?? "Nhà cung cấp";
                    item.IsActive = true;
                }, _supplierMap, log);

                // 5. Customers
                await SeedTableWithMappingAsync<Customer>(workbook, "Customer", "CustomerCode", "Id", (row, item) =>
                {
                    item.CustomerCode = row.GetString("CustomerCode") ?? "CUS";
                    item.DisplayName = row.GetString("DisplayName") ?? "Khách hàng";
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
                await SeedTableWithMappingAsync<Product>(workbook, "Product", "ProductCode", "Id", (row, item) =>
                {
                    item.ProductCode = row.GetString("ProductCode") ?? "PROD";
                    item.DisplayName = row.GetString("DisplayName") ?? "Sản phẩm";
                    item.Description = row.GetString("Description");
                    item.CostPrice = row.GetDecimal("CostPrice") ?? 0;
                    item.DefaultPrice = row.GetDecimal("SalePrice") ?? row.GetDecimal("DefaultPrice") ?? 0;
                    item.IsActive = row.GetString("IsActive")?.ToLower() == "true" || row.GetString("IsActive") == "1";
                    item.IsSerialTracked = row.GetString("TrackSerial")?.ToLower() == "true" || row.GetString("IsSerialTracked") == "1";
                    item.WarrantyPeriodMonths = (int)(row.GetDouble("WarrantyMonths") ?? row.GetDouble("WarrantyPeriodMonths") ?? 12);
                    item.OriginCountry = TranslateOrigin(row.GetString("Origin") ?? row.GetString("OriginCountry") ?? row.GetString("XuatXu"));
                    
                    var catRef = row.GetString("CategoryId");
                    item.CategoryId = _categoryMap.GetValueOrDefault(catRef ?? "");
                    
                    var brandRef = row.GetString("BrandId");
                    item.BrandId = _brandMap.GetValueOrDefault(brandRef ?? "");
                    
                    var unitRef = row.GetString("UnitId") ?? row.GetString("DefaultUnitId");
                    item.DefaultUnitId = _unitMap.GetValueOrDefault(unitRef ?? "");
                    
                    if (item.CategoryId == 0) item.CategoryId = _categoryMap.Values.FirstOrDefault();
                    if (item.BrandId == 0) item.BrandId = _brandMap.Values.FirstOrDefault();
                    if (item.DefaultUnitId == 0) item.DefaultUnitId = _unitMap.Values.FirstOrDefault();
                }, _productMap, log);

                await SeedProductUnitsAsync(workbook, log);

                // 7. StockIn (Opening Balances)
                await SeedTableWithMappingAsync<StockIn>(workbook, "StockIn", "DocumentCode", "Id", (row, item) =>
                {
                    item.DocumentCode = row.GetString("DocumentCode") ?? row.GetString("VoucherCode") ?? "PNK";
                    item.CreatedAt = row.GetDateTime("Ngày nhập") ?? row.GetDateTime("VoucherDate") ?? row.GetDateTime("CreatedAt") ?? DateTime.Now;
                    item.WarehouseId = warehouse.Id;
                    var status = row.GetString("Status") ?? "Posted";
                    if (status.Equals("Completed", StringComparison.OrdinalIgnoreCase) || status.Equals("Complete", StringComparison.OrdinalIgnoreCase))
                    {
                        status = "Posted";
                    }
                    item.Status = status;
                    item.PurposeCode = row.GetString("PurposeCode") ?? "OpeningBalance";
                    item.CreatedBy = 1;
                    var supRef = row.GetString("SupplierId");
                    if (!string.IsNullOrEmpty(supRef)) item.SupplierId = _supplierMap.GetValueOrDefault(supRef);
                }, _stockInMap, log);

                // 8. Serials (and implied lines)
                if (workbook.Worksheets.TryGetWorksheet("ProductSerial", out var serialSheet))
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
                            string sn = wrapper.GetString("SerialNumber") ?? wrapper.GetString("SerialCode") ?? "";
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
                                        CurrentStatus = wrapper.GetString("CurrentStatus") ?? wrapper.GetString("Status") ?? (stockOutId > 0 ? "Sold" : "InStock"),
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
                await SeedTableWithMappingAsync<StockOut>(workbook, "StockOut", "DocumentCode", "Id", (row, item) =>
                {
                    item.DocumentCode = row.GetString("DocumentCode") ?? row.GetString("VoucherCode") ?? "PXK";
                    item.CreatedAt = row.GetDateTime("Ngày xuất") ?? row.GetDateTime("VoucherDate") ?? row.GetDateTime("CreatedAt") ?? DateTime.Now;
                    item.WarehouseId = warehouse.Id;
                    var status = row.GetString("Status") ?? "Posted";
                    if (status.Equals("Completed", StringComparison.OrdinalIgnoreCase) || status.Equals("Complete", StringComparison.OrdinalIgnoreCase))
                    {
                        status = "Posted";
                    }
                    item.Status = status;
                    
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
                await SeedTableWithMappingAsync<PurchaseInvoice>(workbook, "PurchaseInvoice", "InvoiceCode", "Id", (row, item) =>
                {
                    item.InvoiceCode = row.GetString("InvoiceCode") ?? row.GetString("Code") ?? "PIV";
                    item.InvoiceDate = row.GetDateTime("InvoiceDate") ?? DateTime.Now;
                    item.SubTotal = row.GetDecimal("SubTotal") ?? row.GetDecimal("TotalAmount") ?? 0;
                    item.TaxAmount = row.GetDecimal("TaxAmount") ?? 0;
                    item.GrandTotal = row.GetDecimal("GrandTotal") ?? row.GetDecimal("TotalAmount") ?? 0;
                    item.PaidAmount = row.GetDecimal("PaidAmount") ?? 0;
                    item.PaymentStatus = PaymentStatus.Normalize(row.GetString("PaymentStatus") ?? PaymentStatus.Unpaid);
                    item.DueDate = row.GetDateTime("DueDate");
                    item.Notes = row.GetString("Notes");
                    item.CreatedAt = DateTime.Now;
                    item.CreatedBy = 1;
                    var supRef = row.GetString("SupplierId");
                    if (!string.IsNullOrEmpty(supRef)) item.SupplierId = _supplierMap.GetValueOrDefault(supRef);
                }, _purchaseInvoiceMap, log);
                Console.WriteLine($"[SEED] PurchaseInvoice map count: {_purchaseInvoiceMap.Count}");
                
                // 10.5 Purchase Invoice Lines
                if (workbook.Worksheets.TryGetWorksheet("PurchaseInvoiceLine", out var piLineSheet))
                {
                    var existingLineCount = await _context.PurchaseInvoiceLines.CountAsync();
                    if (existingLineCount == 0)
                    {
                        var rows = piLineSheet.RangeUsed()!.RowsUsed().Skip(1);
                        var headers = piLineSheet.Row(1).CellsUsed().ToDictionary(c => c.Value.ToString().Trim(), c => c.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);
                        int importedLines = 0;

                        foreach (var row in rows)
                        {
                            var wrapper = new ExcelRowWrapper(row, headers);
                            string invRef = wrapper.GetString("PurchaseInvoiceId") ?? "";
                            int invoiceId = _purchaseInvoiceMap.GetValueOrDefault(invRef);
                            
                            if (invoiceId > 0)
                            {
                                var line = new PurchaseInvoiceLine
                                {
                                    PurchaseInvoiceId = invoiceId,
                                    ProductId = _productMap.GetValueOrDefault(wrapper.GetString("ProductId") ?? ""),
                                    UnitId = _unitMap.GetValueOrDefault(wrapper.GetString("UnitId") ?? ""),
                                    Quantity = wrapper.GetDecimal("Quantity") ?? 1,
                                    UnitPrice = wrapper.GetDecimal("UnitPrice") ?? 0,
                                    SubTotal = wrapper.GetDecimal("SubTotal") ?? 0,
                                    TaxRate = wrapper.GetDecimal("TaxRate") ?? 0,
                                    TaxAmount = wrapper.GetDecimal("TaxAmount") ?? 0,
                                    GrandTotal = wrapper.GetDecimal("GrandTotal") ?? 0
                                };
                                
                                if (line.ProductId == 0) line.ProductId = _productMap.Values.FirstOrDefault();
                                if (line.UnitId == 0) line.UnitId = _unitMap.Values.FirstOrDefault();
                                
                                _context.PurchaseInvoiceLines.Add(line);
                                importedLines++;
                            }
                        }
                        await _context.SaveChangesAsync();
                        log.AppendLine($"Đã nạp {importedLines} dòng hóa đơn mua.");
                    }
                }

                // 11. Sales Invoices
                await SeedTableWithMappingAsync<SalesInvoice>(workbook, "SalesInvoice", "InvoiceCode", "Id", (row, item) =>
                {
                    item.InvoiceCode = row.GetString("InvoiceCode") ?? row.GetString("Code") ?? "SIV";
                    item.InvoiceDate = row.GetDateTime("InvoiceDate") ?? DateTime.Now;
                    item.SubTotal = row.GetDecimal("SubTotal") ?? row.GetDecimal("TotalAmount") ?? 0;
                    item.TaxAmount = row.GetDecimal("TaxAmount") ?? 0;
                    item.GrandTotal = row.GetDecimal("GrandTotal") ?? row.GetDecimal("TotalAmount") ?? 0;
                    item.PaidAmount = row.GetDecimal("PaidAmount") ?? 0;
                    item.PaymentStatus = PaymentStatus.Normalize(row.GetString("PaymentStatus") ?? PaymentStatus.Unpaid);
                    item.DueDate = row.GetDateTime("DueDate");
                    item.Notes = row.GetString("Notes");
                    item.CreatedAt = DateTime.Now;
                    item.CreatedBy = 1;
                    var custRef = row.GetString("CustomerId");
                    if (!string.IsNullOrEmpty(custRef)) item.CustomerId = _customerMap.GetValueOrDefault(custRef);
                    
                    var stockOutRef = row.GetString("StockOutId");
                    if (!string.IsNullOrEmpty(stockOutRef)) item.StockOutId = _stockOutMap.GetValueOrDefault(stockOutRef);
                }, _salesInvoiceMap, log);

                // 11.5 Sales Invoice Lines
                if (workbook.Worksheets.TryGetWorksheet("SalesInvoiceLine", out var siLineSheet))
                {
                    var existingLineCount = await _context.SalesInvoiceLines.CountAsync();
                    if (existingLineCount == 0)
                    {
                        var rows = siLineSheet.RangeUsed()!.RowsUsed().Skip(1);
                        var headers = siLineSheet.Row(1).CellsUsed().ToDictionary(c => c.Value.ToString().Trim(), c => c.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);
                        int importedLines = 0;

                        foreach (var row in rows)
                        {
                            var wrapper = new ExcelRowWrapper(row, headers);
                            string invRef = wrapper.GetString("SalesInvoiceId") ?? "";
                            int invoiceId = _salesInvoiceMap.GetValueOrDefault(invRef);
                            
                            if (invoiceId > 0)
                            {
                                var line = new SalesInvoiceLine
                                {
                                    SalesInvoiceId = invoiceId,
                                    ProductId = _productMap.GetValueOrDefault(wrapper.GetString("ProductId") ?? ""),
                                    UnitId = _unitMap.GetValueOrDefault(wrapper.GetString("UnitId") ?? ""),
                                    Quantity = wrapper.GetDecimal("Quantity") ?? 1,
                                    UnitPrice = wrapper.GetDecimal("UnitPrice") ?? 0,
                                    SubTotal = wrapper.GetDecimal("SubTotal") ?? 0,
                                    TaxRate = wrapper.GetDecimal("TaxRate") ?? 0,
                                    TaxAmount = wrapper.GetDecimal("TaxAmount") ?? 0,
                                    GrandTotal = wrapper.GetDecimal("GrandTotal") ?? 0
                                };
                                
                                if (line.ProductId == 0) line.ProductId = _productMap.Values.FirstOrDefault();
                                if (line.UnitId == 0) line.UnitId = _unitMap.Values.FirstOrDefault();
                                
                                _context.SalesInvoiceLines.Add(line);
                                importedLines++;
                            }
                        }
                        await _context.SaveChangesAsync();
                        log.AppendLine($"Đã nạp {importedLines} dòng hóa đơn bán.");
                    }
                }

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

        private async Task SeedProductUnitsAsync(XLWorkbook workbook, System.Text.StringBuilder log)
        {
            if (!workbook.Worksheets.TryGetWorksheet("ProductUnit", out var sheet))
            {
                log.AppendLine("Bỏ qua sheet 'ProductUnit': Không tìm thấy.");
                return;
            }

            var headers = sheet.Row(1).CellsUsed().ToDictionary(
                cell => cell.Value.ToString().Trim(),
                cell => cell.Address.ColumnNumber,
                StringComparer.OrdinalIgnoreCase);
            var existingRows = await _context.ProductUnits.AsNoTracking().ToListAsync();
            var existingPairs = existingRows
                .Select(row => (row.ProductId, row.UnitId))
                .ToHashSet();
            var baseUnitProducts = existingRows
                .Where(row => row.IsBaseUnit)
                .Select(row => row.ProductId)
                .ToHashSet();
            var inserted = 0;

            foreach (var row in sheet.RangeUsed()!.RowsUsed().Skip(1))
            {
                var wrapper = new ExcelRowWrapper(row, headers);
                var productReference = wrapper.GetString("ProductId") ?? string.Empty;
                var unitReference = wrapper.GetString("UnitId") ?? string.Empty;
                if (!_productMap.TryGetValue(productReference, out var productId)
                    || !_unitMap.TryGetValue(unitReference, out var unitId))
                {
                    throw new InvalidDataException(
                        $"ProductUnit không resolve được ProductId '{productReference}' hoặc UnitId '{unitReference}'.");
                }

                var conversionFactor = wrapper.GetDecimal("ConversionFactor") ?? 0;
                if (conversionFactor <= 0)
                {
                    throw new InvalidDataException("ProductUnit.ConversionFactor phải lớn hơn 0.");
                }

                var isBaseUnit = IsTrue(wrapper.GetString("IsBaseUnit"));
                if (existingPairs.Contains((productId, unitId))
                    || (isBaseUnit && baseUnitProducts.Contains(productId)))
                {
                    continue;
                }

                _context.ProductUnits.Add(new ProductUnit
                {
                    ProductId = productId,
                    UnitId = unitId,
                    ConversionFactor = conversionFactor,
                    IsBaseUnit = isBaseUnit,
                    IsPurchaseUnit = IsTrue(wrapper.GetString("IsPurchaseUnit")),
                    IsSalesUnit = IsTrue(wrapper.GetString("IsSalesUnit"))
                });
                existingPairs.Add((productId, unitId));
                if (isBaseUnit)
                {
                    baseUnitProducts.Add(productId);
                }
                inserted++;
            }

            await _context.SaveChangesAsync();
            log.AppendLine($"Đã đồng bộ bảng 'ProductUnit': thêm {inserted} dòng.");
        }

        private static bool IsTrue(string? value) =>
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";

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
