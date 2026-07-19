using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Tests.Data;

public sealed class RowVersionModelTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static readonly string[] MutableEntityNames =
    [
        nameof(AppUser),
        nameof(AuditArchiveManifest),
        nameof(Brand),
        nameof(Category),
        nameof(Customer),
        nameof(Product),
        nameof(ProductSerial),
        nameof(ProductUnit),
        nameof(PurchaseInvoice),
        nameof(PurchaseInvoiceLine),
        nameof(SalesInvoice),
        nameof(SalesInvoiceLine),
        nameof(StockAdjustment),
        nameof(StockAdjustmentLine),
        nameof(StockBalance),
        nameof(StockCountLine),
        nameof(StockCountSession),
        nameof(StockIn),
        nameof(StockInLine),
        nameof(StockOut),
        nameof(StockOutLine),
        nameof(StockTransfer),
        nameof(StockTransferLine),
        nameof(Supplier),
        nameof(Unit),
        nameof(Warehouse),
        nameof(WarrantyClaim),
        nameof(WarrantyCoverage),
        nameof(WareProClientSession)
    ];

    [Fact]
    public void Every_mutable_entity_has_one_generated_rowversion_token()
    {
        using var context = CreateContext();
        var entities = context.Model.GetEntityTypes().ToArray();
        var mutableEntities = entities
            .Where(entity => entity.ClrType != typeof(AuditLog) &&
                entity.ClrType != typeof(StockLedger))
            .OrderBy(entity => entity.ClrType.Name)
            .ToArray();

        Assert.Equal(
            MutableEntityNames.OrderBy(name => name),
            mutableEntities.Select(entity => entity.ClrType.Name));

        foreach (var entity in mutableEntities)
        {
            var concurrencyTokens = entity.GetProperties()
                .Where(property => property.IsConcurrencyToken)
                .ToArray();

            var rowVersion = Assert.Single(concurrencyTokens);
            Assert.Equal("RowVersion", rowVersion.Name);
            Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
        }
    }

    [Fact]
    public void Append_only_entities_have_no_concurrency_token()
    {
        using var context = CreateContext();

        foreach (var type in new[] { typeof(AuditLog), typeof(StockLedger) })
        {
            var entity = context.Model.FindEntityType(type);
            Assert.NotNull(entity);
            Assert.DoesNotContain(entity.GetProperties(), property => property.IsConcurrencyToken);
        }
    }

    [Theory]
    [InlineData("AppUser")]
    [InlineData("AuditArchiveManifest")]
    [InlineData("Brand")]
    [InlineData("Category")]
    [InlineData("Customer")]
    [InlineData("Product")]
    [InlineData("ProductSerial")]
    [InlineData("ProductUnit")]
    [InlineData("PurchaseInvoice")]
    [InlineData("PurchaseInvoiceLine")]
    [InlineData("SalesInvoice")]
    [InlineData("SalesInvoiceLine")]
    [InlineData("StockAdjustment")]
    [InlineData("StockAdjustmentLine")]
    [InlineData("StockBalance")]
    [InlineData("StockCountLine")]
    [InlineData("StockCountSession")]
    [InlineData("StockIn")]
    [InlineData("StockInLine")]
    [InlineData("StockOut")]
    [InlineData("StockOutLine")]
    [InlineData("StockTransfer")]
    [InlineData("StockTransferLine")]
    [InlineData("Supplier")]
    [InlineData("Unit")]
    [InlineData("Warehouse")]
    [InlineData("WarrantyClaim")]
    [InlineData("WarrantyCoverage")]
    public void Schema_6_adds_rowversion_to_each_mutable_business_table(string tableName)
    {
        var sql = ReadSchema6();

        Assert.Contains(
            $"COL_LENGTH(N'dbo.{tableName}', N'RowVersion') IS NULL",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            $"ALTER TABLE [dbo].[{tableName}] ADD [RowVersion] ROWVERSION NOT NULL",
            sql,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Schema_6_is_idempotent_and_excludes_append_only_tables()
    {
        var sql = ReadSchema6();

        Assert.Contains("READ_COMMITTED_SNAPSHOT ON", sql, StringComparison.Ordinal);
        Assert.Contains("OBJECT_ID(N'[dbo].[__WareProClientSession]', N'U') IS NULL", sql, StringComparison.Ordinal);
        Assert.Contains("IX___WareProClientSession_LastSeenUtc", sql, StringComparison.Ordinal);
        Assert.Contains("[Version] = 6", sql, StringComparison.Ordinal);
        Assert.Contains("[MinimumClientVersion] = N'1.1.0'", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("ALTER TABLE [dbo].[AuditLog] ADD [RowVersion]", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("ALTER TABLE [dbo].[StockLedger] ADD [RowVersion]", sql, StringComparison.Ordinal);
    }

    private static string ReadSchema6() => File.ReadAllText(
        Path.Combine(RepoRoot, "Database", "Schema", "v6-common-write-safety.sql"));

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        return new AppDbContext(options);
    }
}
