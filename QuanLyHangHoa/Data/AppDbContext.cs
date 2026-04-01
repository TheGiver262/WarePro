using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Models;
using System;

namespace QuanLyHangHoa.Data
{
    // AppDbContext dùng để giao tiếp với file SQL Server (LocalDB)
    public class AppDbContext : DbContext
    {
        public DbSet<Models.Product> Products { get; set; }
        public DbSet<Models.Employee> Employees { get; set; }
        public DbSet<Models.Invoice> Invoices { get; set; }
        public DbSet<Models.InvoiceDetail> InvoiceDetails { get; set; }
        public DbSet<Models.ImportReceipt> ImportReceipts { get; set; }
        public DbSet<Models.ImportReceiptDetail> ImportReceiptDetails { get; set; }
        public DbSet<Models.WarrantyTicket> WarrantyTickets { get; set; }
        public DbSet<Models.WarrantyTicketDetail> WarrantyTicketDetails { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Sử dụng SQLite thay cho SQL Server LocalDB để không cần cài đặt thêm phần mềm
            string dbPath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "QuanLyHangHoa.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ==========================================
            // SEED DATA: Tự động nạp dữ liệu mẫu ban đầu
            // ==========================================

            // 1. Phân quyền và Nhân viên (5 phần tử mẫu)
            modelBuilder.Entity<Models.Employee>().HasData(
                new Models.Employee() { Id = 1, Username = "admin", PasswordHash = "admin", Role = "Admin", FullName = "Quản trị viên Hệ thống", DateOfBirth = new DateTime(1990, 1, 1), Position = "Giám Đốc Cửa Hàng" },
                new Models.Employee() { Id = 2, Username = "staff1", PasswordHash = "staff1", Role = "Staff", FullName = "Nguyễn Văn Thu Ngân", DateOfBirth = new DateTime(1995, 2, 2), Position = "Thu Ngân" },
                new Models.Employee() { Id = 3, Username = "staff2", PasswordHash = "staff2", Role = "Staff", FullName = "Trần Thị Kiểm Kho", DateOfBirth = new DateTime(1996, 3, 3), Position = "Thủ Kho" },
                new Models.Employee() { Id = 4, Username = "staff3", PasswordHash = "staff3", Role = "Staff", FullName = "Lê Bảo Hành", DateOfBirth = new DateTime(1997, 4, 4), Position = "Nhân viên Bảo hành" },
                new Models.Employee() { Id = 5, Username = "staff4", PasswordHash = "staff4", Role = "Staff", FullName = "Phạm Sale", DateOfBirth = new DateTime(1998, 5, 5), Position = "Nhân viên Part-time" }
            );

            // 2. Hàng hoá mẫu (5 phần tử mẫu - có sẵn số lượng tồn kho đầu kỳ)
            modelBuilder.Entity<Models.Product>().HasData(
                new Models.Product() { Id = 1, Name = "Laptop Dell XPS 15", Category = "Máy tính xách tay", Quantity = 20, UnitPrice = 35000000m, Origin = "Mỹ", WarrantyMonths = 24, Notes = "Hàng đắt tiền, cấu hình cao" },
                new Models.Product() { Id = 2, Name = "Chuột Logitech G502", Category = "Linh kiện & Phụ kiện", Quantity = 150, UnitPrice = 1200000m, Origin = "Trung Quốc", WarrantyMonths = 12, Notes = "Chuột gaming siêu nhạy" },
                new Models.Product() { Id = 3, Name = "Bàn phím cơ Filco Majestouch", Category = "Linh kiện & Phụ kiện", Quantity = 35, UnitPrice = 3200000m, Origin = "Nhật Bản", WarrantyMonths = 60, Notes = "Chuyên dụng cho Lập trình viên" },
                new Models.Product() { Id = 4, Name = "Màn hình Dell UltraSharp 27", Category = "Màn hình", Quantity = 50, UnitPrice = 9500000m, Origin = "Mỹ", WarrantyMonths = 36, Notes = "Đồ hoạ cực đỉnh" },
                new Models.Product() { Id = 5, Name = "Tai nghe kiểm âm Sony MDR-7506", Category = "Âm thanh", Quantity = 45, UnitPrice = 2800000m, Origin = "Nhật Bản", WarrantyMonths = 12, Notes = "Tai nghe studio chuẩn" }
            );

            // Invoice & ImportReceipt thường ko seed vì Id auto-generate rườm rà. Nhân viên nạp 2 bảng phụ này bằng cách bán/nhập.
            base.OnModelCreating(modelBuilder);
        }
    }
}
