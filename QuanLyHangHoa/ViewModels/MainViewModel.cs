using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Views;
using System.Windows.Controls;

namespace QuanLyHangHoa.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private Employee _currentUser;

        [ObservableProperty]
        private UserControl? _currentView;

        public MainViewModel(Employee user)
        {
            _currentUser = user;
            CurrentView = new ProductView();
        }

        // ── Core Operations ────────────────────────────────────────────────────
        [RelayCommand] private void OpenProductView()  => CurrentView = new ProductView();
        
        [RelayCommand] 
        private void OpenStockOutView()
        {
            var view = new StockOutView { DataContext = new StockOutViewModel(_currentUser) };
            CurrentView = view;
        }

        [RelayCommand]
        private void OpenStockInView()
        {
            var view = new StockInView { DataContext = new StockInViewModel(_currentUser) };
            CurrentView = view;
        }

        [RelayCommand]
        private void OpenStockAdjustmentView()
        {
            var view = new StockAdjustmentView { DataContext = new StockAdjustmentViewModel(_currentUser) };
            CurrentView = view;
        }

        [RelayCommand]
        private void OpenInvoiceView()
        {
            var view = new InvoiceView { DataContext = new InvoiceViewModel() };
            CurrentView = view;
        }

        [RelayCommand]
        private void OpenDebtReportView()
        {
            var view = new DebtReportView { DataContext = new DebtReportViewModel() };
            CurrentView = view;
        }

        [RelayCommand]
        private void OpenAuditQueryView()
        {
            var view = new AuditQueryView { DataContext = new AuditQueryViewModel() };
            CurrentView = view;
        }

        [RelayCommand] private void OpenWarrantyView() => CurrentView = new WarrantyView();

        // ── Reference Data ─────────────────────────────────────────────────────
        [RelayCommand] private void OpenUnitView()     => CurrentView = new UnitView();
        [RelayCommand] private void OpenCategoryView() => CurrentView = new CategoryView();
        [RelayCommand] private void OpenBrandView()    => CurrentView = new BrandView();
        [RelayCommand] private void OpenSupplierView() => CurrentView = new SupplierView();
        [RelayCommand] private void OpenCustomerView() => CurrentView = new CustomerView();

        // ── Administration ─────────────────────────────────────────────────────
        [RelayCommand]
        private void OpenEmployeeView()
        {
            if (_currentUser.Role == "Admin")
                CurrentView = new EmployeeView();
            else
                System.Windows.MessageBox.Show("Bạn không phải Admin!", "Cảnh Báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }

        [RelayCommand]
        private void Logout()
        {
            new LoginView().Show();
            foreach (System.Windows.Window window in System.Windows.Application.Current.Windows)
            {
                if (window is MainWindow) { window.Close(); break; }
            }
        }
    }
}
