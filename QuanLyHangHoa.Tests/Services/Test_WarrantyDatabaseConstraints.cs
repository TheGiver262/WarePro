using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Inventory;
using Xunit;

namespace QuanLyHangHoa.Tests.Services
{
    [Trait("Category", "RealDatabase")]
    public class Test_WarrantyDatabaseConstraints
    {
        [Fact]
        public void AddTenWarrantyRecords_To_RealDatabase()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer("Server=.\\SQLEXPRESS;Database=ProductManagementDb;Trusted_Connection=True;TrustServerCertificate=True;")
                .Options;

            using (var db = new AppDbContext(options))
            {
                // Dọn dẹp dữ liệu cũ từ các lần chạy trước để tránh trùng lặp khoá chính
                var oldClaims = db.WarrantyClaims.Where(c => c.ClaimCode.StartsWith("WC-TEST-")).ToList();
                if (oldClaims.Any()) db.WarrantyClaims.RemoveRange(oldClaims);

                var oldCoverages = db.WarrantyCoverages.Where(c => c.ProductSerial.SerialNumber.StartsWith("SN-TEST-W-")).ToList();
                if (oldCoverages.Any()) db.WarrantyCoverages.RemoveRange(oldCoverages);

                var oldSerials = db.ProductSerials.Where(s => s.SerialNumber.StartsWith("SN-TEST-W-")).ToList();
                if (oldSerials.Any()) db.ProductSerials.RemoveRange(oldSerials);
                try
                {
                    db.SaveChanges();
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Bỏ qua nếu test chạy song song khác đã xóa trước
                }

                // 1. Tìm hoặc tạo khách hàng mẫu
                var customer = db.Customers.FirstOrDefault(c => c.CustomerCode != "CUS-ADJ");
                if (customer == null)
                {
                    customer = new Customer
                    {
                        CustomerCode = "CUS-TEST-W",
                        DisplayName = "Khách Hàng Bảo Hành Test",
                        Phone = "0987654321",
                        Email = "test.warranty@gmail.com",
                        Address = "Hà Nội",
                        IsActive = true
                    };
                    db.Customers.Add(customer);
                    db.SaveChanges();
                }

                // 2. Tìm hoặc tạo sản phẩm có quản lý serial
                var product = db.Products.FirstOrDefault(p => p.IsSerialTracked);
                if (product == null)
                {
                    product = new Product
                    {
                        ProductCode = "SP-TEST-W",
                        DisplayName = "Sản Phẩm Test Bảo Hành",
                        CategoryId = db.Categories.First().Id,
                        BrandId = db.Brands.First().Id,
                        DefaultUnitId = db.Units.First().Id,
                        DefaultPrice = 500000m,
                        IsSerialTracked = true,
                        IsActive = true
                    };
                    db.Products.Add(product);
                    db.SaveChanges();
                }

                // 3. Tạo 10 ProductSerial mới để gán quyền bảo hành
                var random = new Random();
                var serialList = new List<ProductSerial>();
                var firstStockInLineId = db.StockInLines.Select(s => s.Id).FirstOrDefault();
                if (firstStockInLineId == 0)
                {
                    var stockIn = new StockIn
                    {
                        DocumentCode = $"SI-TEMP-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}",
                        ImportDate = DateTime.Now,
                        Status = "Posted",
                        CreatedBy = 1,
                        WarehouseId = db.Warehouses.Select(w => w.Id).First(),
                        PurposeCode = "IM-PURCHASE"
                    };
                    db.StockIns.Add(stockIn);
                    db.SaveChanges();

                    var line = new StockInLine
                    {
                        StockInId = stockIn.Id,
                        ProductId = product.Id,
                        Quantity = 10,
                        BaseQuantity = 10,
                        UnitPrice = 100000m,
                        UnitId = product.DefaultUnitId
                    };
                    db.StockInLines.Add(line);
                    db.SaveChanges();
                    firstStockInLineId = line.Id;
                }
                
                var firstWarehouseId = db.Warehouses.Select(w => w.Id).First();
                for (int i = 1; i <= 10; i++)
                {
                    string uniqueSerial = $"SN-TEST-W-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
                    var serial = new ProductSerial
                    {
                        ProductId = product.Id,
                        SerialNumber = uniqueSerial,
                        CurrentStatus = SerialStatus.Sold.ToString(),
                        LastStockInLineId = firstStockInLineId,
                        CurrentWarehouseId = firstWarehouseId
                    };
                    db.ProductSerials.Add(serial);
                    serialList.Add(serial);
                }
                db.SaveChanges();

                // 4. Tạo 10 WarrantyCoverage (Quyền bảo hành) tương ứng
                var coverageList = new List<WarrantyCoverage>();
                for (int i = 0; i < 10; i++)
                {
                    var coverage = new WarrantyCoverage
                    {
                        ProductSerialId = serialList[i].Id,
                        CustomerId = customer.Id,
                        WarrantyStartDate = DateTime.Today.AddMonths(-3),
                        WarrantyEndDate = DateTime.Today.AddMonths(9),
                        CoverageStatus = i % 8 == 0 ? "Expired" : (i % 8 == 1 ? "Voided" : "Active")
                    };
                    db.WarrantyCoverages.Add(coverage);
                    coverageList.Add(coverage);
                }
                db.SaveChanges();

                // 5. Tạo 10 WarrantyClaim (Yêu cầu bảo hành) tương ứng
                var statuses = new string[] { "Open", "ManufacturerWait", "Ready", "Closed", "Rejected" };
                var problems = new string[] 
                { 
                    "Màn hình bị sọc kẻ xanh dọc", 
                    "Pin sạc không vào điện", 
                    "Loa rè khi mở âm lượng lớn", 
                    "Nút nguồn bị liệt không phản hồi", 
                    "Không bắt được sóng Wifi", 
                    "Camera sau bị mờ, không lấy nét", 
                    "Hỏng mainboard do nóng máy quá mức", 
                    "Cảm ứng màn hình thỉnh thoảng đơ", 
                    "Cổng USB-C lỏng lẻo không nhận kết nối", 
                    "Sập nguồn đột ngột khi pin còn 30%" 
                };

                var conclusions = new string[]
                {
                    "Kiểm tra có sọc màn hình, cần thay panel mới",
                    "Pin chai hỏng, cần đổi mới",
                    "Màng loa rách nhẹ, đã dán lại thành công",
                    "Nút nguồn bẩn tiếp điểm, đã vệ sinh",
                    "Card wifi lỏng chân cắm, đã cắm lại",
                    "Camera có hạt bụi, đã lau sạch",
                    "Chập tụ nguồn trên mainboard",
                    "Cáp màn hình lỏng, đã cố định lại",
                    "Chân cổng sạc bị mòn tiếp điểm",
                    "Mạch quản lý pin hỏng chip điều khiển"
                };

                for (int i = 0; i < 10; i++)
                {
                    string claimCode = $"WC-TEST-{DateTime.Now:yyyyMMdd}-{i:D3}";
                    var status = statuses[i % statuses.Length];
                    
                    var claim = new WarrantyClaim
                    {
                        ClaimCode = claimCode,
                        WarrantyCoverageId = coverageList[i].Id,
                        ProductSerialId = serialList[i].Id,
                        ReceivedDate = DateTime.Today.AddDays(-10 + i),
                        ProblemDescription = problems[i],
                        TechnicalConclusion = i % 2 == 0 ? conclusions[i] : null,
                        Status = status,
                        ProcessedBy = 1, // ID user mặc định là 1 (thường là Admin)
                        ExpectedReturnDate = DateTime.Today.AddDays(5 + i),
                        RejectionReason = status == "Rejected" ? "Sản phẩm bị vô nước hoặc có dấu hiệu tự ý cạy mở" : null
                    };

                    // Cập nhật trạng thái ProductSerial tương ứng nếu đang trong quá trình bảo hành
                    if (status == "Open" || status == "ManufacturerWait")
                    {
                        serialList[i].CurrentStatus = SerialStatus.InWarrantyProcess.ToString();
                        db.ProductSerials.Update(serialList[i]);
                    }

                    db.WarrantyClaims.Add(claim);
                }
                db.SaveChanges();
            }
        }
    }
}
