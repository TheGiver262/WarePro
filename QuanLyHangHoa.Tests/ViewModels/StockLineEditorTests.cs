using Microsoft.Data.Sqlite;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;
using QuanLyHangHoa.ViewModels;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QuanLyHangHoa.Tests.ViewModels;

public sealed class StockLineEditorTests
{
    [Fact]
    public void StockIn_editor_uses_base_quantity_and_replacing_serials_preserves_quantity()
    {
        using var connection = CreateDatabase();
        var editor = new StockInLineEditor(new ProductUnitService(() => DatabaseHelper.CreateContext(connection)));
        var product = LoadProduct(connection);
        var box = LoadBox(connection);
        editor.SelectedProduct = product;
        editor.Quantity = 2m;
        var changed = new List<string?>();
        editor.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        editor.SelectedUnit = box;
        editor.ReplaceSerials(Enumerable.Range(1, 20).Select(i => $"IN-{i}"));

        Assert.Equal(20m, editor.BaseQuantity);
        Assert.True(editor.IsSerialComplete);
        Assert.Equal(2m, editor.Quantity);
        Assert.Contains(nameof(editor.IsSerialComplete), changed);
    }

    [Fact]
    public void StockOut_editor_uses_base_quantity_and_replacing_serials_preserves_quantity()
    {
        using var connection = CreateDatabase();
        var editor = new StockOutLineEditor(new ProductUnitService(() => DatabaseHelper.CreateContext(connection)));
        var product = LoadProduct(connection);
        var box = LoadBox(connection);
        editor.SelectedProduct = product;
        editor.Quantity = 2m;
        var changed = new List<string?>();
        editor.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        editor.SelectedUnit = box;
        editor.ReplaceSerials(Enumerable.Range(1, 20).Select(i => $"OUT-{i}"));

        Assert.Equal(20m, editor.BaseQuantity);
        Assert.True(editor.IsSerialComplete);
        Assert.Equal(2m, editor.Quantity);
        Assert.Contains(nameof(editor.IsSerialComplete), changed);
    }

    private static SqliteConnection CreateDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = DatabaseHelper.CreateContext(connection);
        DatabaseHelper.SeedBasicData(db);
        db.Units.Add(new Unit { Id = 2, UnitCode = "BOX", DisplayName = "Box", IsActive = true });
        db.Products.Add(new Product
        {
            Id = 920,
            ProductCode = "P920",
            DisplayName = "Editor product",
            CategoryId = 1,
            BrandId = 1,
            DefaultUnitId = 1,
            DefaultPrice = 1m,
            IsSerialTracked = true
        });
        db.ProductUnits.Add(new ProductUnit
        {
            ProductId = 920,
            UnitId = 2,
            ConversionFactor = 10m,
            IsPurchaseUnit = true,
            IsSalesUnit = true
        });
        db.SaveChanges();
        return connection;
    }

    private static Product LoadProduct(SqliteConnection connection)
    {
        using var db = DatabaseHelper.CreateContext(connection);
        return db.Products.Single(product => product.Id == 920);
    }

    private static Unit LoadBox(SqliteConnection connection)
    {
        using var db = DatabaseHelper.CreateContext(connection);
        return db.Units.Single(unit => unit.Id == 2);
    }
}
