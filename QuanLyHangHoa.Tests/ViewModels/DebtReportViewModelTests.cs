using QuanLyHangHoa.Services;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Tests.ViewModels
{
    public class DebtReportViewModelTests
    {
        [Fact]
        public void ConstructorLoadsCustomerDebtByDefault()
        {
            var viewModel = new DebtReportViewModel(
                () => new[] { new DebtSummary(1, "Customer A", 500m, 150m, 350m) },
                () => new[] { new DebtSummary(2, "Supplier A", 800m, 100m, 700m) });

            var summary = Assert.Single(viewModel.Summaries);
            Assert.True(viewModel.IsCustomerMode);
            Assert.Equal("Cong no khach hang", viewModel.ReportTitle);
            Assert.Equal("Customer A", summary.PartyName);
            Assert.Equal(350m, viewModel.TotalDebt);
        }

        [Fact]
        public void ShowSuppliersLoadsSupplierDebt()
        {
            var viewModel = new DebtReportViewModel(
                () => new[] { new DebtSummary(1, "Customer A", 500m, 150m, 350m) },
                () => new[]
                {
                    new DebtSummary(2, "Supplier A", 800m, 100m, 700m),
                    new DebtSummary(3, "Supplier B", 300m, 0m, 300m)
                });

            viewModel.ShowSuppliersCommand.Execute(null);

            Assert.False(viewModel.IsCustomerMode);
            Assert.Equal("Cong no nha cung cap", viewModel.ReportTitle);
            Assert.Equal(2, viewModel.Summaries.Count);
            Assert.Equal(1000m, viewModel.TotalDebt);
        }
    }
}
