using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Views;
using QuanLyHangHoa.Services;
using System.Windows.Controls;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace QuanLyHangHoa.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private AppUser? _currentUser;

        [ObservableProperty]
        private UserControl? _currentView;

        [ObservableProperty]
        private string _currentViewTitle = "DASHBOARD";

        [ObservableProperty]
        private string _currentViewSubtitle = "Tổng quan hoạt động kinh doanh";

        [ObservableProperty]
        private bool _isSidebarCollapsed;

        [ObservableProperty]
        private bool _hasUpdateAvailable;

        public bool IsAdmin => AuthorizationService.CanPerform(CurrentUser, PermissionAction.ManageUsers);
        public bool CanViewLogs => AuthorizationService.CanPerform(CurrentUser, PermissionAction.ManageAuditLogs);
        public bool CanAccessStockIn => AuthorizationService.CanPerform(CurrentUser, PermissionAction.PostStockIn);
        public bool CanAccessStockOut => AuthorizationService.CanPerform(CurrentUser, PermissionAction.PostStockOut);
        public bool CanAccessStockAdjustment => AuthorizationService.CanPerform(CurrentUser, PermissionAction.PostStockAdjustment);
        public bool CanAccessPurchaseInvoices => AuthorizationService.CanPerform(CurrentUser, PermissionAction.CreatePurchaseInvoice);
        public bool CanAccessSalesInvoices => AuthorizationService.CanPerform(CurrentUser, PermissionAction.CreateSalesInvoice);
        public bool CanAccessWarranty => AuthorizationService.CanPerform(CurrentUser, PermissionAction.CreateWarrantyClaim);
        public bool CanAccessReports => AuthorizationService.CanPerform(CurrentUser, PermissionAction.ViewReports);

        public Func<Data.AppDbContext> ContextFactory { get; }
        private readonly DashboardService _dashboardService;
        // cache giữ một View/ViewModel cho mỗi màn hình trong đúng phiên đăng nhập; đổi role hoặc logout sẽ xóa toàn bộ
        private readonly System.Collections.Generic.Dictionary<string, UserControl> _viewCache = new();
        private readonly int _authenticatedUserId;
        private readonly Action _invalidateSession;
        private bool _sessionInvalidated;
        private UpdateViewModel? _updateViewModel;

        public MainViewModel(AppUser user, Func<Data.AppDbContext> contextFactory)
            : this(user, contextFactory, null)
        {
            OpenDashboard();
        }

        public MainViewModel(
            AppUser user,
            Func<Data.AppDbContext> contextFactory,
            Action? invalidateSession)
        {
            ArgumentNullException.ThrowIfNull(user);
            ArgumentNullException.ThrowIfNull(contextFactory);
            CurrentUser = user;
            _authenticatedUserId = user.Id;
            ContextFactory = contextFactory;
            _dashboardService = new DashboardService(ContextFactory);
            _invalidateSession = invalidateSession ?? Logout;
        }

        public Task LoadInitialViewAsync()
        {
            return CurrentView?.DataContext is DashboardViewModel dashboard
                ? dashboard.EnsureLoadedAsync()
                : Task.CompletedTask;
        }

        private bool CanManageUsers()
        {
            return RefreshAndAuthorize(PermissionAction.ManageUsers);
        }
        private bool CanPostStockIn() => RefreshAndAuthorize(PermissionAction.PostStockIn);

        private bool CanPostStockOut() => RefreshAndAuthorize(PermissionAction.PostStockOut);

        private bool CanPostStockAdjustment() => RefreshAndAuthorize(PermissionAction.PostStockAdjustment);

        private bool CanCreatePurchaseInvoice() => RefreshAndAuthorize(PermissionAction.CreatePurchaseInvoice);

        private bool CanCreateSalesInvoice() => RefreshAndAuthorize(PermissionAction.CreateSalesInvoice);

        private bool CanCreateWarrantyClaim() => RefreshAndAuthorize(PermissionAction.CreateWarrantyClaim);

        private bool CanOpenReports() => RefreshAndAuthorize(PermissionAction.ViewReports);

        private bool CanManageAuditLogs() => RefreshAndAuthorize(PermissionAction.ManageAuditLogs);

        private bool CanManageMasterData() => RefreshAndAuthorize(PermissionAction.ManageMasterData);


        // đọc lại user từ database trước mỗi command nhạy cảm để việc khóa tài khoản hoặc đổi role có hiệu lực ngay
        private bool RefreshAndAuthorize(PermissionAction action)
        {
            if (_sessionInvalidated)
            {
                return false;
            }

            var previousUser = CurrentUser;
            using var db = ContextFactory();
            var refreshedUser = db.AppUsers
                .AsNoTracking()
                .SingleOrDefault(user => user.Id == _authenticatedUserId);
            // identityChanged chỉ xét trạng thái và role vì đây là hai field quyết định quyền/session
            var identityChanged = previousUser?.IsActive != refreshedUser?.IsActive ||
                !string.Equals(previousUser?.RoleCode, refreshedUser?.RoleCode, StringComparison.OrdinalIgnoreCase);

            CurrentUser = refreshedUser;
            if (identityChanged)
            {
                _viewCache.Clear();
                CurrentView = null;
            }

            var isAuthorized = AuthorizationService.CanPerform(refreshedUser, action);

            if (refreshedUser == null ||
                !refreshedUser.IsActive ||
                identityChanged && !isAuthorized)
            {
                InvalidateSession();
            }


            if (identityChanged)
            {
                NotifyAuthorizationChanged();
            }
            return isAuthorized;
        }

        private void NotifyAuthorizationChanged()
        {
            OnPropertyChanged(nameof(IsAdmin));
            OnPropertyChanged(nameof(CanViewLogs));
            OnPropertyChanged(nameof(CanAccessStockIn));
            OnPropertyChanged(nameof(CanAccessStockOut));
            OnPropertyChanged(nameof(CanAccessStockAdjustment));
            OnPropertyChanged(nameof(CanAccessPurchaseInvoices));
            OnPropertyChanged(nameof(CanAccessSalesInvoices));
            OnPropertyChanged(nameof(CanAccessWarranty));
            OnPropertyChanged(nameof(CanAccessReports));
        }

        // chỉ invalidate một lần, bỏ View cache rồi chuyển về luồng logout do owner cung cấp
        private void InvalidateSession()
        {
            if (_sessionInvalidated) return;
            _sessionInvalidated = true;
            CurrentView = null;
            _viewCache.Clear();
            _invalidateSession();
        }

        // lần đầu tạo và cache View; lần quay lại gọi IRefreshable để giữ state màn hình nhưng nạp dữ liệu mới
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
            NavigateToView("Product", () => new ProductView { DataContext = new ProductViewModel(ContextFactory, CurrentUser!) }, "KHO HÀNG", "Quản lý danh mục sản phẩm và tồn kho");
        }

        [RelayCommand(CanExecute = nameof(CanPostStockOut))]
        private void OpenStockOutView()
        {
            if (!RefreshAndAuthorize(PermissionAction.PostStockOut)) return;
            NavigateToView("StockOut", () => new StockOutView { DataContext = new StockOutViewModel(CurrentUser!, ContextFactory) }, "XUẤT KHO", "Lập phiếu xuất kho và quản lý hàng xuất");
        }

        [RelayCommand(CanExecute = nameof(CanPostStockIn))]
        private void OpenStockInView()
        {
            if (!RefreshAndAuthorize(PermissionAction.PostStockIn)) return;
            NavigateToView("StockIn", () => new StockInView { DataContext = new StockInViewModel(CurrentUser!, ContextFactory) }, "NHẬP KHO", "Lập phiếu nhập kho và quản lý hàng nhập");
        }

        [RelayCommand(CanExecute = nameof(CanPostStockAdjustment))]
        private void OpenStockTransferView()
        {
            if (!RefreshAndAuthorize(PermissionAction.PostStockAdjustment)) return;
            NavigateToView("StockTransfer", () => new StockTransferView { DataContext = new StockTransferViewModel(CurrentUser!, ContextFactory) }, "CHUYỂN KHO", "Điều chuyển hàng hóa giữa các kho nội bộ");
        }

        [RelayCommand(CanExecute = nameof(CanPostStockAdjustment))]
        private void OpenStockAdjustmentView()
        {
            if (!RefreshAndAuthorize(PermissionAction.PostStockAdjustment)) return;
            NavigateToView("StockAdjustment", () => new StockAdjustmentView { DataContext = new StockAdjustmentViewModel(CurrentUser!, ContextFactory) }, "ĐIỀU CHỈNH", "Điều chỉnh số lượng tồn kho thực tế");
        }

        [RelayCommand(CanExecute = nameof(CanPostStockAdjustment))]
        private void OpenStockCountView()
        {
            if (!RefreshAndAuthorize(PermissionAction.PostStockAdjustment)) return;
            NavigateToView("StockCount", () => new StockCountView { DataContext = new StockCountViewModel(CurrentUser!, ContextFactory) }, "KIỂM KÊ", "Kiểm kê định kỳ và đối soát hàng hóa");
        }

        [RelayCommand(CanExecute = nameof(CanCreatePurchaseInvoice))]
        private void OpenPurchaseInvoiceView()
        {
            if (!RefreshAndAuthorize(PermissionAction.CreatePurchaseInvoice)) return;
            NavigateToView("PurchaseInvoice", () => new PurchaseInvoiceView { DataContext = new PurchaseInvoiceViewModel(CurrentUser!, ContextFactory) }, "HÓA ĐƠN MUA", "Quản lý hóa đơn nhập hàng từ NCC");
        }

        [RelayCommand(CanExecute = nameof(CanCreateSalesInvoice))]
        private void OpenSalesInvoiceView()
        {
            if (!RefreshAndAuthorize(PermissionAction.CreateSalesInvoice)) return;
            NavigateToView("SalesInvoice", () => new SalesInvoiceView { DataContext = new SalesInvoiceViewModel(CurrentUser!, ContextFactory) }, "HÓA ĐƠN BÁN", "Quản lý hóa đơn bán lẻ cho khách hàng");
        }

        [RelayCommand(CanExecute = nameof(CanCreateWarrantyClaim))]
        private void OpenWarrantyView()
        {
            if (!RefreshAndAuthorize(PermissionAction.CreateWarrantyClaim)) return;
            NavigateToView("Warranty", () =>
            {
                var vm = new WarrantyViewModel(CurrentUser!, ContextFactory);
                _ = vm.LoadData();
                return new WarrantyView { DataContext = vm };
            }, "BẢO HÀNH", "Quản lý phiếu bảo hành và sửa chữa");
        }

        [RelayCommand(CanExecute = nameof(CanCreateWarrantyClaim))]
        private void OpenWarrantyCoverageView()
        {
            if (!RefreshAndAuthorize(PermissionAction.CreateWarrantyClaim)) return;
            NavigateToView(
                "WarrantyCoverage",
                () => new WarrantyCoverageView
                {
                    DataContext = new WarrantyCoverageViewModel(CurrentUser ?? throw new InvalidOperationException("Current user is required."), ContextFactory)
                },
                "QUYỀN BẢO HÀNH",
                "Quản lý thời hạn và trạng thái bảo hành");
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

        private void OpenUnitViewFromProductUnit()
        {
            if (RefreshAndAuthorize(PermissionAction.ManageMasterData))
                OpenUnitView();
        }

        [RelayCommand(CanExecute = nameof(CanManageMasterData))]
        private void OpenProductUnitView()
        {
            if (!RefreshAndAuthorize(PermissionAction.ManageMasterData))
                return;

            NavigateToView(
                "ProductUnit",
                () => new ProductUnitView
                {
                    DataContext = new ProductUnitViewModel(
                        ContextFactory,
                        CurrentUser!,
                        openUnitManagement: OpenUnitViewFromProductUnit,
                        canManage: CanManageMasterData)
                },
                "\u0110\u01a0N V\u1eca T\u00cdNH S\u1ea2N PH\u1ea8M",
                "Qu\u1ea3n l\u00fd \u0111\u01a1n v\u1ecb quy \u0111\u1ed5i theo s\u1ea3n ph\u1ea9m");
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
            NavigateToView("ProductSerial", () => new ProductSerialView { DataContext = new ProductSerialViewModel(ContextFactory, CurrentUser!) }, "QUẢN LÝ SERIAL", "Quản lý số Serial và IMEI sản phẩm");
        }

        [RelayCommand(CanExecute = nameof(CanPostStockAdjustment))]
        private void OpenOpeningBalanceImportView()
        {
            if (!RefreshAndAuthorize(PermissionAction.PostStockAdjustment)) return;
            NavigateToView("OpeningBalanceImport", () => new OpeningBalanceImportView { DataContext = new OpeningBalanceImportViewModel(CurrentUser!.Id, ContextFactory) }, "NHẬP TỒN ĐẦU KỲ", "Import số dư đầu kỳ từ file Excel/CSV");
        }

        [RelayCommand(CanExecute = nameof(CanOpenReports))]
        private void OpenReportView()
        {
            if (!RefreshAndAuthorize(PermissionAction.ViewReports)) return;
            NavigateToView("Report", () => new ReportView { DataContext = new ReportViewModel(ContextFactory) }, "BÁO CÁO", "Phân tích hiệu quả kinh doanh và tài chính");
        }

        // ── Administration ─────────────────────────────────────────────────────
        [RelayCommand(CanExecute = nameof(CanManageUsers))]
        private void OpenAppUserView()
        {
            if (!RefreshAndAuthorize(PermissionAction.ManageUsers)) return;
            NavigateToView("AppUser", () => new AppUserView { DataContext = new AppUserViewModel(CurrentUser!, ContextFactory) }, "NGƯỜI DÙNG", "Quản lý tài khoản hệ thống");
        }

        [RelayCommand(CanExecute = nameof(CanManageAuditLogs))]
        private void OpenAuditLogView()
        {
            if (RefreshAndAuthorize(PermissionAction.ManageAuditLogs))
            {
                NavigateToView("AuditLog", () => new AuditLogView { DataContext = new AuditLogViewModel(ContextFactory, CurrentUser!) }, "NHẬT KÝ HỆ THỐNG", "Theo dõi lịch sử thay đổi dữ liệu toàn hệ thống");
            }
            else if (!_sessionInvalidated)
            {
                System.Windows.MessageBox.Show("Bạn không có quyền truy cập!", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }
        [RelayCommand]
        private void OpenChangePasswordView()
        {
            NavigateToView("ChangePassword", () => new ChangePasswordView { DataContext = new ChangePasswordViewModel(CurrentUser!, ContextFactory) }, "ĐỔI MẬT KHẨU", "Cập nhật mật khẩu truy cập");
        }

        public Task CheckForUpdatesAutomaticallyAsync()
        {
            return GetUpdateViewModel().CheckAutomaticallyAsync();
        }

        [RelayCommand]
        private void OpenUpdateView()
        {
            NavigateToView(
                "Update",
                () => new UpdateView { DataContext = GetUpdateViewModel() },
                "CẬP NHẬT WAREPRO",
                "Kiểm tra và cài bản vá hoặc tính năng mới");
        }

        // dùng một UpdateViewModel suốt session để trạng thái check/download và badge không bị mất khi đổi màn hình
        private UpdateViewModel GetUpdateViewModel()
        {
            if (_updateViewModel is null)
            {
                _updateViewModel = UpdateViewModel.CreateDefault();
                _updateViewModel.UpdateAvailabilityChanged += available =>
                    HasUpdateAvailable = available;
            }

            return _updateViewModel;
        }

        [RelayCommand]
        private void ToggleSidebar()
        {
            IsSidebarCollapsed = !IsSidebarCollapsed;
        }

        [RelayCommand]
        // mở cửa sổ login mới rồi đóng MainWindow; toàn bộ cache và user state của session cũ theo đó được giải phóng
        private void Logout()
        {
            var login = new LoginView();
            System.Windows.Application.Current.MainWindow = login;
            login.Show();
            foreach (System.Windows.Window window in System.Windows.Application.Current.Windows)
            {
                if (window is MainWindow) { window.Close(); break; }
            }
        }
    }
}
