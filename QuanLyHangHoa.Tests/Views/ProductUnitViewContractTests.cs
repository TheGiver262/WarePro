using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Tests.Views;

public class ProductUnitViewContractTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void Product_unit_view_binds_only_real_view_model_and_model_members()
    {
        var xaml = File.ReadAllText(Path.Combine(
            RepoRoot,
            "QuanLyHangHoa",
            "Views",
            "ProductUnitView.xaml"));

        Assert.Contains("SearchText, UpdateSourceTrigger=PropertyChanged", xaml);
        Assert.Contains("{Binding RefreshCommand}", xaml);
        Assert.Contains("{Binding OpenAddUnitWindowCommand}", xaml);
        Assert.Contains("SelectedItem", xaml);
        Assert.Contains("{Binding SelectedProductUnit}", xaml);
        Assert.Contains("Unit.DisplayName", xaml);
        Assert.Contains("ConversionFactor, StringFormat='1 : {0}'", xaml);
        Assert.Contains("DataContext.DeleteCommand", xaml);
        Assert.DoesNotContain("Unit.Name", xaml);
        Assert.DoesNotContain("{Binding Ratio", xaml);
        Assert.DoesNotContain("DeleteUnitCommand", xaml);
    }

    [Fact]
    public void Product_unit_commands_and_main_navigation_route_are_generated()
    {
        Assert.NotNull(typeof(ProductUnitViewModel).GetProperty("SearchText"));
        Assert.NotNull(typeof(ProductUnitViewModel).GetProperty("RefreshCommand"));
        Assert.NotNull(typeof(ProductUnitViewModel).GetProperty("OpenAddUnitWindowCommand"));
        Assert.NotNull(typeof(ProductUnitViewModel).GetProperty("DeleteCommand"));
        Assert.NotNull(typeof(MainViewModel).GetProperty("OpenProductUnitViewCommand"));
        var mainWindow = File.ReadAllText(Path.Combine(RepoRoot, "QuanLyHangHoa", "MainWindow.xaml"));
        Assert.Contains("{Binding OpenProductUnitViewCommand}", mainWindow);
    }

    [Fact]
    public void Search_filters_loaded_product_units_without_requerying_the_service()
    {
        var product = new Product { Id = 10, ProductCode = "P10", DisplayName = "Printer" };
        var unit = new Unit { Id = 20, UnitCode = "BOX", DisplayName = "Box", IsActive = true };
        var productUnit = new ProductUnit
        {
            Id = 30,
            ProductId = product.Id,
            UnitId = unit.Id,
            Product = product,
            Unit = unit,
            ConversionFactor = 12m
        };
        var productUnitService = new Mock<ProductUnitService>(new object[] { null! });
        var productService = new Mock<ProductService>(new object[] { null! });
        var referenceDataService = new Mock<ReferenceDataService>(new object[] { null! });
        productService.Setup(service => service.GetAllProducts(It.IsAny<bool>())).Returns([product]);
        referenceDataService.Setup(service => service.GetAllUnits(It.IsAny<bool>())).Returns([unit]);
        productUnitService
            .Setup(service => service.GetByProductId(product.Id, It.IsAny<bool>()))
            .Returns([productUnit]);
        productUnitService.Setup(service => service.DeleteAsync(
                productUnit.Id, It.IsAny<byte[]>(), 2, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var viewModel = new ProductUnitViewModel(
            productUnitService.Object,
            productService.Object,
            referenceDataService.Object,
            Manager());

        Assert.Same(product, viewModel.SelectedProduct);
        Assert.Single(viewModel.ProductUnits);

        viewModel.SearchText = "missing";
        Assert.Empty(viewModel.ProductUnits);

        viewModel.SearchText = "box";
        Assert.Same(productUnit, Assert.Single(viewModel.ProductUnits));
        productUnitService.Verify(
            service => service.GetByProductId(product.Id, It.IsAny<bool>()),
            Times.Once);
    }

    [Fact]
    public void Add_unit_command_uses_the_injected_navigation_action()
    {
        var productUnitService = new Mock<ProductUnitService>(new object[] { null! });
        var productService = new Mock<ProductService>(new object[] { null! });
        var referenceDataService = new Mock<ReferenceDataService>(new object[] { null! });
        productService.Setup(service => service.GetAllProducts(It.IsAny<bool>()))
            .Returns(new List<Product>());
        referenceDataService.Setup(service => service.GetAllUnits(It.IsAny<bool>()))
            .Returns(new List<Unit>());
        var opened = false;
        var viewModel = new ProductUnitViewModel(
            productUnitService.Object,
            productService.Object,
            referenceDataService.Object,
            Manager(),
            () => opened = true);

        viewModel.OpenAddUnitWindowCommand.Execute(null);

        Assert.True(opened);
    }

    [Fact]
    public async Task Delete_command_uses_the_row_parameter()
    {
        var product = new Product { Id = 10, ProductCode = "P10", DisplayName = "Printer" };
        var unit = new Unit { Id = 20, UnitCode = "BOX", DisplayName = "Box", IsActive = true };
        var productUnit = new ProductUnit
        {
            Id = 30,
            ProductId = product.Id,
            UnitId = unit.Id,
            Product = product,
            Unit = unit,
            ConversionFactor = 12m
        };
        var productUnitService = new Mock<ProductUnitService>(new object[] { null! });
        var productService = new Mock<ProductService>(new object[] { null! });
        var referenceDataService = new Mock<ReferenceDataService>(new object[] { null! });
        productService.Setup(service => service.GetAllProducts(It.IsAny<bool>())).Returns([product]);
        referenceDataService.Setup(service => service.GetAllUnits(It.IsAny<bool>())).Returns([unit]);
        productUnitService
            .Setup(service => service.GetByProductId(product.Id, It.IsAny<bool>()))
            .Returns([productUnit]);
        productUnitService.Setup(service => service.DeleteAsync(
                productUnit.Id, It.IsAny<byte[]>(), 2, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var viewModel = new ProductUnitViewModel(
            productUnitService.Object,
            productService.Object,
            referenceDataService.Object,
            Manager());
        viewModel.SelectedProductUnit = null;

        await viewModel.DeleteCommand.ExecuteAsync(productUnit);

        productUnitService.Verify(
            service => service.DeleteAsync(
                productUnit.Id,
                It.IsAny<byte[]>(),
                2,
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static AppUser Manager() => new()
    {
        Id = 2,
        Username = "manager",
        PasswordHash = "hash",
        FullName = "Manager",
        RoleCode = "Quản lý",
        IsActive = true
    };
}
