using QuanLyHangHoa.Models;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Tests.ViewModels
{
    public class StockAdjustmentLineEditorTests
    {
        [Fact]
        public void PositiveQuantityDeltaCreatesInboundLine()
        {
            var line = new StockAdjustmentLineEditor
            {
                SelectedProduct = new Product { Id = 10, Name = "Mouse" },
                QuantityDelta = 4m
            };

            var model = line.ToAdjustmentLine();

            Assert.Equal(10, model.ProductId);
            Assert.Equal(4m, model.QuantityDelta);
            Assert.Equal(4m, model.BaseQuantityDelta);
            Assert.Equal("In", model.Direction);
        }

        [Fact]
        public void NegativeQuantityDeltaCreatesOutboundLine()
        {
            var line = new StockAdjustmentLineEditor
            {
                SelectedProduct = new Product { Id = 11, Name = "Keyboard" },
                QuantityDelta = -2m
            };

            var model = line.ToAdjustmentLine();

            Assert.Equal(11, model.ProductId);
            Assert.Equal(-2m, model.QuantityDelta);
            Assert.Equal(-2m, model.BaseQuantityDelta);
            Assert.Equal("Out", model.Direction);
        }
    }
}
