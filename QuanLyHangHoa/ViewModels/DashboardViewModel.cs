using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using QuanLyHangHoa.Services;
using SkiaSharp;
using System.Linq;
using System.Threading.Tasks;

namespace QuanLyHangHoa.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly DashboardService _dashboardService;
        private readonly MainViewModel _mainViewModel;

        [ObservableProperty]
        private DashboardStats _stats = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private ISeries[] _revenueExpenseSeries = System.Array.Empty<ISeries>();

        [ObservableProperty]
        private Axis[] _revenueExpenseXAxes = System.Array.Empty<Axis>();

        [ObservableProperty]
        private ISeries[] _stockMovementSeries = System.Array.Empty<ISeries>();

        [ObservableProperty]
        private Axis[] _stockMovementXAxes = System.Array.Empty<Axis>();

        [ObservableProperty]
        private ISeries[] _inventoryPieSeries = System.Array.Empty<ISeries>();

        [ObservableProperty]
        private ISeries[] _topProductsSeries = System.Array.Empty<ISeries>();

        [ObservableProperty]
        private Axis[] _topProductsYAxes = System.Array.Empty<Axis>();

        public DashboardViewModel(DashboardService dashboardService, MainViewModel mainViewModel)
        {
            _dashboardService = dashboardService;
            _mainViewModel = mainViewModel;
            _ = LoadStatsAsync();
        }

        [RelayCommand]
        public async Task LoadStatsAsync()
        {
            IsLoading = true;
            Stats = await _dashboardService.GetStatsAsync();
            UpdateCharts();
            IsLoading = false;
        }

        private void UpdateCharts()
        {
            // 1. Biểu đồ doanh thu & chi phí (Bar Chart)
            if (Stats.RevenueExpenseChart != null && Stats.RevenueExpenseChart.Any())
            {
                var months = Stats.RevenueExpenseChart.Select(x => x.Month).ToArray();
                var revenues = Stats.RevenueExpenseChart.Select(x => (double)x.Revenue).ToArray();
                var expenses = Stats.RevenueExpenseChart.Select(x => (double)x.Expense).ToArray();

                RevenueExpenseSeries = new ISeries[]
                {
                    new ColumnSeries<double>
                    {
                        Name = "Doanh thu",
                        Values = revenues,
                        Fill = new SolidColorPaint(SKColors.CornflowerBlue)
                    },
                    new ColumnSeries<double>
                    {
                        Name = "Chi phí",
                        Values = expenses,
                        Fill = new SolidColorPaint(SKColors.Tomato)
                    }
                };
                RevenueExpenseXAxes = new Axis[]
                {
                    new Axis 
                    { 
                        Labels = months,
                        LabelsRotation = 15
                    }
                };
            }

            // 2. Biểu đồ xu hướng nhập xuất kho (Line Chart)
            if (Stats.StockMovementChart != null && Stats.StockMovementChart.Any())
            {
                var dates = Stats.StockMovementChart.Select(x => x.Date).ToArray();
                var stockIns = Stats.StockMovementChart.Select(x => (double)x.StockInCount).ToArray();
                var stockOuts = Stats.StockMovementChart.Select(x => (double)x.StockOutCount).ToArray();

                StockMovementSeries = new ISeries[]
                {
                    new LineSeries<double>
                    {
                        Name = "Phiếu nhập",
                        Values = stockIns,
                        Stroke = new SolidColorPaint(SKColors.MediumSeaGreen) { StrokeThickness = 3 },
                        Fill = null,
                        GeometrySize = 8
                    },
                    new LineSeries<double>
                    {
                        Name = "Phiếu xuất",
                        Values = stockOuts,
                        Stroke = new SolidColorPaint(SKColors.DodgerBlue) { StrokeThickness = 3 },
                        Fill = null,
                        GeometrySize = 8
                    }
                };
                StockMovementXAxes = new Axis[]
                {
                    new Axis { Labels = dates }
                };
            }

            // 3. Biểu đồ cơ cấu tồn kho theo danh mục (Pie/Doughnut Chart)
            if (Stats.InventoryStructureChart != null && Stats.InventoryStructureChart.Any())
            {
                InventoryPieSeries = Stats.InventoryStructureChart.Select(x => new PieSeries<double>
                {
                    Name = x.CategoryName,
                    Values = new double[] { (double)x.TotalValue },
                    InnerRadius = 45, // Tạo hiệu ứng Doughnut
                    OuterRadiusOffset = 0
                }).Cast<ISeries>().ToArray();
            }

            // 4. Biểu đồ top 5 sản phẩm bán chạy (Horizontal Bar / Row Chart)
            if (Stats.TopSellingProductsChart != null && Stats.TopSellingProductsChart.Any())
            {
                var productNames = Stats.TopSellingProductsChart.Select(x => x.ProductName).Reverse().ToArray();
                var solds = Stats.TopSellingProductsChart.Select(x => (double)x.TotalSold).Reverse().ToArray();

                TopProductsSeries = new ISeries[]
                {
                    new RowSeries<double>
                    {
                        Name = "Số lượng đã bán",
                        Values = solds,
                        Fill = new SolidColorPaint(SKColors.MediumPurple)
                    }
                };
                TopProductsYAxes = new Axis[]
                {
                    new Axis { Labels = productNames }
                };
            }
        }

        [RelayCommand]
        private void NavigateToProducts() => _mainViewModel.OpenProductViewCommand.Execute(null);

        [RelayCommand]
        private void NavigateToStockIn() => _mainViewModel.OpenStockInViewCommand.Execute(null);

        [RelayCommand]
        private void NavigateToSalesInvoices() => _mainViewModel.OpenSalesInvoiceViewCommand.Execute(null);

        [RelayCommand]
        private void NavigateToPurchaseInvoices() => _mainViewModel.OpenPurchaseInvoiceViewCommand.Execute(null);

        [RelayCommand]
        private void NavigateToWarranty() => _mainViewModel.OpenWarrantyViewCommand.Execute(null);
    }
}
