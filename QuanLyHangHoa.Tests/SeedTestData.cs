using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using Xunit;
using Xunit.Abstractions;

namespace QuanLyHangHoa.Tests
{
    [Trait("Category", "RealDatabase")]
    public class SeedTestData
    {
        private readonly ITestOutputHelper _output;

        public SeedTestData(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void SeedInvoicesAndStockDocuments()
        {
            _output.WriteLine("Bắt đầu seeding dữ liệu kiểm thử...");

            using var db = new AppDbContext();

            // Dọn dẹp dữ liệu seeding cũ để bảo đảm tính idempotent
            _output.WriteLine("Đang dọn dẹp dữ liệu seeding cũ...");
            var oldSalesInvoices = db.SalesInvoices.Where(i => i.InvoiceCode.StartsWith("HDB-SEED-") || i.InvoiceCode.StartsWith("HDB-INIT")).ToList();
            var oldPurchaseInvoices = db.PurchaseInvoices.Where(i => i.InvoiceCode.StartsWith("HDN-SEED-") || i.InvoiceCode.StartsWith("HDN-INIT")).ToList();
            var oldStockOuts = db.StockOuts.Where(s => s.DocumentCode.StartsWith("SO-SEED-") || s.DocumentCode.StartsWith("SO-INIT")).ToList();
            var oldStockIns = db.StockIns.Where(s => s.DocumentCode.StartsWith("SI-SEED-") || s.DocumentCode.StartsWith("SI-INIT")).ToList();
            var oldSerials = db.ProductSerials.Where(s => s.SerialNumber.StartsWith("SR-NEW-IN-") || s.SerialNumber.StartsWith("SR-INIT-")).ToList();

            // Xóa WarrantyCoverages liên quan
            var oldCoverages = db.WarrantyCoverages
                .Where(c => oldSerials.Select(s => s.Id).Contains(c.ProductSerialId) || oldSalesInvoices.Select(i => i.Id).Contains(c.SalesInvoiceId.GetValueOrDefault()))
                .ToList();
            var oldCoverageIds = oldCoverages.Select(c => c.Id).ToList();
            var oldSerialIds = oldSerials.Select(s => s.Id).ToList();
            var oldWarrantyClaims = db.WarrantyClaims
                .Where(c => oldCoverageIds.Contains(c.WarrantyCoverageId) || oldSerialIds.Contains(c.ProductSerialId))
                .ToList();
            if (oldWarrantyClaims.Any()) db.WarrantyClaims.RemoveRange(oldWarrantyClaims);
            if (oldCoverages.Any()) db.WarrantyCoverages.RemoveRange(oldCoverages);

            // Xóa StockLedgers liên quan
            var oldLedgers = db.StockLedgers.Where(l => oldSerials.Select(s => s.Id).Contains(l.ProductSerialId ?? 0)).ToList();
            if (oldLedgers.Any()) db.StockLedgers.RemoveRange(oldLedgers);

            // Xóa Invoice Lines & Invoices
            foreach (var inv in oldSalesInvoices)
            {
                var lines = db.SalesInvoiceLines.Where(l => l.SalesInvoiceId == inv.Id).ToList();
                db.SalesInvoiceLines.RemoveRange(lines);
            }
            db.SalesInvoices.RemoveRange(oldSalesInvoices);

            foreach (var inv in oldPurchaseInvoices)
            {
                var lines = db.PurchaseInvoiceLines.Where(l => l.PurchaseInvoiceId == inv.Id).ToList();
                db.PurchaseInvoiceLines.RemoveRange(lines);
            }
            db.PurchaseInvoices.RemoveRange(oldPurchaseInvoices);

            // Tìm và giải phóng LastStockOutLineId của các Serials cũ tham chiếu tới StockOutLines sắp bị xóa
            var oldStockOutLineIds = db.StockOutLines
                .Where(l => oldStockOuts.Select(so => so.Id).Contains(l.StockOutId))
                .Select(l => l.Id)
                .ToList();
            if (oldStockOutLineIds.Any())
            {
                var serialsToReset = db.ProductSerials
                    .Where(s => s.LastStockOutLineId.HasValue && oldStockOutLineIds.Contains(s.LastStockOutLineId.Value))
                    .ToList();
                foreach (var s in serialsToReset)
                {
                    s.LastStockOutLineId = null;
                    s.CurrentStatus = "InStock"; // Trả trạng thái về trong kho
                }
                db.SaveChanges();
            }

            // Xóa Serials
            if (oldSerials.Any()) db.ProductSerials.RemoveRange(oldSerials);

            // Xóa StockOut Lines & StockOut
            foreach (var so in oldStockOuts)
            {
                var lines = db.StockOutLines.Where(l => l.StockOutId == so.Id).ToList();
                db.StockOutLines.RemoveRange(lines);
            }
            db.StockOuts.RemoveRange(oldStockOuts);

            // Xóa StockIn Lines & StockIn
            foreach (var si in oldStockIns)
            {
                var lines = db.StockInLines.Where(l => l.StockInId == si.Id).ToList();
                db.StockInLines.RemoveRange(lines);
            }
            db.StockIns.RemoveRange(oldStockIns);

            db.SaveChanges();
            _output.WriteLine("Đã dọn dẹp sạch sẽ dữ liệu seeding cũ.");

            // 1. Kiểm tra hoặc tạo các dữ liệu cơ bản
            var warehouse = db.Warehouses.FirstOrDefault(w => w.IsActive) 
                            ?? new Warehouse { WarehouseCode = "WH-SEED", DisplayName = "Kho Seeding Mới", IsActive = true, IsDefault = true };
            if (warehouse.Id == 0)
            {
                db.Warehouses.Add(warehouse);
                db.SaveChanges();
            }

            var supplier = db.Suppliers.FirstOrDefault(s => s.IsActive)
                           ?? new Supplier { SupplierCode = "SUP-SEED", DisplayName = "Nhà cung cấp Seeding", IsActive = true };
            if (supplier.Id == 0)
            {
                db.Suppliers.Add(supplier);
                db.SaveChanges();
            }

            var customer = db.Customers.FirstOrDefault(c => c.IsActive)
                           ?? new Customer { CustomerCode = "CUS-SEED", DisplayName = "Khách hàng Seeding", IsActive = true };
            if (customer.Id == 0)
            {
                db.Customers.Add(customer);
                db.SaveChanges();
            }

            var user = db.AppUsers.FirstOrDefault(u => u.IsActive)
                       ?? new AppUser { Username = "admin-seed", FullName = "Admin Seeding", RoleCode = "ADMIN", IsActive = true };
            if (user.Id == 0)
            {
                db.AppUsers.Add(user);
                db.SaveChanges();
            }

            var products = db.Products.Include(p => p.Category).Include(p => p.Brand).Where(p => p.IsActive).ToList();
            if (products.Count < 5)
            {
                _output.WriteLine("Số lượng sản phẩm trong DB quá ít để test seeding. Vui lòng kiểm tra lại CSDL.");
                return;
            }

            var serialProducts = products.Where(p => p.IsSerialTracked).ToList();
            var nonSerialProducts = products.Where(p => !p.IsSerialTracked).ToList();

            var unitId = db.Units.Select(u => u.Id).FirstOrDefault();
            if (unitId == 0)
            {
                var newUnit = new Unit { UnitCode = "CAI", DisplayName = "Cái", IsActive = true };
                db.Units.Add(newUnit);
                db.SaveChanges();
                unitId = newUnit.Id;
            }

            var stockInService = new StockInService(() => new AppDbContext());
            var stockOutService = new StockOutService(() => new AppDbContext());
            var invoiceService = new InvoiceService(() => new AppDbContext());

            var random = new Random();

            // --- KHỞI TẠO TỒN KHO BAN ĐẦU CHO TẤT CẢ SẢN PHẨM (01/06/2026) ---
            _output.WriteLine("Đang tạo phiếu nhập khởi tạo tồn kho dồi dào cho tất cả sản phẩm...");
            {
                var initImportDate = new DateTime(2026, 6, 1, 8, 0, 0);
                var initStockIn = new StockIn
                {
                    DocumentCode = "SI-INIT",
                    WarehouseId = warehouse.Id,
                    SupplierId = supplier.Id,
                    PurposeCode = "Purchase",
                    ImportDate = initImportDate,
                    Notes = "Phiếu nhập khởi tạo tồn kho dồi dào cho Seeding",
                    Status = "Draft",
                    CreatedAt = initImportDate,
                    CreatedBy = user.Id
                };

                var initStockInLines = new List<StockInLine>();
                int serialCounter = 1;
                foreach (var prod in products)
                {
                    int qty = prod.IsSerialTracked ? 30 : 100; // Nhập 30 serials hoặc 100 hàng không serial
                    decimal price = prod.CostPrice ?? (prod.DefaultPrice > 0 ? prod.DefaultPrice * 0.7m : 100000m);

                    var line = new StockInLine
                    {
                        ProductId = prod.Id,
                        Quantity = qty,
                        BaseQuantity = qty,
                        UnitPrice = price,
                        UnitId = prod.DefaultUnitId > 0 ? prod.DefaultUnitId : unitId
                    };

                    if (prod.IsSerialTracked)
                    {
                        line.ProductSerials = new List<ProductSerial>();
                        for (int s = 1; s <= qty; s++)
                        {
                            string sn = $"SR-INIT-{prod.Id}-{serialCounter++:D4}";
                            line.ProductSerials.Add(new ProductSerial
                            {
                                SerialNumber = sn,
                                ProductId = prod.Id,
                                CurrentStatus = "InStock"
                            });
                        }
                    }

                    initStockInLines.Add(line);
                }

                stockInService.SaveDraft(initStockIn, initStockInLines, user.Id);
                stockInService.Post(initStockIn.Id, user.Id);

                // Tạo hóa đơn nhập tương ứng
                var initPurchaseInvoice = new PurchaseInvoice
                {
                    InvoiceCode = "HDN-INIT",
                    SupplierId = supplier.Id,
                    StockInId = initStockIn.Id,
                    InvoiceDate = initImportDate,
                    Notes = "Hóa đơn mua khởi tạo tồn kho dồi dào cho Seeding",
                    CreatedBy = user.Id,
                    CreatedAt = initImportDate,
                    Lines = initStockInLines.Select(l => new PurchaseInvoiceLine
                    {
                        ProductId = l.ProductId,
                        UnitId = l.UnitId,
                        StockInLineId = l.Id,
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice,
                        TaxRate = 0.10m
                    }).ToList()
                };

                decimal subTotal = initStockInLines.Sum(l => l.Quantity * l.UnitPrice);
                decimal taxAmount = subTotal * 0.10m;
                initPurchaseInvoice.PaidAmount = subTotal + taxAmount;

                invoiceService.SavePurchaseInvoice(initPurchaseInvoice, user.Id);
                _output.WriteLine("Khởi tạo tồn kho dồi dào thành công!");
            }

            // --- A. SEEDING 20 PHIẾU NHẬP & HÓA ĐƠN NHẬP (TỪ 01/06 ĐẾN 06/06) ---
            _output.WriteLine("Đang sinh 20 phiếu nhập & hóa đơn nhập ngẫu nhiên...");
            for (int i = 1; i <= 20; i++)
            {
                // Ngày nhập ngẫu nhiên từ 01/06/2026 đến 06/06/2026
                int randomDay = random.Next(1, 7); // 1, 2, 3, 4, 5, 6
                var importDate = new DateTime(2026, 6, randomDay, random.Next(8, 18), random.Next(0, 60), 0);

                var stockIn = new StockIn
                {
                    DocumentCode = $"SI-SEED-{i:D3}",
                    WarehouseId = warehouse.Id,
                    SupplierId = supplier.Id,
                    PurposeCode = "Purchase",
                    ImportDate = importDate,
                    Notes = $"Phiếu nhập tự động seed dòng thứ {i}",
                    Status = "Draft",
                    CreatedAt = importDate,
                    CreatedBy = user.Id
                };

                // Chọn ngẫu nhiên 2 sản phẩm có serial và 2 sản phẩm không serial để nhập
                var selectedProductsToImport = new List<Product>();
                if (serialProducts.Any())
                {
                    selectedProductsToImport.AddRange(serialProducts.OrderBy(x => random.Next()).Take(2));
                }
                if (nonSerialProducts.Any())
                {
                    selectedProductsToImport.AddRange(nonSerialProducts.OrderBy(x => random.Next()).Take(2));
                }

                var stockInLines = new List<StockInLine>();
                foreach (var prod in selectedProductsToImport)
                {
                    int qty = random.Next(3, 8); // Nhập số lượng từ 3 đến 7
                    decimal price = prod.CostPrice ?? (prod.DefaultPrice > 0 ? prod.DefaultPrice * 0.7m : 100000m);
                    
                    var line = new StockInLine
                    {
                        ProductId = prod.Id,
                        Quantity = qty,
                        BaseQuantity = qty,
                        UnitPrice = price,
                        UnitId = prod.DefaultUnitId > 0 ? prod.DefaultUnitId : unitId
                    };

                    if (prod.IsSerialTracked)
                    {
                        line.ProductSerials = new List<ProductSerial>();
                        for (int s = 1; s <= qty; s++)
                        {
                            string sn = $"SR-NEW-IN-{i:D3}-{prod.Id}-{s:D2}-{random.Next(1000, 9999)}";
                            line.ProductSerials.Add(new ProductSerial
                            {
                                SerialNumber = sn,
                                ProductId = prod.Id,
                                CurrentStatus = "InStock"
                            });
                        }
                    }

                    stockInLines.Add(line);
                }

                stockInService.SaveDraft(stockIn, stockInLines, user.Id);
                stockInService.Post(stockIn.Id, user.Id);

                // Tạo Hóa đơn nhập tương ứng
                var purchaseInvoice = new PurchaseInvoice
                {
                    InvoiceCode = $"HDN-SEED-{i:D3}",
                    SupplierId = supplier.Id,
                    StockInId = stockIn.Id,
                    InvoiceDate = importDate,
                    Notes = $"Hóa đơn mua tự động seed dòng thứ {i}",
                    CreatedBy = user.Id,
                    CreatedAt = importDate,
                    Lines = stockInLines.Select(l => new PurchaseInvoiceLine
                    {
                        ProductId = l.ProductId,
                        UnitId = l.UnitId,
                        StockInLineId = l.Id,
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice,
                        TaxRate = 0.10m
                    }).ToList()
                };

                decimal subTotal = stockInLines.Sum(l => l.Quantity * l.UnitPrice);
                decimal taxAmount = subTotal * 0.10m;
                purchaseInvoice.PaidAmount = subTotal + taxAmount;

                invoiceService.SavePurchaseInvoice(purchaseInvoice, user.Id);
                _output.WriteLine($"Đã seed thành công cặp Phiếu nhập {stockIn.DocumentCode} & Hóa đơn nhập {purchaseInvoice.InvoiceCode}");
            }

            // --- B. SEEDING 20 PHIẾU XUẤT & HÓA ĐƠN XUẤT (TỪ 07/06 ĐẾN 12/06) ---
            _output.WriteLine("Đang sinh 20 phiếu xuất & hóa đơn xuất ngẫu nhiên...");
            for (int i = 1; i <= 20; i++)
            {
                // Ngày xuất ngẫu nhiên từ 07/06/2026 đến 12/06/2026
                int randomDay = random.Next(7, 13); // 7, 8, 9, 10, 11, 12
                var exportDate = new DateTime(2026, 6, randomDay, random.Next(8, 18), random.Next(0, 60), 0);

                var stockOut = new StockOut
                {
                    DocumentCode = $"SO-SEED-{i:D3}",
                    CustomerId = customer.Id,
                    WarehouseId = warehouse.Id,
                    PurposeCode = "Sale",
                    ExportDate = exportDate,
                    Notes = $"Phiếu xuất tự động seed dòng thứ {i}",
                    Status = "Draft",
                    CreatedAt = exportDate,
                    CreatedBy = user.Id
                };

                // Chọn ngẫu nhiên 2 sản phẩm có serial và 2 sản phẩm không serial để xuất
                var selectedProductsToExport = new List<Product>();
                if (serialProducts.Any())
                {
                    selectedProductsToExport.AddRange(serialProducts.OrderBy(x => random.Next()).Take(2));
                }
                if (nonSerialProducts.Any())
                {
                    selectedProductsToExport.AddRange(nonSerialProducts.OrderBy(x => random.Next()).Take(2));
                }

                var stockOutLines = new List<StockOutLine>();
                foreach (var prod in selectedProductsToExport)
                {
                    decimal price = prod.DefaultPrice > 0 ? prod.DefaultPrice : 150000m;
                    int qty = random.Next(1, 3); // Xuất số lượng từ 1 đến 2

                    if (prod.IsSerialTracked)
                    {
                        // Lấy các serials đang có sẵn InStock trong kho
                        var availableSerials = stockOutService.GetInStockSerials(prod.Id, warehouse.Id);
                        if (availableSerials.Count >= qty)
                        {
                            var selectedSerials = availableSerials.OrderBy(x => random.Next()).Take(qty).ToList();
                            var line = new StockOutLine
                            {
                                ProductId = prod.Id,
                                Quantity = qty,
                                BaseQuantity = qty,
                                UnitPrice = price,
                                UnitId = prod.DefaultUnitId > 0 ? prod.DefaultUnitId : unitId,
                                ProductSerials = selectedSerials.Select(s => new ProductSerial
                                {
                                    SerialNumber = s.SerialNumber,
                                    ProductId = prod.Id
                                }).ToList()
                            };
                            stockOutLines.Add(line);
                        }
                        else
                        {
                            _output.WriteLine($"Bỏ qua xuất sản phẩm serial {prod.DisplayName} vì không đủ tồn kho serial ({availableSerials.Count} < {qty}).");
                        }
                    }
                    else
                    {
                        // Kiểm tra tồn kho khả dụng thực tế (AvailableQuantity)
                        using var checkDb = new AppDbContext();
                        var balance = checkDb.StockBalances.FirstOrDefault(b => b.ProductId == prod.Id && b.WarehouseId == warehouse.Id);
                        var availableQty = balance?.AvailableQuantity ?? 0;

                        if (availableQty >= qty)
                        {
                            var line = new StockOutLine
                            {
                                ProductId = prod.Id,
                                Quantity = qty,
                                BaseQuantity = qty,
                                UnitPrice = price,
                                UnitId = prod.DefaultUnitId > 0 ? prod.DefaultUnitId : unitId
                            };
                            stockOutLines.Add(line);
                        }
                        else
                        {
                            _output.WriteLine($"Bỏ qua xuất sản phẩm không serial {prod.DisplayName} vì không đủ tồn kho khả dụng ({availableQty} < {qty}).");
                        }
                    }
                }

                if (!stockOutLines.Any())
                {
                    _output.WriteLine($"Phiếu xuất SO-SEED-{i:D3} không có dòng nào khả dụng, bỏ qua cặp này.");
                    continue;
                }

                // Lưu Draft và Post để ghi sổ xuất hàng
                stockOutService.SaveDraft(stockOut, stockOutLines, user.Id);
                stockOutService.Post(stockOut.Id, user.Id);

                // Tạo Hóa đơn bán tương ứng
                var salesInvoice = new SalesInvoice
                {
                    InvoiceCode = $"HDB-SEED-{i:D3}",
                    CustomerId = customer.Id,
                    StockOutId = stockOut.Id,
                    InvoiceDate = exportDate,
                    Notes = $"Hóa đơn bán tự động seed dòng thứ {i}",
                    CreatedBy = user.Id,
                    CreatedAt = exportDate,
                    Lines = stockOutLines.Select(l => new SalesInvoiceLine
                    {
                        ProductId = l.ProductId,
                        UnitId = l.UnitId,
                        StockOutLineId = l.Id,
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice,
                        TaxRate = 0.10m
                    }).ToList()
                };

                decimal subTotal = stockOutLines.Sum(l => l.Quantity * l.UnitPrice);
                decimal taxAmount = subTotal * 0.10m;
                salesInvoice.PaidAmount = subTotal + taxAmount;

                invoiceService.SaveSalesInvoice(salesInvoice, user.Id);
                _output.WriteLine($"Đã seed thành công cặp Phiếu xuất {stockOut.DocumentCode} & Hóa đơn xuất {salesInvoice.InvoiceCode}");
            }

            _output.WriteLine("Đã hoàn tất seeding toàn bộ dữ liệu kiểm thử!");
        }

        [Fact]
        public async System.Threading.Tasks.Task VerifyDashboardDataUpdated()
        {
            _output.WriteLine("Bắt đầu xác minh dữ liệu Dashboard...");
            var dashboardService = new DashboardService(() => new AppDbContext());
            var stats = await dashboardService.GetStatsAsync();

            _output.WriteLine("===== KẾT QUẢ ĐỒNG BỘ DASHBOARD =====");
            _output.WriteLine($"Tổng sản phẩm tồn kho (cái): {stats.TotalInventoryCount}");
            _output.WriteLine($"Số lượng phiếu nhập trong tháng: {stats.StockInMonthCount}");
            _output.WriteLine($"Số lượng phiếu xuất trong tháng: {stats.StockOutMonthCount}");
            _output.WriteLine($"Doanh thu tháng này (VND): {stats.RevenueMonth:N0}");
            _output.WriteLine($"Doanh thu năm nay (VND): {stats.RevenueYear:N0}");
            _output.WriteLine($"Số lượng hoá đơn bán chưa thanh toán: {stats.UnpaidSalesInvoiceCount}");
            _output.WriteLine($"Số lượng hoá đơn mua chưa thanh toán: {stats.UnpaidPurchaseInvoiceCount}");
            _output.WriteLine($"Số lượng yêu cầu bảo hành đang hoạt động: {stats.WarrantyActiveCount}");

            _output.WriteLine("===== HOẠT ĐỘNG GẦN ĐÂY =====");
            foreach (var act in stats.Activities)
            {
                _output.WriteLine($"[{act.TimeAgo}] {act.Title}");
            }

            _output.WriteLine("===== DỮ LIỆU BIỂU ĐỒ DOANH THU & CHI PHÍ =====");
            foreach (var row in stats.RevenueExpenseChart)
            {
                _output.WriteLine($"Tháng: {row.Month} | Doanh thu: {row.Revenue:N0} VND | Chi phí: {row.Expense:N0} VND");
            }

            _output.WriteLine("===== DỮ LIỆU BIỂU ĐỒ TOP BÁN CHẠY =====");
            foreach (var prod in stats.TopSellingProductsChart)
            {
                _output.WriteLine($"Sản phẩm: {prod.ProductName} | Số lượng bán: {prod.TotalSold}");
            }

            _output.WriteLine("===== DỮ LIỆU XU HƯỚNG NHẬP XUẤT (7 ngày) =====");
            foreach (var trend in stats.StockMovementChart)
            {
                _output.WriteLine($"Ngày: {trend.Date} | Số phiếu nhập: {trend.StockInCount} | Số phiếu xuất: {trend.StockOutCount}");
            }

            Assert.True(stats.StockInMonthCount >= 20, "Số phiếu nhập trong tháng phải lớn hơn hoặc bằng 20.");
            Assert.True(stats.StockOutMonthCount >= 20, "Số phiếu xuất trong tháng phải lớn hơn hoặc bằng 20.");
        }

        [Fact]
        public void ExportDetailedProductsReport()
        {
            using var db = new AppDbContext();
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("# BÁO CÁO CHI TIẾT SẢN PHẨM TRONG TỪNG PHIẾU ĐÃ SEED");
            sb.AppendLine();
            sb.AppendLine("## I. PHIẾU NHẬP KHO (STOCK IN)");
            sb.AppendLine();

            var stockIns = db.StockIns
                .Include(s => s.Lines)
                    .ThenInclude(l => l.Product)
                .Where(s => s.DocumentCode.StartsWith("SI-SEED-"))
                .OrderBy(s => s.DocumentCode)
                .ToList();

            foreach (var si in stockIns)
            {
                sb.AppendLine($"### Phiếu nhập: {si.DocumentCode} - Ngày: {si.CreatedAt:dd/MM/yyyy HH:mm}");
                sb.AppendLine("| Tên sản phẩm | Số lượng | Đơn giá (VND) | Có Serial? |");
                sb.AppendLine("| :--- | :---: | :---: | :---: |");
                foreach (var line in si.Lines)
                {
                    var hasSerial = line.Product.IsSerialTracked ? "Có" : "Không";
                    sb.AppendLine($"| {line.Product.DisplayName} | {line.Quantity} | {line.UnitPrice:N0} | {hasSerial} |");
                }
                sb.AppendLine();
            }

            sb.AppendLine("## II. PHIẾU XUẤT KHO (STOCK OUT)");
            sb.AppendLine();

            var stockOuts = db.StockOuts
                .Include(s => s.Lines)
                    .ThenInclude(l => l.Product)
                .Where(s => s.DocumentCode.StartsWith("SO-SEED-"))
                .OrderBy(s => s.DocumentCode)
                .ToList();

            foreach (var so in stockOuts)
            {
                sb.AppendLine($"### Phiếu xuất: {so.DocumentCode} - Ngày: {so.CreatedAt:dd/MM/yyyy HH:mm}");
                sb.AppendLine("| Tên sản phẩm | Số lượng | Đơn giá (VND) | Có Serial? |");
                sb.AppendLine("| :--- | :---: | :---: | :---: |");
                foreach (var line in so.Lines)
                {
                    var hasSerial = line.Product.IsSerialTracked ? "Có" : "Không";
                    sb.AppendLine($"| {line.Product.DisplayName} | {line.Quantity} | {line.UnitPrice:N0} | {hasSerial} |");
                }
                sb.AppendLine();
            }

            System.IO.File.WriteAllText(
                System.IO.Path.Combine(AppContext.BaseDirectory, "Seeded_Detailed_Report.txt"),
                sb.ToString(),
                System.Text.Encoding.UTF8);
        }
    }
}
