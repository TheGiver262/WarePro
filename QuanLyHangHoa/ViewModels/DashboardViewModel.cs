using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using QuanLyHangHoa.Services;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyHangHoa.ViewModels
{
    public class InventoryLegendItem
    {
        public string CategoryName { get; set; } = string.Empty;
        public System.Windows.Media.Brush ColorBrush { get; set; } = System.Windows.Media.Brushes.Transparent;
        public decimal TotalValue { get; set; }
    }

    public partial class DashboardViewModel : ObservableObject, IRefreshable
    {
        private static readonly SKColor[] InventoryPalette =
        {
            new(37, 99, 235),
            new(16, 185, 129),
            new(245, 158, 11),
            new(239, 68, 68),
            new(20, 184, 166),
            new(14, 165, 233),
            new(132, 204, 22),
            new(249, 115, 22),
            new(100, 116, 139),
            new(234, 179, 8),
            new(5, 150, 105),
            new(2, 132, 199),
            new(71, 85, 105),
            new(220, 38, 38),
            new(13, 148, 136),
            new(77, 124, 15)
        };

        private readonly DashboardService _dashboardService;
        private readonly MainViewModel _mainViewModel;
        private Task? _initialLoadTask;
        private int _loadGeneration;

        [ObservableProperty]
        private DashboardStats _stats = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string? _loadErrorMessage;

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
        private ObservableCollection<InventoryLegendItem> _inventoryLegendItems = new();

        [ObservableProperty]
        private ISeries[] _topProductsSeries = System.Array.Empty<ISeries>();

        [ObservableProperty]
        private Axis[] _topProductsYAxes = System.Array.Empty<Axis>();

        public DashboardViewModel(DashboardService dashboardService, MainViewModel mainViewModel)
        {
            _dashboardService = dashboardService;
            _mainViewModel = mainViewModel;
            IsLoading = true;
        }

        // giữ lại task load đầu tiên để MainWindow có thể chờ mà không khởi chạy query trùng
        public Task EnsureLoadedAsync()
        {
            return _initialLoadTask ??= LoadStatsAsync();
        }

        [RelayCommand]
        // generation ngăn lượt refresh cũ hoàn tất muộn ghi đè dashboard mới
        public async Task LoadStatsAsync()
        {
            var generation = Interlocked.Increment(ref _loadGeneration);
            IsLoading = true;
            try
            {
                var stats = await _dashboardService.GetStatsAsync();
                if (generation != Volatile.Read(ref _loadGeneration))
                    return;

                Stats = stats;
                UpdateCharts();
                LoadErrorMessage = null;
            }
            catch (Exception ex)
            {
                if (generation == Volatile.Read(ref _loadGeneration))
                    LoadErrorMessage = ex.Message;
            }
            finally
            {
                if (generation == Volatile.Read(ref _loadGeneration))
                    IsLoading = false;
            }
        }

        // chuyển DTO service thành series/axis bind UI; không truy vấn database trong bước vẽ
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
            else
            {
                RevenueExpenseSeries = Array.Empty<ISeries>();
                RevenueExpenseXAxes = Array.Empty<Axis>();
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
            else
            {
                StockMovementSeries = Array.Empty<ISeries>();
                StockMovementXAxes = Array.Empty<Axis>();
            }

            // 3. Biểu đồ cơ cấu tồn kho theo danh mục (Pie/Doughnut Chart)
            if (Stats.InventoryStructureChart != null && Stats.InventoryStructureChart.Any())
            {
                var inventoryItems = Stats.InventoryStructureChart
                    .Where(item => item.TotalValue > 0)
                    .ToArray();
                InventoryPieSeries = inventoryItems.Select((x, index) =>
                {
                    var color = InventoryPalette[index % InventoryPalette.Length];
                    return new PieSeries<double>
                    {
                        Name = x.CategoryName,
                        Values = new double[] { (double)x.TotalValue },
                        Fill = new SolidColorPaint(color),
                        InnerRadius = 45,
                        OuterRadiusOffset = 0
                    };
                }).Cast<ISeries>().ToArray();

                InventoryLegendItems = new ObservableCollection<InventoryLegendItem>(
                    inventoryItems.Select((x, index) =>
                    {
                        var color = InventoryPalette[index % InventoryPalette.Length];
                        return new InventoryLegendItem
                        {
                            CategoryName = x.CategoryName,
                            ColorBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(color.Red, color.Green, color.Blue)),
                            TotalValue = x.TotalValue
                        };
                    }));
            }
            else
            {
                InventoryPieSeries = Array.Empty<ISeries>();
                InventoryLegendItems = new ObservableCollection<InventoryLegendItem>();
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
                        Fill = new SolidColorPaint(new SKColor(37, 99, 235))
                    }
                };
                TopProductsYAxes = new Axis[]
                {
                    new Axis { Labels = productNames }
                };
            }
            else
            {
                TopProductsSeries = Array.Empty<ISeries>();
                TopProductsYAxes = Array.Empty<Axis>();
            }
        }

        public void RefreshData() => _ = LoadStatsAsync();

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
