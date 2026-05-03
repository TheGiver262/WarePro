using QuanLyHangHoa.Models;
using QuanLyHangHoa.ViewModels;
using Xunit;

namespace QuanLyHangHoa.Tests.ViewModels
{
    public class StockCountLineEditorTests
    {
        [Fact]
        public void Editor_holds_selected_product_and_quantity()
        {
            var editor = new StockCountLineEditor
            {
                SelectedProduct = new Product { Id = 77, ProductCode = "P77", DisplayName = "Monitor" },
                CountedQuantity = 12m
            };

            Assert.Equal(77, editor.SelectedProduct.Id);
            Assert.Equal(12m, editor.CountedQuantity);
        }
    }
}
