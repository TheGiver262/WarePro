using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Models;
using System;

namespace QuanLyHangHoa.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext()
        {
        }

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        // Reference / Master Tables
        public DbSet<Unit> Units { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Brand> Brands { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<Customer> Customers { get; set; }

        // Core
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductSerial> ProductSerials { get; set; }
        public DbSet<Warehouse> Warehouses { get; set; }
        public DbSet<StockBalance> StockBalances { get; set; }
        public DbSet<StockLedger> StockLedgers { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<StockAdjustment> StockAdjustments { get; set; }
        public DbSet<StockAdjustmentLine> StockAdjustmentLines { get; set; }
        public DbSet<StockCountSession> StockCountSessions { get; set; }
        public DbSet<StockCountLine> StockCountLines { get; set; }

        // Stock In
        public DbSet<StockIn> StockIns { get; set; }
        public DbSet<StockInDetail> StockInDetails { get; set; }

        // Stock Out
        public DbSet<StockOut> StockOuts { get; set; }
        public DbSet<StockOutDetail> StockOutDetails { get; set; }

        // Invoices
        public DbSet<PurchaseInvoice> PurchaseInvoices { get; set; }
        public DbSet<PurchaseInvoiceLine> PurchaseInvoiceLines { get; set; }
        public DbSet<SalesInvoice> SalesInvoices { get; set; }
        public DbSet<SalesInvoiceLine> SalesInvoiceLines { get; set; }

        // Warranty
        public DbSet<Warranty> Warranties { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder.IsConfigured)
            {
                return;
            }

            string dbPath = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                "QuanLyHangHoa_v2.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ── Product → Category, Brand, Unit (restrict delete to avoid orphan)
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Brand)
                .WithMany(b => b.Products)
                .HasForeignKey(p => p.BrandId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Unit)
                .WithMany(u => u.Products)
                .HasForeignKey(p => p.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── ProductSerial → StockInDetail (nullable)
            modelBuilder.Entity<ProductSerial>()
                .HasOne(ps => ps.StockInDetail)
                .WithMany(sid => sid.ProductSerials)
                .HasForeignKey(ps => ps.StockInDetailId)
                .OnDelete(DeleteBehavior.SetNull);

            // ── ProductSerial → StockOutDetail (nullable)
            modelBuilder.Entity<ProductSerial>()
                .HasOne(ps => ps.StockOutDetail)
                .WithMany(sod => sod.ProductSerials)
                .HasForeignKey(ps => ps.StockOutDetailId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ProductSerial>()
                .HasOne(ps => ps.CurrentWarehouse)
                .WithMany(w => w.ProductSerials)
                .HasForeignKey(ps => ps.CurrentWarehouseId)
                .OnDelete(DeleteBehavior.SetNull);

            // ── Warranty ↔ ProductSerial (1-to-1)
            modelBuilder.Entity<Warranty>()
                .HasOne(w => w.ProductSerial)
                .WithOne(ps => ps.Warranty)
                .HasForeignKey<Warranty>(w => w.ProductSerialId)
                .OnDelete(DeleteBehavior.Cascade);

            // ── Unique index on ProductSerial.SerialNumber
            modelBuilder.Entity<ProductSerial>()
                .HasIndex(ps => ps.SerialNumber)
                .IsUnique();

            modelBuilder.Entity<Warehouse>()
                .HasIndex(w => w.Code)
                .IsUnique();

            modelBuilder.Entity<StockBalance>()
                .HasIndex(sb => new { sb.ProductId, sb.WarehouseId })
                .IsUnique();

            modelBuilder.Entity<StockBalance>()
                .HasOne(sb => sb.Product)
                .WithMany(p => p.StockBalances)
                .HasForeignKey(sb => sb.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockBalance>()
                .HasOne(sb => sb.Warehouse)
                .WithMany(w => w.StockBalances)
                .HasForeignKey(sb => sb.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockLedger>()
                .HasOne(sl => sl.Product)
                .WithMany(p => p.StockLedgers)
                .HasForeignKey(sl => sl.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockLedger>()
                .HasOne(sl => sl.Warehouse)
                .WithMany()
                .HasForeignKey(sl => sl.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── StockIn → Employee (restrict)
            modelBuilder.Entity<StockAdjustment>()
                .HasIndex(sa => sa.DocumentCode)
                .IsUnique();

            modelBuilder.Entity<StockAdjustment>()
                .HasOne(sa => sa.Warehouse)
                .WithMany()
                .HasForeignKey(sa => sa.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockAdjustmentLine>()
                .HasOne(line => line.StockAdjustment)
                .WithMany(adjustment => adjustment.Lines)
                .HasForeignKey(line => line.StockAdjustmentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StockAdjustmentLine>()
                .HasOne(line => line.Product)
                .WithMany()
                .HasForeignKey(line => line.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockAdjustmentLine>()
                .HasOne(line => line.ProductSerial)
                .WithMany()
                .HasForeignKey(line => line.ProductSerialId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<StockCountSession>()
                .HasIndex(session => session.SessionCode)
                .IsUnique();

            modelBuilder.Entity<StockCountSession>()
                .HasOne(session => session.Warehouse)
                .WithMany()
                .HasForeignKey(session => session.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockCountLine>()
                .HasOne(line => line.StockCountSession)
                .WithMany(session => session.Lines)
                .HasForeignKey(line => line.StockCountSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StockCountLine>()
                .HasOne(line => line.Product)
                .WithMany()
                .HasForeignKey(line => line.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockIn>()
                .HasOne(s => s.Employee)
                .WithMany(e => e.StockIns)
                .HasForeignKey(s => s.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── StockOut → Employee (restrict)
            modelBuilder.Entity<StockOut>()
                .HasOne(s => s.Employee)
                .WithMany(e => e.StockOuts)
                .HasForeignKey(s => s.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            // ──────────────────────────────────────────────────────────────────
            // SEED DATA
            // ──────────────────────────────────────────────────────────────────

            modelBuilder.Entity<PurchaseInvoice>()
                .HasIndex(invoice => invoice.InvoiceCode)
                .IsUnique();

            modelBuilder.Entity<PurchaseInvoice>()
                .HasOne(invoice => invoice.Supplier)
                .WithMany()
                .HasForeignKey(invoice => invoice.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PurchaseInvoice>()
                .HasOne(invoice => invoice.StockIn)
                .WithMany()
                .HasForeignKey(invoice => invoice.StockInId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<PurchaseInvoiceLine>()
                .HasOne(line => line.PurchaseInvoice)
                .WithMany(invoice => invoice.Lines)
                .HasForeignKey(line => line.PurchaseInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PurchaseInvoiceLine>()
                .HasOne(line => line.Product)
                .WithMany()
                .HasForeignKey(line => line.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PurchaseInvoiceLine>()
                .HasOne(line => line.Unit)
                .WithMany()
                .HasForeignKey(line => line.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PurchaseInvoiceLine>()
                .HasOne(line => line.StockInDetail)
                .WithMany()
                .HasForeignKey(line => line.StockInDetailId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<SalesInvoice>()
                .HasIndex(invoice => invoice.InvoiceCode)
                .IsUnique();

            modelBuilder.Entity<SalesInvoice>()
                .HasOne(invoice => invoice.Customer)
                .WithMany()
                .HasForeignKey(invoice => invoice.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SalesInvoice>()
                .HasOne(invoice => invoice.StockOut)
                .WithMany()
                .HasForeignKey(invoice => invoice.StockOutId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<SalesInvoiceLine>()
                .HasOne(line => line.SalesInvoice)
                .WithMany(invoice => invoice.Lines)
                .HasForeignKey(line => line.SalesInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SalesInvoiceLine>()
                .HasOne(line => line.Product)
                .WithMany()
                .HasForeignKey(line => line.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SalesInvoiceLine>()
                .HasOne(line => line.Unit)
                .WithMany()
                .HasForeignKey(line => line.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SalesInvoiceLine>()
                .HasOne(line => line.StockOutDetail)
                .WithMany()
                .HasForeignKey(line => line.StockOutDetailId)
                .OnDelete(DeleteBehavior.SetNull);

            // Units
            modelBuilder.Entity<Unit>().HasData(
                new Unit { Id = 1, Name = "Cái" },
                new Unit { Id = 2, Name = "Chiếc" },
                new Unit { Id = 3, Name = "Bộ" },
                new Unit { Id = 4, Name = "Hộp" }
            );

            // Categories
            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Máy tính xách tay" },
                new Category { Id = 2, Name = "Linh kiện & Phụ kiện" },
                new Category { Id = 3, Name = "Màn hình" },
                new Category { Id = 4, Name = "Âm thanh" }
            );

            // Brands
            modelBuilder.Entity<Brand>().HasData(
                new Brand { Id = 1, Name = "Dell" },
                new Brand { Id = 2, Name = "Logitech" },
                new Brand { Id = 3, Name = "Sony" },
                new Brand { Id = 4, Name = "Filco" }
            );

            // Suppliers
            modelBuilder.Entity<Supplier>().HasData(
                new Supplier { Id = 1, Name = "Công ty Dell Việt Nam", Address = "HCM", Phone = "028-1234567" },
                new Supplier { Id = 2, Name = "Phân Phối Logitech SEA", Address = "Hà Nội", Phone = "024-9876543" }
            );

            // Customers
            modelBuilder.Entity<Customer>().HasData(
                new Customer { Id = 1, Name = "Khách lẻ", Address = "", Phone = "" }
            );

            modelBuilder.Entity<Warehouse>().HasData(
                new Warehouse { Id = 1, Code = "MAIN", Name = "Main warehouse", IsDefault = true, IsActive = true }
            );

            // Employees
            modelBuilder.Entity<Employee>().HasData(
                new Employee { Id = 1, Username = "admin",  PasswordHash = "admin",  Role = "Admin", FullName = "Quản trị viên Hệ thống",    DateOfBirth = new DateTime(1990, 1, 1), Position = "Giám Đốc Cửa Hàng" },
                new Employee { Id = 2, Username = "staff1", PasswordHash = "staff1", Role = "Staff", FullName = "Nguyễn Văn Thu Ngân",         DateOfBirth = new DateTime(1995, 2, 2), Position = "Thu Ngân" },
                new Employee { Id = 3, Username = "staff2", PasswordHash = "staff2", Role = "Staff", FullName = "Trần Thị Kiểm Kho",           DateOfBirth = new DateTime(1996, 3, 3), Position = "Thủ Kho" },
                new Employee { Id = 4, Username = "staff3", PasswordHash = "staff3", Role = "Staff", FullName = "Lê Bảo Hành",                 DateOfBirth = new DateTime(1997, 4, 4), Position = "Nhân viên Bảo hành" },
                new Employee { Id = 5, Username = "staff4", PasswordHash = "staff4", Role = "Staff", FullName = "Phạm Sale",                   DateOfBirth = new DateTime(1998, 5, 5), Position = "Nhân viên Part-time" }
            );

            // Products
            modelBuilder.Entity<Product>().HasData(
                new Product { Id = 1, Name = "Laptop Dell XPS 15",          CategoryId = 1, BrandId = 1, UnitId = 1, Quantity = 20, UnitPrice = 35000000m, Origin = "Mỹ",         WarrantyMonths = 24, Notes = "Hàng đắt tiền, cấu hình cao" },
                new Product { Id = 2, Name = "Chuột Logitech G502",         CategoryId = 2, BrandId = 2, UnitId = 1, Quantity = 150, UnitPrice = 1200000m, Origin = "Trung Quốc",  WarrantyMonths = 12, Notes = "Chuột gaming siêu nhạy" },
                new Product { Id = 3, Name = "Bàn phím cơ Filco Majestouch",CategoryId = 2, BrandId = 4, UnitId = 1, Quantity = 35,  UnitPrice = 3200000m, Origin = "Nhật Bản",   WarrantyMonths = 60, Notes = "Chuyên dụng cho Lập trình viên" },
                new Product { Id = 4, Name = "Màn hình Dell UltraSharp 27", CategoryId = 3, BrandId = 1, UnitId = 1, Quantity = 50,  UnitPrice = 9500000m, Origin = "Mỹ",         WarrantyMonths = 36, Notes = "Đồ hoạ cực đỉnh" },
                new Product { Id = 5, Name = "Tai nghe kiểm âm Sony MDR-7506", CategoryId = 4, BrandId = 3, UnitId = 1, Quantity = 45, UnitPrice = 2800000m, Origin = "Nhật Bản", WarrantyMonths = 12, Notes = "Tai nghe studio chuẩn" },
                new Product { Id = 6, Name = "Bàn phím cơ Logitech G Pro X", CategoryId = 2, BrandId = 2, UnitId = 1, Quantity = 60, UnitPrice = 2500000m, Origin = "Trung Quốc", WarrantyMonths = 24, Notes = "Bàn phím TKL chuyên eSports" },
                new Product { Id = 7, Name = "Màn hình cong Dell S3221QS", CategoryId = 3, BrandId = 1, UnitId = 1, Quantity = 25, UnitPrice = 11500000m, Origin = "Mỹ", WarrantyMonths = 36, Notes = "Màn hình 4K 32 inch" },
                new Product { Id = 8, Name = "Laptop Dell Inspiron 15", CategoryId = 1, BrandId = 1, UnitId = 1, Quantity = 40, UnitPrice = 18000000m, Origin = "Mỹ", WarrantyMonths = 12, Notes = "Laptop văn phòng quốc dân" },
                new Product { Id = 9, Name = "Tai nghe không dây Sony WH-1000XM4", CategoryId = 4, BrandId = 3, UnitId = 1, Quantity = 70, UnitPrice = 6500000m, Origin = "Nhật Bản", WarrantyMonths = 12, Notes = "Chống ồn chủ động đỉnh cao" },
                new Product { Id = 10, Name = "Chuột không dây Logitech MX Master 3S", CategoryId = 2, BrandId = 2, UnitId = 1, Quantity = 100, UnitPrice = 2300000m, Origin = "Trung Quốc", WarrantyMonths = 12, Notes = "Dòng chuột làm việc chuyên nghiệp" },
                new Product { Id = 11, Name = "Bàn phím không dây Logitech MX Keys", CategoryId = 2, BrandId = 2, UnitId = 1, Quantity = 55, UnitPrice = 2200000m, Origin = "Trung Quốc", WarrantyMonths = 12, Notes = "Thiết kế mỏng, gõ êm ái" },
                new Product { Id = 12, Name = "Loa Bluetooth Sony SRS-XB13", CategoryId = 4, BrandId = 3, UnitId = 1, Quantity = 120, UnitPrice = 1200000m, Origin = "Trung Quốc", WarrantyMonths = 12, Notes = "Nhỏ gọn, âm thanh Extra Bass" },
                new Product { Id = 13, Name = "Màn hình Dell Alienware AW2521H", CategoryId = 3, BrandId = 1, UnitId = 1, Quantity = 15, UnitPrice = 14000000m, Origin = "Mỹ", WarrantyMonths = 36, Notes = "Màn hình Gaming 360Hz" },
                new Product { Id = 14, Name = "Máy ảnh Sony Alpha A7 III", CategoryId = 2, BrandId = 3, UnitId = 1, Quantity = 10, UnitPrice = 45000000m, Origin = "Nhật Bản", WarrantyMonths = 24, Notes = "Máy ảnh Mirrorless Full-frame" },
                new Product { Id = 15, Name = "Laptop Dell Alienware m15 R7", CategoryId = 1, BrandId = 1, UnitId = 1, Quantity = 5, UnitPrice = 65000000m, Origin = "Mỹ", WarrantyMonths = 24, Notes = "Siêu phẩm laptop gaming 2026" }
            );

            base.OnModelCreating(modelBuilder);
        }
    }
}
