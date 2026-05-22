using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("Server=.\\SQLEXPRESS;Database=ProductManagementDb;Trusted_Connection=True;TrustServerCertificate=True;");
        }
    }

    public virtual DbSet<AppUser> AppUsers { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Brand> Brands { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductSerial> ProductSerials { get; set; }

    public virtual DbSet<ProductUnit> ProductUnits { get; set; }

    public virtual DbSet<PurchaseInvoice> PurchaseInvoices { get; set; }

    public virtual DbSet<PurchaseInvoiceLine> PurchaseInvoiceLines { get; set; }

    public virtual DbSet<SalesInvoice> SalesInvoices { get; set; }

    public virtual DbSet<SalesInvoiceLine> SalesInvoiceLines { get; set; }

    public virtual DbSet<StockAdjustment> StockAdjustments { get; set; }

    public virtual DbSet<StockAdjustmentLine> StockAdjustmentLines { get; set; }

    public virtual DbSet<StockBalance> StockBalances { get; set; }

    public virtual DbSet<StockCountLine> StockCountLines { get; set; }

    public virtual DbSet<StockCountSession> StockCountSessions { get; set; }

    public virtual DbSet<StockIn> StockIns { get; set; }

    public virtual DbSet<StockInLine> StockInLines { get; set; }

    public virtual DbSet<StockLedger> StockLedgers { get; set; }

    public virtual DbSet<StockOut> StockOuts { get; set; }

    public virtual DbSet<StockOutLine> StockOutLines { get; set; }
    public virtual DbSet<StockTransfer> StockTransfers { get; set; }
    public virtual DbSet<StockTransferLine> StockTransferLines { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }

    public virtual DbSet<Unit> Units { get; set; }

    public virtual DbSet<Warehouse> Warehouses { get; set; }

    public virtual DbSet<WarrantyClaim> WarrantyClaims { get; set; }

    public virtual DbSet<WarrantyCoverage> WarrantyCoverages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var isSqlite = Database.ProviderName?.Contains("Sqlite") ?? false;
        var defaultDateTime = isSqlite ? "CURRENT_TIMESTAMP" : "sysutcdatetime()";
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AppUser__3214EC07BB2116D3");

            entity.ToTable("AppUser");

            entity.HasIndex(e => e.CreatedBy, "IX_AppUser_CreatedBy");

            entity.HasIndex(e => e.Username, "UX_AppUser_Username").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql(defaultDateTime);
            entity.Property(e => e.FullName).HasMaxLength(200);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LastFailedLoginAt).HasPrecision(0);
            entity.Property(e => e.LastLoginAt).HasPrecision(0);
            entity.Property(e => e.LastPasswordChangedAt).HasPrecision(0);
            entity.Property(e => e.LockoutUntil).HasPrecision(0);
            entity.Property(e => e.MustChangePassword).HasDefaultValue(true);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.RoleCode).HasMaxLength(50);
            entity.Property(e => e.Username).HasMaxLength(100);

            entity.HasOne(d => d.Creator).WithMany(p => p.InverseCreator)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_AppUser_CreatedBy");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AuditLog__3214EC07718D8FB1");

            entity.ToTable("AuditLog");

            entity.HasIndex(e => new { e.EntityName, e.EntityId, e.PerformedAt }, "IX_AuditLog_Entity");

            entity.Property(e => e.ActionCode).HasMaxLength(50);
            entity.Property(e => e.EntityName).HasMaxLength(100);
            entity.Property(e => e.PerformedAt)
                .HasPrecision(0)
                .HasDefaultValueSql(defaultDateTime);

            entity.HasOne(d => d.Performer).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.PerformedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AuditLog_PerformedBy");
        });

        modelBuilder.Entity<Brand>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Brand__3214EC07FFE55213");

            entity.ToTable("Brand");

            entity.HasIndex(e => e.BrandCode, "UX_Brand_BrandCode").IsUnique();

            entity.Property(e => e.BrandCode).HasMaxLength(50);
            entity.Property(e => e.DisplayName).HasMaxLength(200);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.OriginCountry).HasMaxLength(100);
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Category__3214EC077A60AC54");

            entity.ToTable("Category");

            entity.HasIndex(e => e.CategoryCode, "UX_Category_CategoryCode").IsUnique();

            entity.Property(e => e.CategoryCode).HasMaxLength(50);
            entity.Property(e => e.DisplayName).HasMaxLength(200);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Customer__3214EC073FBB68C6");

            entity.ToTable("Customer");

            entity.HasIndex(e => e.CustomerCode, "UX_Customer_CustomerCode").IsUnique();

            entity.Property(e => e.CustomerCode).HasMaxLength(50);
            entity.Property(e => e.DisplayName).HasMaxLength(200);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Phone).HasMaxLength(30);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Product__3214EC07522A03D9");

            entity.ToTable("Product");

            entity.HasIndex(e => e.BrandId, "IX_Product_BrandId");

            entity.HasIndex(e => e.CategoryId, "IX_Product_CategoryId");

            entity.HasIndex(e => e.ProductCode, "UX_Product_ProductCode").IsUnique();

            entity.Property(e => e.CostPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Description).IsRequired(false);
            entity.Property(e => e.DefaultPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DisplayName).HasMaxLength(200);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.OriginCountry).HasMaxLength(100);
            entity.Property(e => e.ProductCode).HasMaxLength(50);

            entity.HasOne(d => d.Brand).WithMany(p => p.Products)
                .HasForeignKey(d => d.BrandId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Product_Brand");

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Product_Category");

            entity.HasOne(d => d.DefaultUnit).WithMany(p => p.Products)
                .HasForeignKey(d => d.DefaultUnitId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Product_DefaultUnit");
        });

        modelBuilder.Entity<ProductSerial>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ProductS__3214EC07227D15E8");

            entity.ToTable("ProductSerial");

            entity.HasIndex(e => e.SerialNumber, "UX_ProductSerial_SerialNumber").IsUnique();

            entity.Property(e => e.CurrentStatus).HasMaxLength(50);
            entity.Property(e => e.Note).IsRequired(false);
            entity.Property(e => e.SerialNumber).HasMaxLength(150);

            entity.HasOne(d => d.CurrentWarehouse).WithMany(p => p.ProductSerials)
                .HasForeignKey(d => d.CurrentWarehouseId)
                .HasConstraintName("FK_ProductSerial_CurrentWarehouse");

            entity.HasOne(d => d.LastStockInLine).WithMany(p => p.ProductSerials)
                .HasForeignKey(d => d.LastStockInLineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductSerial_LastStockInLine");

            entity.HasOne(d => d.LastStockOutLine).WithMany(p => p.ProductSerials)
                .HasForeignKey(d => d.LastStockOutLineId)
                .HasConstraintName("FK_ProductSerial_LastStockOutLine");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductSerials)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductSerial_Product");

            entity.HasOne(d => d.StockTransferLine).WithMany(p => p.ProductSerials)
                .HasForeignKey(d => d.StockTransferLineId)
                .HasConstraintName("FK_ProductSerial_StockTransferLine");
        });

        modelBuilder.Entity<ProductUnit>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ProductU__3214EC077420ED44");

            entity.ToTable("ProductUnit");

            entity.HasIndex(e => e.ProductId, "UX_ProductUnit_BaseUnit")
                .IsUnique()
                .HasFilter("IsBaseUnit = 1");

            entity.HasIndex(e => new { e.ProductId, e.UnitId }, "UX_ProductUnit_Product_Unit").IsUnique();

            entity.Property(e => e.ConversionFactor).HasColumnType("decimal(18, 6)");

            entity.HasOne(d => d.Product).WithOne(p => p.ProductUnit)
                .HasForeignKey<ProductUnit>(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductUnit_Product");

            entity.HasOne(d => d.Unit).WithMany(p => p.ProductUnits)
                .HasForeignKey(d => d.UnitId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProductUnit_Unit");
        });

        modelBuilder.Entity<PurchaseInvoice>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Purchase__3214EC0788F7D39F");

            entity.ToTable("PurchaseInvoice");

            entity.HasIndex(e => e.InvoiceCode, "UX_PurchaseInvoice_InvoiceCode").IsUnique();

            entity.Property(e => e.GrandTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasPrecision(0);
            entity.Property(e => e.Notes).IsRequired(false);
            entity.Property(e => e.PaidAmount).HasColumnType("decimal(18, 2)").HasDefaultValue(0m);
            entity.Property(e => e.PaymentStatus).HasMaxLength(50).HasDefaultValue("Unpaid");
            entity.Property(e => e.DueDate).HasPrecision(0);
            entity.Property(e => e.InvoiceCode).HasMaxLength(50);
            entity.Property(e => e.InvoiceDate).HasPrecision(0);
            entity.Property(e => e.SubTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxAmount).HasColumnType("decimal(18, 2)");

            entity.ToTable("PurchaseInvoice", t => t.HasCheckConstraint("CK_PurchaseInvoice_PaymentStatus", "[PaymentStatus] IN ('Unpaid', 'PartiallyPaid', 'Paid', 'Overdue')"));

            entity.HasOne(d => d.StockIn).WithMany(p => p.PurchaseInvoices)
                .HasForeignKey(d => d.StockInId)
                .HasConstraintName("FK_PurchaseInvoice_StockIn");

            entity.HasOne(d => d.Supplier).WithMany(p => p.PurchaseInvoices)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PurchaseInvoice_Supplier");

            entity.HasOne(d => d.Creator).WithMany(p => p.PurchaseInvoices)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PurchaseInvoice_CreatedBy");
        });

        modelBuilder.Entity<PurchaseInvoiceLine>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Purchase__3214EC07D7329CC8");

            entity.ToTable("PurchaseInvoiceLine");

            entity.Property(e => e.GrandTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SubTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxRate).HasColumnType("decimal(9, 4)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Product).WithMany(p => p.PurchaseInvoiceLines)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PurchaseInvoiceLine_Product");

            entity.HasOne(d => d.PurchaseInvoice).WithMany(p => p.Lines)
                .HasForeignKey(d => d.PurchaseInvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PurchaseInvoiceLine_Invoice");

            entity.HasOne(d => d.StockInLine).WithMany(p => p.PurchaseInvoiceLines)
                .HasForeignKey(d => d.StockInLineId)
                .HasConstraintName("FK_PurchaseInvoiceLine_StockInLine");

            entity.HasOne(d => d.Unit).WithMany(p => p.PurchaseInvoiceLines)
                .HasForeignKey(d => d.UnitId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PurchaseInvoiceLine_Unit");
        });

        modelBuilder.Entity<SalesInvoice>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__SalesInv__3214EC07631EEA74");

            entity.ToTable("SalesInvoice");

            entity.HasIndex(e => e.InvoiceCode, "UX_SalesInvoice_InvoiceCode").IsUnique();

            entity.Property(e => e.GrandTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasPrecision(0);
            entity.Property(e => e.Notes).IsRequired(false);
            entity.Property(e => e.PaidAmount).HasColumnType("decimal(18, 2)").HasDefaultValue(0m);
            entity.Property(e => e.PaymentStatus).HasMaxLength(50).HasDefaultValue("Unpaid");
            entity.Property(e => e.DueDate).HasPrecision(0);
            entity.Property(e => e.InvoiceCode).HasMaxLength(50);
            entity.Property(e => e.InvoiceDate).HasPrecision(0);
            entity.Property(e => e.SubTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxAmount).HasColumnType("decimal(18, 2)");

            entity.ToTable("SalesInvoice", t => t.HasCheckConstraint("CK_SalesInvoice_PaymentStatus", "[PaymentStatus] IN ('Unpaid', 'PartiallyPaid', 'Paid', 'Overdue')"));

            entity.HasOne(d => d.Customer).WithMany(p => p.SalesInvoices)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SalesInvoice_Customer");

            entity.HasOne(d => d.StockOut).WithMany(p => p.SalesInvoices)
                .HasForeignKey(d => d.StockOutId)
                .HasConstraintName("FK_SalesInvoice_StockOut");

            entity.HasOne(d => d.Creator).WithMany(p => p.SalesInvoices)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SalesInvoice_CreatedBy");
        });

        modelBuilder.Entity<SalesInvoiceLine>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__SalesInv__3214EC07959093CA");

            entity.ToTable("SalesInvoiceLine");

            entity.Property(e => e.GrandTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SubTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TaxRate).HasColumnType("decimal(9, 4)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Product).WithMany(p => p.SalesInvoiceLines)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SalesInvoiceLine_Product");

            entity.HasOne(d => d.SalesInvoice).WithMany(p => p.Lines)
                .HasForeignKey(d => d.SalesInvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SalesInvoiceLine_Invoice");

            entity.HasOne(d => d.StockOutLine).WithMany(p => p.SalesInvoiceLines)
                .HasForeignKey(d => d.StockOutLineId)
                .HasConstraintName("FK_SalesInvoiceLine_StockOutLine");

            entity.HasOne(d => d.Unit).WithMany(p => p.SalesInvoiceLines)
                .HasForeignKey(d => d.UnitId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SalesInvoiceLine_Unit");
        });

        modelBuilder.Entity<StockAdjustment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__StockAdj__3214EC07AF8EB3B2");

            entity.ToTable("StockAdjustment");

            entity.HasIndex(e => e.DocumentCode, "UX_StockAdjustment_DocumentCode").IsUnique();

            entity.Property(e => e.AdjustmentType).HasMaxLength(50);
            entity.Property(e => e.ApprovedAt).HasPrecision(0);
            entity.Property(e => e.DocumentCode).HasMaxLength(50);
            entity.Property(e => e.PostedAt).HasPrecision(0);
            entity.Property(e => e.ReasonCode).HasMaxLength(100);
            entity.Property(e => e.ReferenceDocumentType).HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Ignore(e => e.ReferenceDocumentCode);

            entity.HasOne(d => d.Approver).WithMany(p => p.StockAdjustmentApprovers)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK_StockAdjustment_ApprovedBy");

            entity.HasOne(d => d.Creator).WithMany(p => p.StockAdjustmentCreators)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockAdjustment_CreatedBy");

            entity.HasOne(d => d.Poster).WithMany(p => p.StockAdjustmentPosters)
                .HasForeignKey(d => d.PostedBy)
                .HasConstraintName("FK_StockAdjustment_PostedBy");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.StockAdjustments)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockAdjustment_Warehouse");
        });

        modelBuilder.Entity<StockAdjustmentLine>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__StockAdj__3214EC075A0DD95F");

            entity.ToTable("StockAdjustmentLine");

            entity.Property(e => e.BaseQuantityDelta).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Direction).HasMaxLength(20);
            entity.Property(e => e.QuantityDelta).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Adjustment).WithMany(p => p.Lines)
                .HasForeignKey(d => d.AdjustmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockAdjustmentLine_Adjustment");

            entity.HasOne(d => d.Product).WithMany(p => p.StockAdjustmentLines)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockAdjustmentLine_Product");

            entity.HasOne(d => d.ProductSerial).WithMany(p => p.StockAdjustmentLines)
                .HasForeignKey(d => d.ProductSerialId)
                .HasConstraintName("FK_StockAdjustmentLine_ProductSerial");
        });

        modelBuilder.Entity<StockBalance>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__StockBal__3214EC074E13DD5E");

            entity.ToTable("StockBalance");

            entity.HasIndex(e => new { e.WarehouseId, e.ProductId }, "UX_StockBalance_Warehouse_Product").IsUnique();

            entity.Property(e => e.AvailableQuantity).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.OnHandQuantity).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ReservedQuantity).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Product).WithMany(p => p.StockBalances)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockBalance_Product");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.StockBalances)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockBalance_Warehouse");
        });

        modelBuilder.Entity<StockCountLine>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__StockCou__3214EC07D1A91334");

            entity.ToTable("StockCountLine");

            entity.Property(e => e.CountedQuantity).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SystemQuantity).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.VarianceQuantity).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Product).WithMany(p => p.StockCountLines)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockCountLine_Product");

            entity.HasOne(d => d.Session).WithMany(p => p.Lines)
                .HasForeignKey(d => d.SessionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockCountLine_Session");
        });

        modelBuilder.Entity<StockCountSession>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__StockCou__3214EC07BB80C561");

            entity.ToTable("StockCountSession");

            entity.HasIndex(e => e.SessionCode, "UX_StockCountSession_SessionCode").IsUnique();

            entity.Property(e => e.ApprovedAt).HasPrecision(0);
            entity.Property(e => e.CountDate).HasPrecision(0);
            entity.Property(e => e.PostedAt).HasPrecision(0);
            entity.Property(e => e.SessionCode).HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasOne(d => d.Approver).WithMany(p => p.StockCountSessionApprovers)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK_StockCountSession_ApprovedBy");

            entity.HasOne(d => d.Creator).WithMany(p => p.StockCountSessionCreators)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockCountSession_CreatedBy");

            entity.HasOne(d => d.Poster).WithMany(p => p.StockCountSessionPosters)
                .HasForeignKey(d => d.PostedBy)
                .HasConstraintName("FK_StockCountSession_PostedBy");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.StockCountSessions)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockCountSession_Warehouse");
        });

        modelBuilder.Entity<StockIn>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__StockIn__3214EC07786210D8");

            entity.ToTable("StockIn");

            entity.HasIndex(e => e.SupplierId, "IX_StockIn_SupplierId");

            entity.HasIndex(e => new { e.WarehouseId, e.PostedAt }, "IX_StockIn_Warehouse_ProductLookup");

            entity.HasIndex(e => e.DocumentCode, "UX_StockIn_DocumentCode").IsUnique();

            entity.Property(e => e.ApprovedAt).HasPrecision(0);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql(defaultDateTime);
            entity.Property(e => e.DocumentCode).HasMaxLength(50);
            entity.Property(e => e.PostedAt).HasPrecision(0);
            entity.Property(e => e.PurposeCode).HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.ImportDate).HasPrecision(0);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
            entity.Property(e => e.UpdatedBy).IsRequired(false);
            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasOne(d => d.Approver).WithMany(p => p.StockInApprovers)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK_StockIn_ApprovedBy");

            entity.HasOne(d => d.Creator).WithMany(p => p.StockInCreators)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockIn_CreatedBy");

            entity.HasOne(d => d.Poster).WithMany(p => p.StockInPosters)
                .HasForeignKey(d => d.PostedBy)
                .HasConstraintName("FK_StockIn_PostedBy");

            entity.HasOne(d => d.Supplier).WithMany(p => p.StockIns)
                .HasForeignKey(d => d.SupplierId)
                .HasConstraintName("FK_StockIn_Supplier");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.StockIns)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockIn_Warehouse");
        });

        modelBuilder.Entity<StockInLine>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__StockInL__3214EC0700E37989");

            entity.ToTable("StockInLine");

            entity.Property(e => e.BaseQuantity).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DraftSerials).IsRequired(false);

            entity.HasOne(d => d.Product).WithMany(p => p.StockInLines)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockInLine_Product");

            entity.HasOne(d => d.StockIn).WithMany(p => p.Lines)
                .HasForeignKey(d => d.StockInId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockInLine_StockIn");

            entity.HasOne(d => d.Unit).WithMany(p => p.StockInLines)
                .HasForeignKey(d => d.UnitId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockInLine_Unit");
        });

        modelBuilder.Entity<StockLedger>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__StockLed__3214EC072689E89F");

            entity.ToTable("StockLedger");

            entity.HasIndex(e => new { e.SourceDocumentType, e.SourceDocumentId }, "IX_StockLedger_SourceDocument");

            entity.HasIndex(e => new { e.WarehouseId, e.ProductId, e.PostedAt }, "IX_StockLedger_Warehouse_Product_PostedAt");

            entity.Property(e => e.MovementType).HasMaxLength(50);
            entity.Property(e => e.PostedAt)
                .HasPrecision(0)
                .HasDefaultValueSql(defaultDateTime);
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SourceDocumentType).HasMaxLength(50);

            entity.HasOne(d => d.Poster).WithMany(p => p.StockLedgers)
                .HasForeignKey(d => d.PostedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockLedger_PostedBy");

            entity.HasOne(d => d.Product).WithMany(p => p.StockLedgers)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockLedger_Product");

            entity.HasOne(d => d.ProductSerial).WithMany(p => p.StockLedgers)
                .HasForeignKey(d => d.ProductSerialId)
                .HasConstraintName("FK_StockLedger_ProductSerial");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.StockLedgers)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockLedger_Warehouse");
        });

        modelBuilder.Entity<StockOut>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__StockOut__3214EC073AFD26FA");

            entity.ToTable("StockOut");

            entity.HasIndex(e => e.CustomerId, "IX_StockOut_CustomerId");

            entity.HasIndex(e => new { e.WarehouseId, e.PostedAt }, "IX_StockOut_Warehouse_ProductLookup");

            entity.HasIndex(e => e.DocumentCode, "UX_StockOut_DocumentCode").IsUnique();

            entity.Property(e => e.ApprovedAt).HasPrecision(0);
            entity.Property(e => e.CreatedAt)
                .HasPrecision(0)
                .HasDefaultValueSql(defaultDateTime);
            entity.Property(e => e.DocumentCode).HasMaxLength(50);
            entity.Property(e => e.PostedAt).HasPrecision(0);
            entity.Property(e => e.PurposeCode).HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.ExportDate).HasPrecision(0);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
            entity.Property(e => e.UpdatedBy).IsRequired(false);

            entity.HasOne(d => d.Approver).WithMany(p => p.StockOutApprovers)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK_StockOut_ApprovedBy");

            entity.HasOne(d => d.Creator).WithMany(p => p.StockOutCreators)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockOut_CreatedBy");

            entity.HasOne(d => d.Customer).WithMany(p => p.StockOuts)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockOut_Customer");

            entity.HasOne(d => d.Poster).WithMany(p => p.StockOutPosters)
                .HasForeignKey(d => d.PostedBy)
                .HasConstraintName("FK_StockOut_PostedBy");

            entity.HasOne(d => d.Warehouse).WithMany(p => p.StockOuts)
                .HasForeignKey(d => d.WarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockOut_Warehouse");
        });

        modelBuilder.Entity<StockOutLine>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__StockOut__3214EC07BD561A98");

            entity.ToTable("StockOutLine");

            entity.Property(e => e.BaseQuantity).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DraftSerials).IsRequired(false);

            entity.HasOne(d => d.Product).WithMany(p => p.StockOutLines)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockOutLine_Product");

            entity.HasOne(d => d.StockOut).WithMany(p => p.Lines)
                .HasForeignKey(d => d.StockOutId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockOutLine_StockOut");

            entity.HasOne(d => d.Unit).WithMany(p => p.StockOutLines)
                .HasForeignKey(d => d.UnitId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockOutLine_Unit");
        });

        modelBuilder.Entity<StockTransfer>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_StockTransfer");
            entity.ToTable("StockTransfer");

            entity.HasIndex(e => e.DocumentCode, "UX_StockTransfer_DocumentCode").IsUnique();

            entity.Property(e => e.CreatedAt).HasPrecision(0).HasDefaultValueSql(defaultDateTime);
            entity.Property(e => e.DocumentCode).HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.TransferDate).HasPrecision(0);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.UpdatedAt).HasPrecision(0);
            entity.Property(e => e.UpdatedBy).IsRequired(false);

            entity.HasOne(d => d.Approver).WithMany(p => p.StockTransferApprovers)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK_StockTransfer_ApprovedBy");

            entity.HasOne(d => d.Creator).WithMany(p => p.StockTransferCreators)
                .HasForeignKey(d => d.CreatedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockTransfer_CreatedBy");

            entity.HasOne(d => d.Poster).WithMany(p => p.StockTransferPosters)
                .HasForeignKey(d => d.PostedBy)
                .HasConstraintName("FK_StockTransfer_PostedBy");

            entity.HasOne(d => d.FromWarehouse).WithMany(p => p.StockTransfersFrom)
                .HasForeignKey(d => d.FromWarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockTransfer_FromWarehouse");

            entity.HasOne(d => d.ToWarehouse).WithMany(p => p.StockTransfersTo)
                .HasForeignKey(d => d.ToWarehouseId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockTransfer_ToWarehouse");
        });

        modelBuilder.Entity<StockTransferLine>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_StockTransferLine");
            entity.ToTable("StockTransferLine");

            entity.Property(e => e.BaseQuantity).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Product).WithMany(p => p.StockTransferLines)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockTransferLine_Product");

            entity.HasOne(d => d.StockTransfer).WithMany(p => p.Lines)
                .HasForeignKey(d => d.StockTransferId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockTransferLine_StockTransfer");

            entity.HasOne(d => d.Unit).WithMany(p => p.StockTransferLines)
                .HasForeignKey(d => d.UnitId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockTransferLine_Unit");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Supplier__3214EC0720C79339");

            entity.ToTable("Supplier");

            entity.HasIndex(e => e.SupplierCode, "UX_Supplier_SupplierCode").IsUnique();

            entity.Property(e => e.DisplayName).HasMaxLength(200);
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Phone).HasMaxLength(30);
            entity.Property(e => e.SupplierCode).HasMaxLength(50);
        });

        modelBuilder.Entity<Unit>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Unit__3214EC07F52055D4");

            entity.ToTable("Unit");

            entity.HasIndex(e => e.UnitCode, "UX_Unit_UnitCode").IsUnique();

            entity.Property(e => e.DisplayName).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UnitCode).HasMaxLength(50);
        });

        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Warehous__3214EC0700E1A1FD");

            entity.ToTable("Warehouse");

            entity.HasIndex(e => e.IsDefault, "UX_Warehouse_SingleDefault")
                .IsUnique()
                .HasFilter("IsDefault = 1");

            entity.HasIndex(e => e.WarehouseCode, "UX_Warehouse_WarehouseCode").IsUnique();

            entity.Property(e => e.DisplayName).HasMaxLength(200);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.WarehouseCode).HasMaxLength(50);
        });

        modelBuilder.Entity<WarrantyClaim>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Warranty__3214EC07EDE89108");

            entity.ToTable("WarrantyClaim");

            entity.HasIndex(e => e.WarrantyCoverageId, "IX_WarrantyClaim_CoverageId");

            entity.HasIndex(e => e.ClaimCode, "UX_WarrantyClaim_ClaimCode").IsUnique();

            entity.HasIndex(e => e.ProductSerialId, "UX_WarrantyClaim_OpenClaim_PerSerial");

            entity.Property(e => e.ClaimCode).HasMaxLength(50);
            entity.Property(e => e.ClosedDate).HasPrecision(0);
            entity.Property(e => e.ManufacturerResult).HasMaxLength(1000);
            entity.Property(e => e.ProblemDescription).HasMaxLength(1000);
            entity.Property(e => e.ProcessingNote).HasMaxLength(1000);
            entity.Property(e => e.ReceivedDate).HasPrecision(0);
            entity.Property(e => e.RejectionReason).HasMaxLength(1000);
            entity.Property(e => e.ResolutionType).HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.TechnicalConclusion).HasMaxLength(1000);
            entity.Property(e => e.ExpectedReturnDate).HasPrecision(0);
            entity.Property(e => e.ManufacturerName).HasMaxLength(200);
            entity.Property(e => e.ManufacturerTrackingCode).HasMaxLength(100);
            entity.Property(e => e.ManufacturerExpectedReturnDate).HasPrecision(0);

            entity.HasOne(d => d.Approver).WithMany(p => p.WarrantyClaimApprovers)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK_WarrantyClaim_ApprovedBy");

            entity.HasOne(d => d.Processor).WithMany(p => p.WarrantyClaimProcessors)
                .HasForeignKey(d => d.ProcessedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WarrantyClaim_ProcessedBy");

            entity.HasOne(d => d.ProductSerial).WithMany(p => p.WarrantyClaims)
                .HasForeignKey(d => d.ProductSerialId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WarrantyClaim_ProductSerial");

            entity.HasOne(d => d.ReplacementSerial).WithMany(p => p.WarrantyClaimReplacementSerials)
                .HasForeignKey(d => d.ReplacementSerialId)
                .HasConstraintName("FK_WarrantyClaim_ReplacementSerial");

            entity.HasOne(d => d.ReplacementStockOut).WithMany(p => p.WarrantyClaims)
                .HasForeignKey(d => d.ReplacementStockOutId)
                .HasConstraintName("FK_WarrantyClaim_ReplacementStockOut");

            entity.HasOne(d => d.WarrantyCoverage).WithMany(p => p.WarrantyClaims)
                .HasForeignKey(d => d.WarrantyCoverageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WarrantyClaim_Coverage");
        });

        modelBuilder.Entity<WarrantyCoverage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Warranty__3214EC07B813D31B");

            entity.ToTable("WarrantyCoverage");

            entity.HasIndex(e => e.CustomerId, "IX_WarrantyCoverage_CustomerId");

            entity.HasIndex(e => e.ProductSerialId, "UX_WarrantyCoverage_Active_PerSerial")
                .IsUnique()
                .HasFilter("CoverageStatus = 'Active'");

            entity.Property(e => e.CoverageStatus).HasMaxLength(50);
            entity.Property(e => e.WarrantyEndDate).HasPrecision(0);
            entity.Property(e => e.WarrantyStartDate).HasPrecision(0);

            entity.HasOne(d => d.Customer).WithMany(p => p.WarrantyCoverages)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WarrantyCoverage_Customer");

            entity.HasOne(d => d.ProductSerial).WithOne(p => p.WarrantyCoverage)
                .HasForeignKey<WarrantyCoverage>(d => d.ProductSerialId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_WarrantyCoverage_ProductSerial");

            entity.HasOne(d => d.SalesInvoice).WithMany(p => p.WarrantyCoverages)
                .HasForeignKey(d => d.SalesInvoiceId)
                .HasConstraintName("FK_WarrantyCoverage_SalesInvoice");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
