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

        [ObservableProperty]
        private bool _isSidebarCollapsed;

        public bool IsAdmin => AuthorizationService.CanPerform(CurrentUser, PermissionAction.ManageUsers);
        public bool CanViewLogs => AuthorizationService.CanPerform(CurrentUser, PermissionAction.ManageAuditLogs);

        public Func<Data.AppDbContext> ContextFactory { get; }
        private readonly DashboardService _dashboardService;
        private readonly System.Collections.Generic.Dictionary<string, UserControl> _viewCache = new();

        public MainViewModel(AppUser user, Func<Data.AppDbContext> contextFactory)
        {
            CurrentUser = user;
            ContextFactory = contextFactory;
            _dashboardService = new DashboardService(ContextFactory);
            
            OpenDashboard();
        }

        private void NavigateToView<TView>(string cacheKey, Func<TView> viewFactory, string title, string subtitle) where TView : UserControl
        {
            if (!_viewCache.TryGetValue(cacheKey, out var view))
            {
                view = viewFactory();
                _viewCache[cacheKey] = view;
            }
            else
            {
                if (view.DataContext is IRefreshable refreshable)
                {
                    refreshable.RefreshData();
                }
            }
            CurrentView = view;
            CurrentViewTitle = title;
            CurrentViewSubtitle = subtitle;
        }

        // ── Navigation Commands ────────────────────────────────────────────────
        [RelayCommand]
        private void OpenDashboard()
        {
            NavigateToView("Dashboard", () => new DashboardView { DataContext = new DashboardViewModel(_dashboardService, this) }, "DASHBOARD", "Tổng quan hoạt động kinh doanh");
        }

        [RelayCommand]
        private void OpenProductView()
        {
            NavigateToView("Product", () => new ProductView { DataContext = new ProductViewModel(ContextFactory, CurrentUser) }, "KHO HÀNG", "Quản lý danh mục sản phẩm và tồn kho");
        }

        [RelayCommand]
        private void OpenStockOutView()
        {
            NavigateToView("StockOut", () => new StockOutView { DataContext = new StockOutViewModel(CurrentUser, ContextFactory) }, "XUẤT KHO", "Lập phiếu xuất kho và quản lý hàng xuất");
        }

        [RelayCommand]
        private void OpenStockInView()
        {
            NavigateToView("StockIn", () => new StockInView { DataContext = new StockInViewModel(CurrentUser, ContextFactory) }, "NHẬP KHO", "Lập phiếu nhập kho và quản lý hàng nhập");
        }

        [RelayCommand]
        private void OpenStockTransferView()
        {
            NavigateToView("StockTransfer", () => new StockTransferView { DataContext = new StockTransferViewModel(CurrentUser, ContextFactory) }, "CHUYỂN KHO", "Điều chuyển hàng hóa giữa các kho nội bộ");
        }

        [RelayCommand]
        private void OpenStockAdjustmentView()
        {
            NavigateToView("StockAdjustment", () => new StockAdjustmentView { DataContext = new StockAdjustmentViewModel(CurrentUser, ContextFactory) }, "ĐIỀU CHỈNH", "Điều chỉnh số lượng tồn kho thực tế");
        }

        [RelayCommand]
        private void OpenStockCountView()
        {
            NavigateToView("StockCount", () => new StockCountView { DataContext = new StockCountViewModel(CurrentUser, ContextFactory) }, "KIỂM KÊ", "Kiểm kê định kỳ và đối soát hàng hóa");
        }

        [RelayCommand]
        private void OpenPurchaseInvoiceView()
        {
            NavigateToView("PurchaseInvoice", () => new PurchaseInvoiceView { DataContext = new PurchaseInvoiceViewModel(this) }, "HÓA ĐƠN MUA", "Quản lý hóa đơn nhập hàng từ NCC");
        }

        [RelayCommand]
        private void OpenSalesInvoiceView()
        {
            NavigateToView("SalesInvoice", () => new SalesInvoiceView { DataContext = new SalesInvoiceViewModel(this) }, "HÓA ĐƠN BÁN", "Quản lý hóa đơn bán lẻ cho khách hàng");
        }

        [RelayCommand]
        private void OpenWarrantyView()
        {
            NavigateToView("Warranty", () => 
            {
                var vm = new WarrantyViewModel(CurrentUser, ContextFactory);
                vm.LoadData();
                return new WarrantyView { DataContext = vm };
            }, "BẢO HÀNH", "Quản lý phiếu bảo hành và sửa chữa");
        }

        // ── Reference Data ─────────────────────────────────────────────────────
        [RelayCommand]
        private void OpenCategoryView()
        {
            NavigateToView("Category", () => new CategoryView { DataContext = new CategoryViewModel(ContextFactory, CurrentUser!) }, "DANH MỤC", "Quản lý nhóm phân loại sản phẩm");
        }

        [RelayCommand]
        private void OpenBrandView()
        {
            NavigateToView("Brand", () => new BrandView { DataContext = new BrandViewModel(ContextFactory, CurrentUser!) }, "THƯƠNG HIỆU", "Quản lý các hãng sản xuất");
        }

        [RelayCommand]
        private void OpenUnitView()
        {
            NavigateToView("Unit", () => new UnitView { DataContext = new UnitViewModel(ContextFactory, CurrentUser!) }, "ĐƠN VỊ TÍNH", "Quản lý đơn vị đo lường");
        }

        [RelayCommand]
        private void OpenSupplierView()
        {
            NavigateToView("Supplier", () => new SupplierView { DataContext = new SupplierViewModel(ContextFactory, CurrentUser!) }, "NHÀ CUNG CẤP", "Quản lý đối tác nhập hàng");
        }

        [RelayCommand]
        private void OpenCustomerView()
        {
            NavigateToView("Customer", () => new CustomerView { DataContext = new CustomerViewModel(ContextFactory, CurrentUser!) }, "KHÁCH HÀNG", "Quản lý thông tin khách hàng");
        }

        [RelayCommand]
        private void OpenInventoryView()
        {
            NavigateToView("Inventory", () => new InventoryView { DataContext = new InventoryViewModel(ContextFactory) }, "TỒN KHO", "Theo dõi số lượng và giá trị hàng hóa hiện có");
        }

        [RelayCommand]
        private void OpenProductSerialView()
        {
            NavigateToView("ProductSerial", () => new ProductSerialView { DataContext = new ProductSerialViewModel(ContextFactory, CurrentUser) }, "QUẢN LÝ SERIAL", "Quản lý số Serial và IMEI sản phẩm");
        }

        [RelayCommand]
        private void OpenOpeningBalanceImportView()
        {
            NavigateToView("OpeningBalanceImport", () => new OpeningBalanceImportView { DataContext = new OpeningBalanceImportViewModel(CurrentUser.Id, ContextFactory) }, "NHẬP TỒN ĐẦU KỲ", "Import số dư đầu kỳ từ file Excel/CSV");
        }

        [RelayCommand]
        private void OpenAuditQueryView()
        {
            NavigateToView("AuditQuery", () => new AuditQueryView { DataContext = new AuditQueryViewModel(ContextFactory) }, "TRUY VẤN LỊCH SỬ", "Xem lịch sử biến động kho và chứng từ");
        }

        [RelayCommand]
        private void OpenWarrantyCoverageView()
        {
            NavigateToView("WarrantyCoverage", () => new WarrantyCoverageView { DataContext = new WarrantyCoverageViewModel(ContextFactory) }, "QUYỀN BẢO HÀNH", "Thiết lập các gói và điều kiện bảo hành");
        }

        [RelayCommand]
        private void OpenReportView()
        {
            NavigateToView("Report", () => new ReportView { DataContext = new ReportViewModel() }, "BÁO CÁO", "Phân tích hiệu quả kinh doanh và tài chính");
        }

        // ── Administration ─────────────────────────────────────────────────────
        [RelayCommand]
        private void OpenAppUserView()
        {
            if (IsAdmin)
            {
                NavigateToView("AppUser", () => new AppUserView { DataContext = new AppUserViewModel(CurrentUser, ContextFactory) }, "NGƯỜI DÙNG", "Quản lý tài khoản hệ thống");
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
                NavigateToView("AuditLog", () => new AuditLogView { DataContext = new AuditLogViewModel(ContextFactory) }, "NHẬT KÝ HỆ THỐNG", "Theo dõi lịch sử thay đổi dữ liệu toàn hệ thống");
            }
            else
            {
                System.Windows.MessageBox.Show("Bạn không có quyền truy cập!", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }
        [RelayCommand]
        private void OpenChangePasswordView()
        {
            NavigateToView("ChangePassword", () => new ChangePasswordView { DataContext = new ChangePasswordViewModel(CurrentUser, ContextFactory) }, "ĐỔI MẬT KHẨU", "Cập nhật mật khẩu truy cập");
        }

        [RelayCommand]
        private void ToggleSidebar()
        {
            IsSidebarCollapsed = !IsSidebarCollapsed;
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
