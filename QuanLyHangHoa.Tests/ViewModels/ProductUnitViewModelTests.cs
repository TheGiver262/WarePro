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
        var mockProductUnitService = new Mock<ProductUnitService>();
        var mockProductService = new Mock<ProductService>();
        var mockRefDataService = new Mock<ReferenceDataService>();

        var products = new List<Product> { new() { Id = 10, DisplayName = "May in" } };
        var units = new List<Unit> { new() { Id = 20, DisplayName = "Thung" } };

        mockProductService.Setup(s => s.GetAllProducts()).Returns(products);
        mockRefDataService.Setup(s => s.GetAllUnits()).Returns(units);
        mockProductUnitService.Setup(s => s.GetByProductId(It.IsAny<int>())).Returns(new List<ProductUnit>());

        var viewModel = new ProductUnitViewModel(mockProductUnitService.Object, mockProductService.Object, mockRefDataService.Object);
        viewModel.SelectedProduct = products.First();
        viewModel.SelectedUnitId = 20;
        viewModel.ConversionFactor = 12m;

        // Act
        viewModel.SaveCommand.Execute(null);

        // Assert
        mockProductUnitService.Verify(s => s.Add(It.Is<ProductUnit>(pu => 
            pu.ProductId == 10 && 
            pu.UnitId == 20 && 
            pu.ConversionFactor == 12m)), Times.Once);
        Assert.Equal("Đã lưu đơn vị quy đổi.", viewModel.StatusMessage);
    }

    [Fact]
    public void SaveCommand_RejectsMissingProductOrUnit()
    {
        // Arrange
        var mockProductUnitService = new Mock<ProductUnitService>();
        var mockProductService = new Mock<ProductService>();
        var mockRefDataService = new Mock<ReferenceDataService>();

        var viewModel = new ProductUnitViewModel(mockProductUnitService.Object, mockProductService.Object, mockRefDataService.Object);

        // Act
        viewModel.SaveCommand.Execute(null);

        // Assert
        mockProductUnitService.Verify(s => s.Add(It.IsAny<ProductUnit>()), Times.Never);
        Assert.Equal("Chưa chọn hàng hóa hoặc đơn vị.", viewModel.StatusMessage);
    }
}
