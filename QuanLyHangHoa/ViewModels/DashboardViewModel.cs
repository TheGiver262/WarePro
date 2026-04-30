using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Services;
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
            IsLoading = false;
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
