using Moq;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.ViewModels;
using Xunit;
using System.Collections.Generic;
using System.Linq;

namespace QuanLyHangHoa.Tests.ViewModels;

public class ProductUnitViewModelTests
{
    [Fact]
    public void SaveCommand_AddsNewProductUnit_WhenSelectedProductUnitIsNull()
    {
        // Arrange
        var mockProductUnitService = new Mock<ProductUnitService>(new object[] { null! });
        var mockProductService = new Mock<ProductService>(new object[] { null! });
        var mockRefDataService = new Mock<ReferenceDataService>(new object[] { null! });

        var products = new List<Product> { new() { Id = 10, DisplayName = "May in" } };
        var units = new List<Unit> { new() { Id = 20, DisplayName = "Thung" } };

        mockProductService.Setup(s => s.GetAllProducts(It.IsAny<bool>())).Returns(products);
        mockRefDataService.Setup(s => s.GetAllUnits(It.IsAny<bool>())).Returns(units);
        mockProductUnitService.Setup(s => s.GetByProductId(It.IsAny<int>(), It.IsAny<bool>())).Returns(new List<ProductUnit>());

        var viewModel = new ProductUnitViewModel(
            mockProductUnitService.Object,
            mockProductService.Object,
            mockRefDataService.Object,
            Manager());
        viewModel.SelectedProduct = products.First();
        viewModel.SelectedUnitId = 20;
        viewModel.ConversionFactor = 12m;

        // Act
        viewModel.SaveCommand.Execute(null);

        // Assert
        mockProductUnitService.Verify(s => s.Add(It.Is<ProductUnit>(pu => 
            pu.ProductId == 10 && 
            pu.UnitId == 20 && 
            pu.ConversionFactor == 12m),
            2), Times.Once);
        Assert.Equal("Đã lưu đơn vị quy đổi.", viewModel.StatusMessage);
    }

    [Fact]
    public void SaveCommand_RejectsMissingProductOrUnit()
    {
        // Arrange
        var mockProductUnitService = new Mock<ProductUnitService>(new object[] { null! });
        var mockProductService = new Mock<ProductService>(new object[] { null! });
        var mockRefDataService = new Mock<ReferenceDataService>(new object[] { null! });

        var viewModel = new ProductUnitViewModel(
            mockProductUnitService.Object,
            mockProductService.Object,
            mockRefDataService.Object,
            Manager());

        // Act
        viewModel.SaveCommand.Execute(null);

        // Assert
        mockProductUnitService.Verify(
            s => s.Add(It.IsAny<ProductUnit>(), It.IsAny<int>()),
            Times.Never);
        Assert.Equal("Chưa chọn hàng hóa hoặc đơn vị.", viewModel.StatusMessage);
    }

    [Fact]
    public void Unauthorized_user_cannot_run_product_unit_mutation_commands()
    {
        var productUnitService = new Mock<ProductUnitService>(new object[] { null! });
        var productService = new Mock<ProductService>(new object[] { null! });
        var referenceDataService = new Mock<ReferenceDataService>(new object[] { null! });
        productService.Setup(service => service.GetAllProducts(It.IsAny<bool>()))
            .Returns([new Product { Id = 10, DisplayName = "Printer" }]);
        referenceDataService.Setup(service => service.GetAllUnits(It.IsAny<bool>()))
            .Returns([new Unit { Id = 20, DisplayName = "Box" }]);
        productUnitService
            .Setup(service => service.GetByProductId(It.IsAny<int>(), It.IsAny<bool>()))
            .Returns([]);
        var user = Manager();
        user.RoleCode = "Nhân viên bán hàng";

        var viewModel = new ProductUnitViewModel(
            productUnitService.Object,
            productService.Object,
            referenceDataService.Object,
            user);

        Assert.False(viewModel.SaveCommand.CanExecute(null));
        Assert.False(viewModel.DeleteCommand.CanExecute(null));
        Assert.False(viewModel.OpenAddUnitWindowCommand.CanExecute(null));
        productUnitService.Verify(
            service => service.Add(It.IsAny<ProductUnit>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public void Rejected_update_preserves_visible_product_unit_and_surfaces_error()
    {
        var product = new Product { Id = 10, DisplayName = "Printer" };
        var oldUnit = new Unit { Id = 20, DisplayName = "Box" };
        var newUnit = new Unit { Id = 21, DisplayName = "Pallet" };
        var existing = new ProductUnit
        {
            Id = 30,
            ProductId = product.Id,
            UnitId = oldUnit.Id,
            ConversionFactor = 12m,
            IsPurchaseUnit = true
        };
        var productUnitService = new Mock<ProductUnitService>(new object[] { null! });
        var productService = new Mock<ProductService>(new object[] { null! });
        var referenceDataService = new Mock<ReferenceDataService>(new object[] { null! });
        productService.Setup(service => service.GetAllProducts(It.IsAny<bool>()))
            .Returns([product]);
        referenceDataService.Setup(service => service.GetAllUnits(It.IsAny<bool>()))
            .Returns([oldUnit, newUnit]);
        productUnitService
            .Setup(service => service.GetByProductId(product.Id, It.IsAny<bool>()))
            .Returns([existing]);
        productUnitService
            .Setup(service => service.Update(It.IsAny<ProductUnit>(), 2))
            .Throws(new InvalidOperationException("not authorized"));
        var viewModel = new ProductUnitViewModel(
            productUnitService.Object,
            productService.Object,
            referenceDataService.Object,
            Manager());
        viewModel.SelectedProductUnit = existing;
        viewModel.SelectedUnitId = newUnit.Id;
        viewModel.ConversionFactor = 24m;

        viewModel.SaveCommand.Execute(null);

        Assert.Equal(oldUnit.Id, existing.UnitId);
        Assert.Equal(12m, existing.ConversionFactor);
        Assert.Contains("not authorized", viewModel.StatusMessage);
    }

    [Fact]
    public void Fresh_permission_callback_disables_all_mutation_commands()
    {
        var productUnitService = new Mock<ProductUnitService>(new object[] { null! });
        var productService = new Mock<ProductService>(new object[] { null! });
        var referenceDataService = new Mock<ReferenceDataService>(new object[] { null! });
        productService.Setup(service => service.GetAllProducts(It.IsAny<bool>()))
            .Returns([]);
        referenceDataService.Setup(service => service.GetAllUnits(It.IsAny<bool>()))
            .Returns([]);
        var authorized = true;
        var opened = false;
        var viewModel = new ProductUnitViewModel(
            productUnitService.Object,
            productService.Object,
            referenceDataService.Object,
            Manager(),
            () => opened = true,
            () => authorized);

        Assert.True(viewModel.SaveCommand.CanExecute(null));
        Assert.True(viewModel.DeleteCommand.CanExecute(null));
        Assert.True(viewModel.OpenAddUnitWindowCommand.CanExecute(null));

        authorized = false;

        Assert.False(viewModel.SaveCommand.CanExecute(null));
        Assert.False(viewModel.DeleteCommand.CanExecute(null));
        Assert.False(viewModel.OpenAddUnitWindowCommand.CanExecute(null));
        viewModel.OpenAddUnitWindowCommand.Execute(null);
        viewModel.SaveCommand.Execute(null);
        viewModel.DeleteCommand.Execute(null);
        Assert.False(opened);
        productUnitService.Verify(
            service => service.Add(It.IsAny<ProductUnit>(), It.IsAny<int>()),
            Times.Never);
        productUnitService.Verify(
            service => service.Delete(It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);
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
