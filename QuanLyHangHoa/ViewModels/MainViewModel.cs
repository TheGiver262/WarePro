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
        private AppUser _currentUser;

        [ObservableProperty]
        private UserControl? _currentView;

        private readonly Data.AppDbContext _dbContext;
        public MainViewModel(AppUser user, Data.AppDbContext dbContext)
        {
            CurrentUser = user;
            _dbContext = dbContext;
            CurrentView = new ProductView();
        }

        // ── Core Operations ────────────────────────────────────────────────────
        [RelayCommand] private void OpenProductView()  => CurrentView = new ProductView();
        
        [RelayCommand] 
        private void OpenStockOutView()
        {
            var view = new StockOutView { DataContext = new StockOutViewModel(CurrentUser) };
            CurrentView = view;
        }

        [RelayCommand]
        private void OpenStockInView()
        {
            var view = new StockInView { DataContext = new StockInViewModel(CurrentUser) };
            CurrentView = view;
        }

        [RelayCommand]
        private void OpenOpeningBalanceImportView()
        {
            CurrentView = new OpeningBalanceImportView(CurrentUser.Id);
        }

        [RelayCommand]
        private void OpenStockAdjustmentView()
        {
            var view = new StockAdjustmentView { DataContext = new StockAdjustmentViewModel(CurrentUser) };
            CurrentView = view;
        }

        [RelayCommand]
        private void OpenStockCountView()
        {
            var view = new StockCountView { DataContext = new StockCountViewModel(CurrentUser) };
            CurrentView = view;
        }

        [RelayCommand]
        private void OpenInvoiceView()
        {
            var view = new InvoiceView { DataContext = new InvoiceViewModel() };
            CurrentView = view;
        }

        [RelayCommand]
        private void OpenInvoicePaymentView()
        {
            var view = new InvoicePaymentView { DataContext = new InvoicePaymentViewModel(CurrentUser) };
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

        [RelayCommand]
        private void OpenStockReversalView()
        {
            var view = new StockReversalView { DataContext = new StockReversalViewModel(CurrentUser) };
            CurrentView = view;
        }

        [RelayCommand]
        private void OpenWarrantyView()
        {
            var view = new WarrantyView { DataContext = new WarrantyViewModel(CurrentUser, _dbContext) };
            CurrentView = view;
        }

        // ── Reference Data ─────────────────────────────────────────────────────
        [RelayCommand] private void OpenUnitView()     => CurrentView = new UnitView();
        [RelayCommand] private void OpenProductUnitView() => CurrentView = new ProductUnitView();
        [RelayCommand] private void OpenProductSerialView() => CurrentView = new ProductSerialView();
        [RelayCommand] private void OpenCategoryView() => CurrentView = new CategoryView();
        [RelayCommand] private void OpenBrandView()    => CurrentView = new BrandView();
        [RelayCommand] private void OpenSupplierView() => CurrentView = new SupplierView();
        [RelayCommand] private void OpenCustomerView() => CurrentView = new CustomerView();

        // ── Administration ─────────────────────────────────────────────────────
        [RelayCommand]
        private void OpenChangePasswordView()
        {
            var view = new ChangePasswordView { DataContext = new ChangePasswordViewModel(CurrentUser) };
            CurrentView = view;
        }

        [RelayCommand]
        private void OpenAppUserView()
        {
            if (CurrentUser.RoleCode == "Admin")
                CurrentView = new AppUserView { DataContext = new AppUserViewModel(CurrentUser, _dbContext) };
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
