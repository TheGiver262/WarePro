using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Tests.Data;

public sealed class AppDbContextSqlServerModelTests
{
    [Fact]
    public void Open_warranty_claim_index_uses_supported_filtered_columns()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(local);Database=WareProModelTest;Integrated Security=True;Encrypt=False")
            .Options;
        using var context = new AppDbContext(options);

        var warrantyClaim = context.Model.FindEntityType(typeof(WarrantyClaim))!;
        var index = warrantyClaim.GetIndexes()
            .Single(item => item.GetDatabaseName() == "UX_WarrantyClaim_OpenProductSerialId");

        Assert.Equal(
            new[] { nameof(WarrantyClaim.ProductSerialId) },
            index.Properties.Select(property => property.Name));
        Assert.Equal(
            "[Status] <> N'Closed' AND [Status] <> N'Rejected'",
            index.GetFilter());
        Assert.Null(warrantyClaim.FindProperty("OpenProductSerialId"));
    }

    [Theory]
    [InlineData(typeof(SalesInvoice), "UX_SalesInvoice_StockOutId", nameof(SalesInvoice.StockOutId))]
    [InlineData(typeof(PurchaseInvoice), "UX_PurchaseInvoice_StockInId", nameof(PurchaseInvoice.StockInId))]
    public void Invoice_stock_document_link_uses_filtered_unique_index(
        Type entityType,
        string indexName,
        string propertyName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(local);Database=WareProModelTest;Integrated Security=True;Encrypt=False")
            .Options;
        using var context = new AppDbContext(options);

        var index = context.Model.FindEntityType(entityType)!
            .GetIndexes()
            .Single(item => item.GetDatabaseName() == indexName);

        Assert.True(index.IsUnique);
        Assert.Equal(new[] { propertyName }, index.Properties.Select(property => property.Name));
        Assert.Equal($"[{propertyName}] IS NOT NULL", index.GetFilter());
    }
}