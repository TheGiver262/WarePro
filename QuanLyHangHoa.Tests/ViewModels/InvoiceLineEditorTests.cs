using QuanLyHangHoa.Models;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Tests.ViewModels
{
    public class InvoiceLineEditorTests
    {
        [Fact]
        public void SelectingProductPrefillsUnitPriceAndUnitName()
        {
            var product = new Product
            {
                Id = 1,
                Name = "Laptop",
                UnitId = 3,
                UnitPrice = 1250m,
                Unit = new Unit { Id = 3, Name = "Cai" }
            };

            var line = new InvoiceLineEditor
            {
                SelectedProduct = product
            };

            Assert.Equal(1250m, line.UnitPrice);
            Assert.Equal("Cai", line.UnitName);
            Assert.Equal(3, line.UnitId);
        }

        [Fact]
        public void RecalculatesTotalsWhenValuesChange()
        {
            var line = new InvoiceLineEditor
            {
                Quantity = 2m,
                UnitPrice = 100m,
                TaxRate = 0.1m
            };

            Assert.Equal(200m, line.SubTotal);
            Assert.Equal(20m, line.TaxAmount);
            Assert.Equal(220m, line.GrandTotal);

            line.Quantity = 3m;

            Assert.Equal(300m, line.SubTotal);
            Assert.Equal(30m, line.TaxAmount);
            Assert.Equal(330m, line.GrandTotal);
        }
    }
}
