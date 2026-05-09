using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Views;
using QuanLyHangHoa.Services;
using System.Windows.Controls;

namespace QuanLyHangHoa.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private AppUser _currentUser;

        [ObservableProperty]
        private UserControl? _currentView;

        [ObservableProperty]
        private string _currentViewTitle = "DASHBOARD";

        [ObservableProperty]
        private string _currentViewSubtitle = "Tổng quan hoạt động kinh doanh";

        public bool IsAdmin => AuthorizationService.CanPerform(CurrentUser, PermissionAction.ManageUsers);
        public bool CanViewLogs => AuthorizationService.CanPerform(CurrentUser, PermissionAction.ManageAuditLogs);

        public Func<Data.AppDbContext> ContextFactory { get; }
        private readonly DashboardService _dashboardService;

        public MainViewModel(AppUser user, Func<Data.AppDbContext> contextFactory)
        {
            CurrentUser = user;
            ContextFactory = contextFactory;
            _dashboardService = new DashboardService(ContextFactory);
            
            OpenDashboard();
        }

        // ── Navigation Commands ────────────────────────────────────────────────
        [RelayCommand]
        private void OpenDashboard()
        {
            CurrentView = new DashboardView { DataContext = new DashboardViewModel(_dashboardService, this) };
            CurrentViewTitle = "DASHBOARD";
            CurrentViewSubtitle = "Tổng quan hoạt động kinh doanh";
        }

        [RelayCommand]
        private void OpenProductView()
        {
            CurrentView = new ProductView { DataContext = new ProductViewModel(ContextFactory, CurrentUser) };
            CurrentViewTitle = "KHO HÀNG";
            CurrentViewSubtitle = "Quản lý danh mục sản phẩm và tồn kho";
        }

        [RelayCommand]
        private void OpenStockOutView()
        {
            var view = new StockOutView { DataContext = new StockOutViewModel(CurrentUser) };
            CurrentView = view;
            CurrentViewTitle = "XUẤT KHO";
            CurrentViewSubtitle = "Lập phiếu xuất kho và quản lý hàng xuất";
        }

        [RelayCommand]
        private void OpenStockInView()
        {
            var view = new StockInView { DataContext = new StockInViewModel(CurrentUser) };
            CurrentView = view;
            CurrentViewTitle = "NHẬP KHO";
            CurrentViewSubtitle = "Lập phiếu nhập kho và quản lý hàng nhập";
        }

        [RelayCommand]
        private void OpenStockAdjustmentView()
        {
            var view = new StockAdjustmentView { DataContext = new StockAdjustmentViewModel(CurrentUser) };
            CurrentView = view;
            CurrentViewTitle = "ĐIỀU CHỈNH";
            CurrentViewSubtitle = "Điều chỉnh số lượng tồn kho thực tế";
        }

        [RelayCommand]
        private void OpenStockCountView()
        {
            var view = new StockCountView { DataContext = new StockCountViewModel(CurrentUser) };
            CurrentView = view;
            CurrentViewTitle = "KIỂM KÊ";
            CurrentViewSubtitle = "Kiểm kê định kỳ và đối soát hàng hóa";
        }

        [RelayCommand]
        private void OpenPurchaseInvoiceView()
        {
            CurrentView = new PurchaseInvoiceView { DataContext = new PurchaseInvoiceViewModel(this) };
            CurrentViewTitle = "HÓA ĐƠN MUA";
            CurrentViewSubtitle = "Quản lý hóa đơn nhập hàng từ NCC";
        }

        [RelayCommand]
        private void OpenSalesInvoiceView()
        {
            CurrentView = new SalesInvoiceView { DataContext = new SalesInvoiceViewModel(this) };
            CurrentViewTitle = "HÓA ĐƠN BÁN";
            CurrentViewSubtitle = "Quản lý hóa đơn bán lẻ cho khách hàng";
        }



        [RelayCommand]
        private void OpenWarrantyView()
        {
            var view = new WarrantyView { DataContext = new WarrantyViewModel(CurrentUser, ContextFactory) };
            CurrentView = view;
            CurrentViewTitle = "BẢO HÀNH";
            CurrentViewSubtitle = "Quản lý phiếu bảo hành và sửa chữa";
        }

        // ── Reference Data ─────────────────────────────────────────────────────
        [RelayCommand]
        private void OpenCategoryView()
        {
            CurrentView = new CategoryView { DataContext = new CategoryViewModel(ContextFactory, CurrentUser!) };
            CurrentViewTitle = "DANH MỤC";
            CurrentViewSubtitle = "Quản lý nhóm phân loại sản phẩm";
        }

        [RelayCommand]
        private void OpenBrandView()
        {
            CurrentView = new BrandView { DataContext = new BrandViewModel(ContextFactory, CurrentUser!) };
            CurrentViewTitle = "THƯƠNG HIỆU";
            CurrentViewSubtitle = "Quản lý các hãng sản xuất";
        }

        [RelayCommand]
        private void OpenUnitView()
        {
            CurrentView = new UnitView { DataContext = new UnitViewModel(ContextFactory, CurrentUser!) };
            CurrentViewTitle = "ĐƠN VỊ TÍNH";
            CurrentViewSubtitle = "Quản lý đơn vị đo lường";
        }

        [RelayCommand]
        private void OpenSupplierView()
        {
            CurrentView = new SupplierView { DataContext = new SupplierViewModel(ContextFactory, CurrentUser!) };
            CurrentViewTitle = "NHÀ CUNG CẤP";
            CurrentViewSubtitle = "Quản lý đối tác nhập hàng";
        }

        [RelayCommand]
        private void OpenCustomerView()
        {
            CurrentView = new CustomerView { DataContext = new CustomerViewModel(ContextFactory, CurrentUser!) };
            CurrentViewTitle = "KHÁCH HÀNG";
            CurrentViewSubtitle = "Quản lý thông tin khách hàng";
        }

        [RelayCommand]
        private void OpenInventoryView()
        {
            CurrentView = new InventoryView();
            CurrentViewTitle = "TỒN KHO";
            CurrentViewSubtitle = "Theo dõi số lượng và giá trị hàng hóa hiện có";
        }

        [RelayCommand]
        private void OpenProductSerialView()
        {
            CurrentView = new ProductSerialView { DataContext = new ProductSerialViewModel(ContextFactory, CurrentUser) };
            CurrentViewTitle = "QUẢN LÝ SERIAL";
            CurrentViewSubtitle = "Quản lý số Serial và IMEI sản phẩm";
        }

        [RelayCommand]
        private void OpenWarrantyCoverageView()
        {
            CurrentView = new WarrantyCoverageView();
            CurrentViewTitle = "QUYỀN BẢO HÀNH";
            CurrentViewSubtitle = "Thiết lập các gói và điều kiện bảo hành";
        }

        [RelayCommand]
        private void OpenReportView()
        {
            CurrentView = new ReportView { DataContext = new ReportViewModel() };
            CurrentViewTitle = "BÁO CÁO";
            CurrentViewSubtitle = "Phân tích hiệu quả kinh doanh và tài chính";
        }

        // ── Administration ─────────────────────────────────────────────────────
        [RelayCommand]
        private void OpenAppUserView()
        {
            if (IsAdmin)
            {
                CurrentView = new AppUserView { DataContext = new AppUserViewModel(CurrentUser, ContextFactory) };
                CurrentViewTitle = "NGƯỜI DÙNG";
                CurrentViewSubtitle = "Quản lý tài khoản hệ thống";
            }
            else
            {
                System.Windows.MessageBox.Show("Bạn không có quyền truy cập!", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        private void OpenAuditLogView()
        {
            if (CanViewLogs)
            {
                CurrentView = new AuditLogView { DataContext = new AuditLogViewModel(ContextFactory) };
                CurrentViewTitle = "NHẬT KÝ HỆ THỐNG";
                CurrentViewSubtitle = "Theo dõi lịch sử thay đổi dữ liệu toàn hệ thống";
            }
            else
            {
                System.Windows.MessageBox.Show("Bạn không có quyền truy cập!", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        private void OpenChangePasswordView()
        {
            var view = new ChangePasswordView { DataContext = new ChangePasswordViewModel(CurrentUser, ContextFactory) };
            CurrentView = view;
            CurrentViewTitle = "ĐỔI MẬT KHẨU";
            CurrentViewSubtitle = "Cập nhật mật khẩu truy cập";
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
