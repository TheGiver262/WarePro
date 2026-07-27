using WarePro.Database;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Tests.Services;

public class DatabaseInitializerSchemaTests
{
    [Fact]
    public void Current_schema_repairs_warehouse_posting_columns()
    {
        var sql = DatabaseSchemaScripts.SchemaVersion4;
        foreach (var table in new[] { "StockIn", "StockOut", "StockAdjustment", "StockTransfer" })
        {
            Assert.Contains($"COL_LENGTH('{table}', 'ApprovedAt')", sql);
            Assert.Contains($"COL_LENGTH('{table}', 'PostedAt')", sql);
        }
    }

    [Fact]
    public void Schema_6_adds_rowversion_sessions_and_current_finalize_requires_client_1_1_0()
    {
        Assert.Contains("RowVersion", DatabaseSchemaScripts.SchemaVersion6, StringComparison.Ordinal);
        Assert.Contains("__WareProClientSession", DatabaseSchemaScripts.SchemaVersion6, StringComparison.Ordinal);
        Assert.Contains("MinimumClientVersion = N'1.1.0'",
            DatabaseSchemaScripts.BuildFinalizeSql(9, "1.1.0"), StringComparison.Ordinal);
    }

    [Fact]
    public void Schema_7_adds_invoice_void_status_and_open_claim_guard()
    {
        var sql = DatabaseSchemaScripts.SchemaVersion7;

        Assert.Contains("SalesInvoice", sql, StringComparison.Ordinal);
        Assert.Contains("PurchaseInvoice", sql, StringComparison.Ordinal);
        Assert.Contains("OpenProductSerialId", sql, StringComparison.Ordinal);
        Assert.Contains("UX_WarrantyClaim_OpenProductSerialId", sql, StringComparison.Ordinal);
        Assert.Contains("IF @CurrentVersion < 7", DatabaseSchemaScripts.BuildUpgradeSql(9, "1.1.0"), StringComparison.Ordinal);
    }

    [Fact]
    public void Schema_8_adds_unique_invoice_stock_document_links()
    {
        var sql = DatabaseSchemaScripts.BuildUpgradeSql(9, "1.1.0");

        Assert.Contains("IF @CurrentVersion < 8", sql, StringComparison.Ordinal);
        Assert.Contains("UX_SalesInvoice_StockOutId", sql, StringComparison.Ordinal);
        Assert.Contains("UX_PurchaseInvoice_StockInId", sql, StringComparison.Ordinal);
        Assert.Contains("duplicate stock-out links", sql, StringComparison.Ordinal);
        Assert.Contains("duplicate stock-in links", sql, StringComparison.Ordinal);
        Assert.Contains("mismatched warranty coverage serials", sql, StringComparison.Ordinal);
        Assert.Contains("AK_WarrantyCoverage_Id_ProductSerialId", sql, StringComparison.Ordinal);
        Assert.Contains("FOREIGN KEY (WarrantyCoverageId, ProductSerialId)", sql, StringComparison.Ordinal);
        Assert.Contains("has_filter = 1", DatabaseSchemaScripts.ShapeValidationPredicate, StringComparison.Ordinal);
    }

    [Fact]
    public void Current_schema_allows_system_owned_login_audits()
    {
        var sql = DatabaseSchemaScripts.BuildUpgradeSql(9, "1.1.0");

        Assert.Contains("ALTER TABLE dbo.AuditLog ALTER COLUMN PerformedBy INT NULL", sql, StringComparison.Ordinal);
        Assert.Contains("ON DELETE SET NULL", sql, StringComparison.Ordinal);
        Assert.Contains("OBJECT_ID(N'dbo.AuditLog')", DatabaseSchemaScripts.ShapeValidationPredicate, StringComparison.Ordinal);
    }

    [Fact]
    public void Invoice_models_default_to_active()
    {
        Assert.Equal(InvoiceStatus.Active, new SalesInvoice().Status);
        Assert.Equal(InvoiceStatus.Active, new PurchaseInvoice().Status);
    }

    [Fact]
    public void Schema_6_repairs_archive_operation_identity_idempotently()
    {
        var sql = DatabaseSchemaScripts.SchemaArchiveReplay;
        Assert.Contains("COL_LENGTH(N'dbo.AuditArchiveManifest', N'OperationId') IS NULL", sql);
        Assert.Contains("ADD [OperationId] UNIQUEIDENTIFIER NULL", sql);
        Assert.Contains("SET [OperationId] = NEWID()", sql);
        Assert.Contains("ALTER COLUMN [OperationId] UNIQUEIDENTIFIER NOT NULL", sql);
        Assert.Contains("UX_AuditArchiveManifest_OperationId", sql);
        Assert.Contains("CREATE UNIQUE INDEX", sql);
    }

    [Fact]
    public void Schema_metadata_uses_dynamic_batches_for_legacy_missing_columns()
    {
        var sql = DatabaseSchemaScripts.SchemaMetadata;

        Assert.Contains("EXEC sys.sp_executesql", sql, StringComparison.Ordinal);
        Assert.Contains("N'ALTER TABLE [dbo].[__WareProSchemaVersion] ADD [MinimumClientVersion]", sql, StringComparison.Ordinal);
        Assert.Contains("N'ALTER TABLE [dbo].[__WareProSchemaVersion] ADD [AppliedByAppVersion]", sql, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO [dbo].[__WareProSchemaVersion]", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Schema_metadata_records_client_and_application_compatibility()
    {
        var sql = DatabaseSchemaScripts.SchemaMetadata;
        Assert.Contains("MinimumClientVersion", sql, StringComparison.Ordinal);
        Assert.Contains("AppliedByAppVersion", sql, StringComparison.Ordinal);
        Assert.Contains("UpdatedAt", sql, StringComparison.Ordinal);
    }
}