using QuanLyHangHoa.Models;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Tests.ViewModels;

public class ProductUnitViewModelTests
{
    [Fact]
    public void SaveProductUnitPassesSelectedProductUnitAndRateToService()
    {
        ProductUnit? savedProductUnit = null;
        var viewModel = new ProductUnitViewModel(
            () => new List<Product> { new() { Id = 10, Name = "May in" } },
            () => new List<Unit> { new() { Id = 20, Name = "Thung" } },
            _ => new List<ProductUnit>(),
            productUnit => savedProductUnit = productUnit,
            _ => { },
            (_, _) => { });
        viewModel.SelectedProduct = viewModel.AvailableProducts.Single();
        viewModel.SelectedUnit = viewModel.AvailableUnits.Single();
        viewModel.ConversionRateToBaseUnit = 12m;
        viewModel.IsBaseUnit = false;

        viewModel.SaveProductUnitCommand.Execute(null);

        Assert.NotNull(savedProductUnit);
        Assert.Equal(10, savedProductUnit.ProductId);
        Assert.Equal(20, savedProductUnit.UnitId);
        Assert.Equal(12m, savedProductUnit.ConversionRateToBaseUnit);
        Assert.False(savedProductUnit.IsBaseUnit);
        Assert.Equal("Da luu don vi quy doi.", viewModel.StatusMessage);
    }

    [Fact]
    public void SaveProductUnitRejectsMissingProductOrUnit()
    {
        var called = false;
        var viewModel = new ProductUnitViewModel(
            () => new List<Product>(),
            () => new List<Unit>(),
            _ => new List<ProductUnit>(),
            _ => called = true,
            _ => { },
            (_, _) => { });

        viewModel.SaveProductUnitCommand.Execute(null);

        Assert.False(called);
        Assert.Equal("Chua chon hang hoa hoac don vi.", viewModel.StatusMessage);
    }
}
