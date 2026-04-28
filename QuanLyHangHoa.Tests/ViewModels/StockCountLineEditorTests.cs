using QuanLyHangHoa.Models;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Tests.ViewModels
{
    public class StockCountLineEditorTests
    {
        [Fact]
        public void ToInputUsesSelectedProductAndCountedQuantity()
        {
            var editor = new StockCountLineEditor
            {
                SelectedProduct = new Product { Id = 77, Name = "Monitor" },
                CountedQuantity = 12m
            };

            var input = editor.ToInput();

            Assert.Equal(77, input.ProductId);
            Assert.Equal(12m, input.CountedQuantity);
        }
    }
}
