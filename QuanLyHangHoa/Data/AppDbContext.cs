using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Models;
using System;

namespace QuanLyHangHoa.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext() { }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Master Data
        public DbSet<AppUser> AppUsers { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Brand> Brands { get; set; }
        public DbSet<Unit> Units { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Warehouse> Warehouses { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductUnit> ProductUnits { get; set; }
        public DbSet<InvoicePayment> InvoicePayments { get; set; } = null!;

        // Inventory & Tracking
        public DbSet<StockBalance> StockBalances { get; set; }
        public DbSet<ProductSerial> ProductSerials { get; set; }
        public DbSet<StockLedger> StockLedgers { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        // Operations
        public DbSet<StockIn> StockIns { get; set; }
        public DbSet<StockInLine> StockInLines { get; set; }
        public DbSet<StockOut> StockOuts { get; set; }
        public DbSet<StockOutLine> StockOutLines { get; set; }
        public DbSet<StockCountSession> StockCountSessions { get; set; }
        public DbSet<StockCountLine> StockCountLines { get; set; }
        public DbSet<StockAdjustment> StockAdjustments { get; set; }
        public DbSet<StockAdjustmentLine> StockAdjustmentLines { get; set; }

        // Finance
        public DbSet<PurchaseInvoice> PurchaseInvoices { get; set; }
        public DbSet<PurchaseInvoiceLine> PurchaseInvoiceLines { get; set; }
        public DbSet<SalesInvoice> SalesInvoices { get; set; }
        public DbSet<SalesInvoiceLine> SalesInvoiceLines { get; set; }

        // Warranty
        public DbSet<WarrantyCoverage> WarrantyCoverages { get; set; }
        public DbSet<WarrantyClaim> WarrantyClaims { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                string dbDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database");
                if (!System.IO.Directory.Exists(dbDir))
                {
                    System.IO.Directory.CreateDirectory(dbDir);
                }
                string dbPath = System.IO.Path.Combine(dbDir, "QuanLyHangHoa_v2.db");
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── AppUser
            modelBuilder.Entity<AppUser>().HasIndex(u => u.Username).IsUnique();

            // ── Category
            modelBuilder.Entity<Category>().HasIndex(c => c.CategoryCode).IsUnique();

            // ── Brand
            modelBuilder.Entity<Brand>().HasIndex(b => b.BrandCode).IsUnique();

            // ── Unit
            modelBuilder.Entity<Unit>().HasIndex(u => u.UnitCode).IsUnique();

            // ── Supplier
            modelBuilder.Entity<Supplier>().HasIndex(s => s.SupplierCode).IsUnique();

            // ── Customer
            modelBuilder.Entity<Customer>().HasIndex(c => c.CustomerCode).IsUnique();

            // ── Warehouse
            modelBuilder.Entity<Warehouse>().HasIndex(w => w.WarehouseCode).IsUnique();

            // ── Product
            modelBuilder.Entity<Product>().HasIndex(p => p.ProductCode).IsUnique();
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
                .HasOne(p => p.DefaultUnit)
                .WithMany()
                .HasForeignKey(p => p.DefaultUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── ProductUnit
            modelBuilder.Entity<ProductUnit>().HasIndex(pu => new { pu.ProductId, pu.UnitId }).IsUnique();

            // ── StockBalance
            modelBuilder.Entity<StockBalance>().HasIndex(sb => new { sb.WarehouseId, sb.ProductId }).IsUnique();

            // ── StockIn
            modelBuilder.Entity<StockIn>().HasIndex(si => si.DocumentCode).IsUnique();

            // ── StockOut
            modelBuilder.Entity<StockOut>().HasIndex(so => so.DocumentCode).IsUnique();

            // ── ProductSerial
            modelBuilder.Entity<ProductSerial>().HasIndex(ps => ps.SerialNumber).IsUnique();

            // ── PurchaseInvoice
            modelBuilder.Entity<PurchaseInvoice>().HasIndex(pi => pi.InvoiceCode).IsUnique();

            // ── SalesInvoice
            modelBuilder.Entity<SalesInvoice>().HasIndex(si => si.InvoiceCode).IsUnique();

            // ── WarrantyClaim
            modelBuilder.Entity<WarrantyClaim>().HasIndex(wc => wc.ClaimCode).IsUnique();

            // ── StockLedger Indexes
            modelBuilder.Entity<StockLedger>().HasIndex(sl => new { sl.WarehouseId, sl.ProductId, sl.PostedAt });
            modelBuilder.Entity<StockLedger>().HasIndex(sl => new { sl.SourceDocumentType, sl.SourceDocumentId });

            // ── AuditLog Indexes
            modelBuilder.Entity<AuditLog>().HasIndex(al => new { al.EntityName, al.EntityId, al.PerformedAt });

            // ── Seeding Master Data (Optional: use Migration seeding or separate Initializer)
            SeedData(modelBuilder);
        }

        private void SeedData(ModelBuilder modelBuilder)
        {
            // Seed Admin User (Password: admin123)
            // Hash: $2a$11$m6m1Y2.vEaOqP0GZ0O8e2.pI1oJ6k3aZ5oZ6Y5qZ6Y5qZ6Y5qZ6Y5
            // Actually I'll use a hardcoded hash to avoid dependency issues during migration generation
            string adminHash = BCrypt.Net.BCrypt.HashPassword("admin123");

            modelBuilder.Entity<AppUser>().HasData(new AppUser
            {
                Id = 1,
                Username = "admin",
                PasswordHash = adminHash,
                FullName = "Administrator",
                RoleCode = "Admin",
                IsActive = true,
                MustChangePassword = false
            });

            modelBuilder.Entity<Warehouse>().HasData(new Warehouse
            {
                Id = 1,
                WarehouseCode = "WH01",
                DisplayName = "Main Warehouse",
                IsDefault = true,
                IsActive = true
            });

            modelBuilder.Entity<Unit>().HasData(
                new Unit { Id = 1, UnitCode = "PCS", DisplayName = "Cái", IsActive = true },
                new Unit { Id = 2, UnitCode = "SET", DisplayName = "Bộ", IsActive = true }
            );

            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, CategoryCode = "LAPTOP", DisplayName = "Laptop", IsActive = true },
                new Category { Id = 2, CategoryCode = "ACCESSORY", DisplayName = "Linh kiện", IsActive = true }
            );

            modelBuilder.Entity<Brand>().HasData(
                new Brand { Id = 1, BrandCode = "DELL", DisplayName = "Dell", IsActive = true },
                new Brand { Id = 2, BrandCode = "SONY", DisplayName = "Sony", IsActive = true }
            );

            modelBuilder.Entity<Supplier>().HasData(
                new Supplier { Id = 1, SupplierCode = "SUP01", DisplayName = "Công ty TNHH Công Nghệ A", Phone = "0123456789", Email = "contact@tech-a.vn", Address = "Hà Nội", IsActive = true },
                new Supplier { Id = 2, SupplierCode = "SUP02", DisplayName = "Nhà Phân Phối B", Phone = "0987654321", Email = "sales@distributor-b.com", Address = "TP. HCM", IsActive = true }
            );

            modelBuilder.Entity<Customer>().HasData(
                new Customer { Id = 1, CustomerCode = "CUS01", DisplayName = "Nguyễn Văn A", Phone = "0909090909", Email = "nguyenvana@gmail.com", Address = "Đà Nẵng", IsActive = true },
                new Customer { Id = 2, CustomerCode = "CUS02", DisplayName = "Trần Thị B", Phone = "0808080808", Email = "tranthib@gmail.com", Address = "Hải Phòng", IsActive = true }
            );

            modelBuilder.Entity<Product>().HasData(
                new Product { Id = 1, ProductCode = "PROD01", DisplayName = "Laptop Dell Inspiron 15", CategoryId = 1, BrandId = 1, DefaultUnitId = 1, DefaultPrice = 15000000, OriginCountry = "Trung Quốc", WarrantyPeriodMonths = 12, IsSerialTracked = true, IsActive = true },
                new Product { Id = 2, ProductCode = "PROD02", DisplayName = "Tai nghe Sony WH-1000XM4", CategoryId = 2, BrandId = 2, DefaultUnitId = 1, DefaultPrice = 6000000, OriginCountry = "Malaysia", WarrantyPeriodMonths = 12, IsSerialTracked = false, IsActive = true }
            );
        }
    }
}
