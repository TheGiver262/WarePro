using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services.DataImport
{
    public class DatabaseSeeder
    {
        private AppDbContext _context = null!;
        private readonly DatabaseWriteExecutor _writeExecutor;
        private readonly string _excelPath;
        
        // mỗi map nối id dạng chuỗi trong excel với id số do database sinh; các bảng con dùng map này để giữ đúng khóa ngoại
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
        private readonly Dictionary<string, int> _productSerialMap = new();
        private readonly Dictionary<string, int> _warrantyCoverageMap = new();
        private readonly Dictionary<string, int> _warrantyClaimMap = new();

        public DatabaseSeeder(Func<AppDbContext> contextFactory, string excelPath)
        {
            ArgumentNullException.ThrowIfNull(contextFactory);
            _writeExecutor = new DatabaseWriteExecutor(contextFactory);
            _excelPath = excelPath;
        }

        // thứ tự seed đi từ bảng gốc đến chứng từ và dòng chi tiết vì mỗi bước sau cần id của bước trước
        public async Task<string> SeedAsync(CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"[SEED] Starting seed from: {Path.GetFullPath(_excelPath)}");
            if (!File.Exists(_excelPath))
            {
                return $"Lỗi: Không tìm thấy file Excel tại {_excelPath}";
            }

            // chụp workbook thành dữ liệu thuần trước retry; mỗi attempt phải đọc cùng một đầu vào dù file gốc bị thay đổi.
            var workbookBytes = await File.ReadAllBytesAsync(_excelPath, cancellationToken);
            var preparedWorkbook = PrepareWorkbook(workbookBytes, cancellationToken);

            return await _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest(
                    "database.seed",
                    Guid.NewGuid(),
                    IsolationLevel.Serializable),
                async (db, token) =>
                {
                    _context = db;
                    // executor tạo DbContext mới khi retry, nên map id sinh tự động cũng phải dựng lại theo đúng attempt đó.
                    ResetMaps();
                    try
                    {
                        return await SeedWorkbookAsync(preparedWorkbook, token);
                    }
                    finally
                    {
                        _context = null!;
                    }
                },
                cancellationToken: cancellationToken);
        }

        private async Task<string> SeedWorkbookAsync(
            PreparedWorkbook workbook,
            CancellationToken cancellationToken)
        {
            var log = new System.Text.StringBuilder();
            cancellationToken.ThrowIfCancellationRequested();
            // thứ tự seed giữ nguyên trong một executor attempt để toàn bộ thay đổi cùng transaction.
                // 1. Units
                await SeedTableWithMappingAsync<Unit>(workbook, "Unit", "UnitCode", "Id", (row, item) =>
                {
                    item.UnitCode = row.GetString("UnitCode") ?? "UNT";
                    item.DisplayName = row.GetString("DisplayName") ?? "Đơn vị";
                    item.IsActive = true;
                }, _unitMap, log, cancellationToken);

                // 2. Categories
                await SeedTableWithMappingAsync<Category>(workbook, "Category", "CategoryCode", "Id", (row, item) =>
                {
                    item.CategoryCode = row.GetString("CategoryCode") ?? "CAT";
                    item.DisplayName = row.GetString("DisplayName") ?? "Nhóm hàng";
                    item.IsActive = true;
                }, _categoryMap, log, cancellationToken);

                // 3. Brands
                await SeedTableWithMappingAsync<Brand>(workbook, "Brand", "BrandCode", "Id", (row, item) =>
                {
                    item.BrandCode = row.GetString("BrandCode") ?? "BRD";
                    item.DisplayName = row.GetString("DisplayName") ?? "Thương hiệu";
                    item.OriginCountry = TranslateOrigin(row.GetString("Origin") ?? row.GetString("OriginCountry") ?? row.GetString("XuatXu"));
                    item.IsActive = true;
                }, _brandMap, log, cancellationToken);

                // 4. Suppliers
                await SeedTableWithMappingAsync<Supplier>(workbook, "Supplier", "SupplierCode", "Id", (row, item) =>
                {
                    item.SupplierCode = row.GetString("SupplierCode") ?? "SUP";
                    item.DisplayName = row.GetString("DisplayName") ?? "Nhà cung cấp";
                    item.IsActive = true;
                }, _supplierMap, log, cancellationToken);

                // 5. Customers
                await SeedTableWithMappingAsync<Customer>(workbook, "Customer", "CustomerCode", "Id", (row, item) =>
                {
                    item.CustomerCode = row.GetString("CustomerCode") ?? "CUS";
                    item.DisplayName = row.GetString("DisplayName") ?? "Khách hàng";
                    item.IsActive = true;
                }, _customerMap, log, cancellationToken);

                // Warehouse
                var warehouse = await _context.Warehouses.FirstOrDefaultAsync(cancellationToken);
                if (warehouse == null)
                {
                    warehouse = new Warehouse { WarehouseCode = "WH001", DisplayName = "\u004B\u0068\u00F4\u0020\u0063\u0068\u00ED\u006E\u0068", IsActive = true, IsDefault = true }; // Kho chính
                    _context.Warehouses.Add(warehouse);
                    await _context.SaveChangesAsync(cancellationToken);
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
                }, _productMap, log, cancellationToken);

                // quan hệ sản phẩm - đơn vị chỉ được tạo sau khi cả hai map khóa đã đầy đủ
                await SeedProductUnitsAsync(workbook, log, cancellationToken);

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
                }, _stockInMap, log, cancellationToken);

                // 8. Serials (and implied lines)
                if (workbook.TryGetSheet("ProductSerial", out var serialSheet))
                {
                    var serialCount = await _context.ProductSerials.CountAsync(cancellationToken);
                    if (serialCount == 0)
                    {
                        int imported = 0;

                        // mỗi cặp phiếu nhập - sản phẩm chỉ có một dòng kho; lineMap giúp dùng lại dòng đó cho nhiều serial
                        var lineMap = new Dictionary<(int, int), StockInLine>();
                        var justCreatedLines = new HashSet<int>();
                        var justCreatedOutLines = new HashSet<int>();

                        foreach (var wrapper in serialSheet.Rows)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
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
                                    line = await _context.StockInLines.FirstOrDefaultAsync(
                                        l => l.StockInId == stockInId && l.ProductId == productId,
                                        cancellationToken);
                                    if (line == null)
                                    {
                                        line = new StockInLine { StockInId = stockInId, ProductId = productId, Quantity = 0, BaseQuantity = 0, UnitId = 1 };
                                        // dòng mới phải có số lượng 1 trước lần lưu đầu tiên để thỏa ràng buộc số lượng dương
                                        line.Quantity = 1;
                                        line.BaseQuantity = 1;
                                        _context.StockInLines.Add(line);
                                        await _context.SaveChangesAsync(cancellationToken);
                                        
                                        // serial đầu tiên đã được tính vào số lượng khi tạo dòng
                                        lineMap[(stockInId, productId)] = line;
                                        // tập id này phân biệt dòng vừa tạo để cuối vòng lặp không cộng serial đầu tiên thêm lần nữa
                                        justCreatedLines.Add(line.Id);
                                    }
                                    else
                                    {
                                        lineMap[(stockInId, productId)] = line;
                                    }
                                }

                                var existingSerial = await _context.ProductSerials.FirstOrDefaultAsync(
                                    s => s.SerialNumber == sn,
                                    cancellationToken);
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
                                        // serial đã bán cần dòng xuất tương ứng để giữ được lịch sử nhập - xuất
                                        var sol = await _context.StockOutLines.FirstOrDefaultAsync(
                                            l => l.StockOutId == stockOutId && l.ProductId == productId,
                                            cancellationToken);
                                        if (sol == null)
                                        {
                                            sol = new StockOutLine { StockOutId = stockOutId, ProductId = productId, Quantity = 1, BaseQuantity = 1, UnitId = 1 };
                                            _context.StockOutLines.Add(sol);
                                            await _context.SaveChangesAsync(cancellationToken);
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
                        await _context.SaveChangesAsync(cancellationToken);
                        log.AppendLine($"Đã nạp {imported} số Serial vào hệ thống.");
                    }
                }

                if (workbook.TryGetSheet("ProductSerial", out var warrantySerialSheet))
                {
                    // Map source serial IDs to database IDs for warranty data.
                    var serialIdsByNumber = (await _context.ProductSerials
                        .AsNoTracking()
                        .ToListAsync(cancellationToken))
                        .ToDictionary(serial => serial.SerialNumber, serial => serial.Id, StringComparer.OrdinalIgnoreCase);
                    foreach (var wrapper in warrantySerialSheet.Rows)
                    {
                        var sourceSerialId = wrapper.GetString("Id");
                        var serialNumber = wrapper.GetString("SerialNumber") ?? wrapper.GetString("SerialCode");
                        if (!string.IsNullOrWhiteSpace(sourceSerialId)
                            && !string.IsNullOrWhiteSpace(serialNumber)
                            && serialIdsByNumber.TryGetValue(serialNumber, out var productSerialId))
                        {
                            _productSerialMap[sourceSerialId] = productSerialId;
                        }
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
                    
                    // quy đổi tên mục đích trong file cũ về các giá trị PurposeCode mà database hiện tại cho phép
                    string type = row.GetString("StockOutType") ?? row.GetString("PurposeCode") ?? "Sale";
                    if (type == "Sales" || type == "Sale") item.PurposeCode = "Sale";
                    else if (type == "WarrantyReplacement") item.PurposeCode = "WarrantyReplacement";
                    else item.PurposeCode = "Sale"; // Fallback to Sale for other types to satisfy constraint
                    item.CreatedBy = 1;
                    var custRef = row.GetString("CustomerId");
                    if (!string.IsNullOrEmpty(custRef)) item.CustomerId = _customerMap.GetValueOrDefault(custRef);
                    if (item.CustomerId == 0) item.CustomerId = _customerMap.Values.FirstOrDefault();
                }, _stockOutMap, log, cancellationToken);

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
                }, _purchaseInvoiceMap, log, cancellationToken);
                Console.WriteLine($"[SEED] PurchaseInvoice map count: {_purchaseInvoiceMap.Count}");
                
                // 10.5 Purchase Invoice Lines
                if (workbook.TryGetSheet("PurchaseInvoiceLine", out var piLineSheet))
                {
                    var existingLineCount = await _context.PurchaseInvoiceLines.CountAsync(cancellationToken);
                    if (existingLineCount == 0)
                    {
                        int importedLines = 0;

                        foreach (var wrapper in piLineSheet.Rows)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
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
                        await _context.SaveChangesAsync(cancellationToken);
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
                }, _salesInvoiceMap, log, cancellationToken);

                // 11.5 Sales Invoice Lines
                if (workbook.TryGetSheet("SalesInvoiceLine", out var siLineSheet))
                {
                    var existingLineCount = await _context.SalesInvoiceLines.CountAsync(cancellationToken);
                    if (existingLineCount == 0)
                    {
                        int importedLines = 0;

                        foreach (var wrapper in siLineSheet.Rows)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
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
                        await _context.SaveChangesAsync(cancellationToken);
                        log.AppendLine($"Đã nạp {importedLines} dòng hóa đơn bán.");
                    }
                }

                // 12. Warranty coverage
                await SeedWarrantyCoveragesAsync(workbook, log, cancellationToken);

                // 13. Warranty claims
                var warrantyClaimSheetName = workbook.TryGetSheet("Yêu cầu bảo hành", out _)
                    ? "Yêu cầu bảo hành"
                    : "WarrantyClaim";
                await SeedTableWithMappingAsync<WarrantyClaim>(workbook, warrantyClaimSheetName, "ClaimCode", "Id", (row, item) =>
                {
                    var coverageReference = row.GetString("WarrantyCoverageId") ?? "";
                    var serialReference = row.GetString("ProductSerialId") ?? "";
                    item.WarrantyCoverageId = _warrantyCoverageMap.GetValueOrDefault(coverageReference);
                    item.ProductSerialId = _productSerialMap.GetValueOrDefault(serialReference);
                    if (item.WarrantyCoverageId == 0 || item.ProductSerialId == 0)
                    {
                        throw new InvalidDataException("WarrantyClaim has an unresolved coverage or serial reference.");
                    }

                    var replacementSerialReference = row.GetString("ReplacementSerialId");
                    item.ReplacementSerialId = string.IsNullOrWhiteSpace(replacementSerialReference)
                        ? null
                        : _productSerialMap.GetValueOrDefault(replacementSerialReference);
                    if (!string.IsNullOrWhiteSpace(replacementSerialReference) && item.ReplacementSerialId == 0)
                    {
                        throw new InvalidDataException("WarrantyClaim has an unresolved replacement serial reference.");
                    }

                    var replacementStockOutReference = row.GetString("ReplacementStockOutId");
                    item.ReplacementStockOutId = string.IsNullOrWhiteSpace(replacementStockOutReference)
                        ? null
                        : _stockOutMap.GetValueOrDefault(replacementStockOutReference);
                    if (!string.IsNullOrWhiteSpace(replacementStockOutReference) && item.ReplacementStockOutId == 0)
                    {
                        throw new InvalidDataException("WarrantyClaim has an unresolved replacement stock-out reference.");
                    }

                    item.ClaimCode = row.GetString("ClaimCode") ?? "WRN";
                    item.ReceivedDate = row.GetDateTime("ReceivedDate") ?? DateTime.Now;
                    item.ProblemDescription = row.GetString("ProblemDescription");
                    item.TechnicalConclusion = row.GetString("TechnicalConclusion");
                    item.ManufacturerResult = row.GetString("ManufacturerResult");
                    item.RejectionReason = row.GetString("RejectionReason");
                    item.ProcessingNote = row.GetString("ProcessingNote");
                    item.ResolutionType = row.GetString("ResolutionType");
                    item.Status = row.GetString("Status") ?? "Pending";
                    item.ApprovedBy = row.GetDouble("ApprovedBy") is null ? null : 1;
                    item.ProcessedBy = 1;
                    item.ClosedDate = row.GetDateTime("ClosedDate");
                }, _warrantyClaimMap, log, cancellationToken);

            return log.ToString();
        }

        private static PreparedWorkbook PrepareWorkbook(
            byte[] workbookBytes,
            CancellationToken cancellationToken)
        {
            using var stream = new MemoryStream(workbookBytes, writable: false);
            using var workbook = new XLWorkbook(stream);
            // sheets chỉ nhận scalar; không mang workbook/cell của ClosedXML vào callback có thể chạy nhiều lần.
            var sheets = new Dictionary<string, PreparedSheet>(StringComparer.OrdinalIgnoreCase);

            foreach (var worksheet in workbook.Worksheets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var range = worksheet.RangeUsed()
                    ?? throw new InvalidDataException(
                        $"Sheet '{worksheet.Name}' does not contain a header row.");
                var headers = worksheet.Row(1).CellsUsed()
                    .Select(cell => cell.GetString().Trim())
                    .ToArray();
                if (headers.Length == 0 || headers.Any(string.IsNullOrWhiteSpace))
                {
                    throw new InvalidDataException(
                        $"Sheet '{worksheet.Name}' contains an empty header.");
                }

                if (headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Length)
                {
                    throw new InvalidDataException(
                        $"Sheet '{worksheet.Name}' contains duplicate headers.");
                }

                var headerColumns = worksheet.Row(1).CellsUsed()
                    .ToDictionary(
                        cell => cell.GetString().Trim(),
                        cell => cell.Address.ColumnNumber,
                        StringComparer.OrdinalIgnoreCase);
                var rows = new List<PreparedRow>();
                foreach (var row in range.RowsUsed().Skip(1))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var values = headerColumns.ToDictionary(
                        pair => pair.Key,
                        pair => PreparedCell.From(row.Cell(pair.Value)),
                        StringComparer.OrdinalIgnoreCase);
                    rows.Add(new PreparedRow(values));
                }

                sheets.Add(worksheet.Name, new PreparedSheet(rows));
            }

            var preparedWorkbook = new PreparedWorkbook(sheets);
            ValidatePreparedWorkbook(preparedWorkbook);
            return preparedWorkbook;
        }

        private static void ValidatePreparedWorkbook(PreparedWorkbook workbook)
        {
            if (!workbook.TryGetSheet("ProductUnit", out var productUnits))
            {
                return;
            }

            foreach (var row in productUnits.Rows)
            {
                if (string.IsNullOrWhiteSpace(row.GetString("ProductId")))
                {
                    throw new InvalidDataException(
                        "ProductUnit.ProductId is required.");
                }

                if (string.IsNullOrWhiteSpace(row.GetString("UnitId")))
                {
                    throw new InvalidDataException(
                        "ProductUnit.UnitId is required.");
                }

                var conversionFactor = row.GetDecimal("ConversionFactor") ?? 0;
                if (conversionFactor <= 0)
                {
                    throw new InvalidDataException(
                        "ProductUnit.ConversionFactor must be greater than zero.");
                }
            }
        }

        private static void ValidateWorkbook(byte[] workbookBytes)
        {
            using var stream = new MemoryStream(workbookBytes, writable: false);
            using var workbook = new XLWorkbook(stream);

            foreach (var worksheet in workbook.Worksheets)
            {
                if (worksheet.RangeUsed() is null)
                {
                    throw new InvalidDataException(
                        $"Sheet '{worksheet.Name}' không có hàng tiêu đề.");
                }

                var headers = worksheet.Row(1).CellsUsed()
                    .Select(cell => cell.GetString().Trim())
                    .ToArray();
                if (headers.Length == 0 || headers.Any(string.IsNullOrWhiteSpace))
                {
                    throw new InvalidDataException(
                        $"Sheet '{worksheet.Name}' có tiêu đề trống.");
                }

                if (headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Length)
                {
                    throw new InvalidDataException(
                        $"Sheet '{worksheet.Name}' có tiêu đề trùng.");
                }
            }
        }

        private void ResetMaps()
        {
            _unitMap.Clear();
            _categoryMap.Clear();
            _brandMap.Clear();
            _supplierMap.Clear();
            _customerMap.Clear();
            _productMap.Clear();
            _stockInMap.Clear();
            _stockOutMap.Clear();
            _purchaseInvoiceMap.Clear();
            _salesInvoiceMap.Clear();
            _productSerialMap.Clear();
            _warrantyCoverageMap.Clear();
            _warrantyClaimMap.Clear();
        }

        // cặp product-unit và đơn vị cơ sở được kiểm tra bằng hash set để bỏ qua dữ liệu đã tồn tại trong thời gian hằng số
        private async Task SeedProductUnitsAsync(
            PreparedWorkbook workbook,
            System.Text.StringBuilder log,
            CancellationToken cancellationToken)
        {
            if (!workbook.TryGetSheet("ProductUnit", out var sheet))
            {
                log.AppendLine("Bỏ qua sheet 'ProductUnit': Không tìm thấy.");
                return;
            }

            // chỉ đọc dữ liệu hiện có để lập bộ khóa; không cần EF theo dõi vì các dòng này không bị sửa
            var existingRows = await _context.ProductUnits
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            var existingPairs = existingRows
                .Select(row => (row.ProductId, row.UnitId))
                .ToHashSet();
            var baseUnitProducts = existingRows
                .Where(row => row.IsBaseUnit)
                .Select(row => row.ProductId)
                .ToHashSet();
            var inserted = 0;

            foreach (var wrapper in sheet.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
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

            await _context.SaveChangesAsync(cancellationToken);
            log.AppendLine($"Đã đồng bộ bảng 'ProductUnit': thêm {inserted} dòng.");
        }

        private async Task SeedWarrantyCoveragesAsync(
            PreparedWorkbook workbook,
            System.Text.StringBuilder log,
            CancellationToken cancellationToken)
        {
            if (!workbook.TryGetSheet("WarrantyCoverage", out var sheet))
            {
                log.AppendLine("Bỏ qua sheet 'WarrantyCoverage': Không tìm thấy.");
                return;
            }

            var existingByKey = (await _context.WarrantyCoverages
                    .ToListAsync(cancellationToken))
                .ToDictionary(CreateWarrantyCoverageKey);
            var inserted = 0;

            foreach (var row in sheet.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var serialReference = row.GetString("ProductSerialId") ?? "";
                var customerReference = row.GetString("CustomerId") ?? "";
                var productSerialId = _productSerialMap.GetValueOrDefault(serialReference);
                var customerId = _customerMap.GetValueOrDefault(customerReference);
                if (productSerialId == 0 || customerId == 0)
                {
                    throw new InvalidDataException("WarrantyCoverage has an unresolved serial or customer reference.");
                }

                var salesInvoiceReference = row.GetString("SalesInvoiceId");
                int? salesInvoiceId = string.IsNullOrWhiteSpace(salesInvoiceReference)
                    ? null
                    : _salesInvoiceMap.GetValueOrDefault(salesInvoiceReference);
                if (!string.IsNullOrWhiteSpace(salesInvoiceReference) && salesInvoiceId == 0)
                {
                    throw new InvalidDataException("WarrantyCoverage has an unresolved sales invoice reference.");
                }

                var startDate = row.GetDateTime("WarrantyStartDate") ?? DateTime.Now;
                var endDate = row.GetDateTime("WarrantyEndDate") ?? DateTime.Now;
                var coverageStatus = row.GetString("CoverageStatus") ?? "Active";
                var key = new WarrantyCoverageSeedKey(
                    productSerialId,
                    customerId,
                    salesInvoiceId,
                    NormalizeWarrantyDate(startDate),
                    NormalizeWarrantyDate(endDate),
                    coverageStatus.ToUpperInvariant());

                if (!existingByKey.TryGetValue(key, out var coverage))
                {
                    coverage = new WarrantyCoverage
                    {
                        ProductSerialId = productSerialId,
                        CustomerId = customerId,
                        SalesInvoiceId = salesInvoiceId,
                        WarrantyStartDate = startDate,
                        WarrantyEndDate = endDate,
                        CoverageStatus = coverageStatus
                    };
                    _context.WarrantyCoverages.Add(coverage);
                    await _context.SaveChangesAsync(cancellationToken);
                    existingByKey.Add(key, coverage);
                    inserted++;
                }

                var sourceId = row.GetString("Id");
                if (!string.IsNullOrWhiteSpace(sourceId))
                {
                    _warrantyCoverageMap[sourceId] = coverage.Id;
                }
            }

            log.AppendLine($"Đã đồng bộ bảng 'WarrantyCoverage': thêm {inserted} dòng.");
        }

        private static WarrantyCoverageSeedKey CreateWarrantyCoverageKey(WarrantyCoverage coverage) =>
            new(
                coverage.ProductSerialId,
                coverage.CustomerId,
                coverage.SalesInvoiceId,
                NormalizeWarrantyDate(coverage.WarrantyStartDate),
                NormalizeWarrantyDate(coverage.WarrantyEndDate),
                coverage.CoverageStatus.ToUpperInvariant());

        private static DateTime NormalizeWarrantyDate(DateTime value) =>
            value.AddTicks(-(value.Ticks % TimeSpan.TicksPerSecond));

        private readonly record struct WarrantyCoverageSeedKey(
            int ProductSerialId,
            int CustomerId,
            int? SalesInvoiceId,
            DateTime WarrantyStartDate,
            DateTime WarrantyEndDate,
            string CoverageStatus);
        private static bool IsTrue(string? value) =>
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";

        // hàm chung dùng mã nghiệp vụ để upsert nhẹ: có rồi thì chỉ dựng map id, chưa có mới gọi mapAction và insert
        private async Task SeedTableWithMappingAsync<T>(
            PreparedWorkbook workbook,
            string sheetName,
            string codeHeader,
            string idHeader,
            Action<PreparedRow, T> mapAction,
            Dictionary<string, int> idMap,
            System.Text.StringBuilder log,
            CancellationToken cancellationToken)
            where T : class, new()
        {
            if (!workbook.TryGetSheet(sheetName, out var sheet))
            {
                log.AppendLine($"B\u1ECF qua sheet '{sheetName}': Kh\u00F4ng t\u00ECm th\u1EA5y.");
                return;
            }

            // nạp cả dữ liệu cũ để mã trong excel vẫn resolve được dù bản ghi đã có từ lần seed trước
            var existingItems = await _context.Set<T>().ToListAsync(cancellationToken);
            var codeProp = typeof(T).GetProperty(codeHeader.Contains("Code") ? codeHeader : (typeof(T).Name + "Code"));
            if (codeProp == null) codeProp = typeof(T).GetProperty("DocumentCode") ?? typeof(T).GetProperty("DisplayName");

            foreach (var wrapper in sheet.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
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
                    await _context.SaveChangesAsync(cancellationToken);
                    
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

        // chuẩn hóa xuất xứ về một cách viết thống nhất; bảng từ điển xử lý trường hợp rõ ràng trước, heuristic chỉ là đường lui
        private string? TranslateOrigin(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            string normalized = input.Trim();
            
            // các tên tiếng anh và biến thể viết hoa thường gặp được đổi sang tên hiển thị tiếng việt
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

            // nhận diện một số chuỗi bị lỗi dấu từ workbook cũ khi không khớp từ điển
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

            // không nhận diện được thì chỉ chuẩn hóa chữ đầu, không tự đoán quốc gia khác
            if (normalized.Length > 0)
            {
                return char.ToUpper(normalized[0]) + normalized.Substring(1).ToLower();
            }

            return normalized;
        }

        // wrapper giấu việc tìm chỉ số cột và trả null cho ô trống, giúp các hàm map tập trung vào nghiệp vụ
        private sealed class PreparedWorkbook(
            IReadOnlyDictionary<string, PreparedSheet> sheets)
        {
            public bool TryGetSheet(string name, out PreparedSheet sheet) =>
                sheets.TryGetValue(name, out sheet!);
        }

        private sealed class PreparedSheet(IReadOnlyList<PreparedRow> rows)
        {
            public IReadOnlyList<PreparedRow> Rows { get; } = rows;
        }

        private sealed class PreparedRow(
            IReadOnlyDictionary<string, PreparedCell> cells)
        {
            public string? GetString(string header) =>
                cells.TryGetValue(header, out var cell) ? cell.Text : null;

            public decimal? GetDecimal(string header) =>
                cells.TryGetValue(header, out var cell) ? cell.Decimal : null;

            public double? GetDouble(string header) =>
                cells.TryGetValue(header, out var cell) ? cell.Double : null;

            public DateTime? GetDateTime(string header) =>
                cells.TryGetValue(header, out var cell) ? cell.DateTime : null;
        }

        private readonly record struct PreparedCell(
            string? Text,
            decimal? Decimal,
            double? Double,
            DateTime? DateTime)
        {
            public static PreparedCell From(IXLCell cell)
            {
                var rawText = cell.Value.ToString();
                var text = string.IsNullOrWhiteSpace(rawText) ? null : rawText.Trim();
                decimal? decimalValue = decimal.TryParse(text, out var parsedDecimal)
                    ? parsedDecimal
                    : null;
                double? doubleValue = double.TryParse(text, out var parsedDouble)
                    ? parsedDouble
                    : null;
                DateTime? dateTimeValue = cell.TryGetValue(out DateTime parsedDateTime)
                    ? parsedDateTime
                    : null;
                return new PreparedCell(text, decimalValue, doubleValue, dateTimeValue);
            }
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
