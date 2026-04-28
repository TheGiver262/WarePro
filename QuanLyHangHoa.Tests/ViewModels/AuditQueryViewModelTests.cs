using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Tests.ViewModels
{
    public class AuditQueryViewModelTests
    {
        [Fact]
        public void LoadProductLedgerUsesSelectedProduct()
        {
            var product = new Product { Id = 7, Name = "Laptop" };
            var entry = new AuditTimelineEntry(
                AuditTimelineEntryKind.StockLedger,
                Guid.NewGuid(),
                new DateTime(2026, 4, 28),
                "IN",
                1,
                7,
                1,
                3);

            var viewModel = new AuditQueryViewModel(
                () => new[] { product },
                _ => new[] { entry },
                _ => Array.Empty<AuditTimelineEntry>());
            viewModel.SelectedProduct = product;

            viewModel.LoadProductLedgerCommand.Execute(null);

            var loaded = Assert.Single(viewModel.Entries);
            Assert.Equal(entry, loaded);
            Assert.Equal("Lich su san pham: Laptop", viewModel.ReportTitle);
        }

        [Fact]
        public void LoadDocumentTimelineRejectsInvalidDocumentId()
        {
            var viewModel = new AuditQueryViewModel(
                () => Array.Empty<Product>(),
                _ => Array.Empty<AuditTimelineEntry>(),
                _ => Array.Empty<AuditTimelineEntry>());

            viewModel.DocumentIdText = "not-a-guid";
            viewModel.LoadDocumentTimelineCommand.Execute(null);

            Assert.Empty(viewModel.Entries);
            Assert.Equal("DocumentId khong hop le.", viewModel.StatusMessage);
        }
    }
}
