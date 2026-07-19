using QuanLyHangHoa.ViewModels;
using System.Collections;
using System.IO;
using System.Runtime.ExceptionServices;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;
using System.Reflection;
using System.Windows.Controls;


namespace QuanLyHangHoa.Tests.ViewModels;

public class MainViewModelCommandTests
{
    [Fact]
    public async Task Privileged_command_reloads_changed_role_and_invalidates_session()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var db = CreateContext(connection))
        {
            db.Database.EnsureCreated();
            db.AppUsers.AddRange(User(10, "Quản trị viên"), User(11, "Quản trị viên"));
            db.SaveChanges();
        }

        using var loginContext = CreateContext(connection);
        var loginIdentity = loginContext.AppUsers.AsNoTracking().Single(user => user.Id == 10);
        var invalidated = false;
        MainViewModel? viewModel = null;
        RunSta(() => viewModel = new MainViewModel(
            loginIdentity,
            () => CreateContext(connection),
            () => invalidated = true));

        var service = new AppUserService(() => CreateContext(connection));
        await service.UpdateUserAsync(10, User(10, "Quản lý"), loginIdentity.RowVersion, performedByUserId: 11, Guid.NewGuid());

        Assert.False(viewModel!.OpenAppUserViewCommand.CanExecute(null));
        Assert.True(invalidated);
        Assert.NotSame(loginIdentity, viewModel.CurrentUser);
        Assert.Equal("Quản lý", viewModel.CurrentUser!.RoleCode);
    }

    [Theory]
    [InlineData("user")]
    [InlineData("audit")]
    [InlineData("stock")]
    [InlineData("product-unit")]
    [InlineData("product-unit-unit")]
    public void Protected_command_denies_stale_role_and_releases_cached_identity_view(string command)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var db = CreateContext(connection))
        {
            db.Database.EnsureCreated();
            db.AppUsers.Add(User(10, "Quản trị viên"));
            db.SaveChanges();
        }

        AppUser loginIdentity;
        using (var loginContext = CreateContext(connection))
        {
            loginIdentity = loginContext.AppUsers.AsNoTracking().Single();
        }
        using (var db = CreateContext(connection))
        {
            var persisted = db.AppUsers.Single();
            persisted.RoleCode = "Nhân viên bán hàng";
            db.SaveChanges();
        }

        var invalidated = false;
        MainViewModel? viewModel = null;
        RunSta(() =>
        {
            viewModel = new MainViewModel(
                loginIdentity,
                () => CreateContext(connection),
                () => invalidated = true);
            CachedViews(viewModel)["Identity"] = new UserControl
            {
                DataContext = new AppUserViewModel(loginIdentity, () => CreateContext(connection))
            };

            switch (command)
            {
                case "user":
                    viewModel.OpenAppUserViewCommand.Execute(null);
                    break;
                case "audit":
                    viewModel.OpenAuditLogViewCommand.Execute(null);
                    break;
                case "stock":
                    viewModel.OpenStockAdjustmentViewCommand.Execute(null);
                    break;
                case "product-unit":
                    viewModel.OpenProductUnitViewCommand.Execute(null);
                    break;
                case "product-unit-unit":
                    typeof(MainViewModel)
                        .GetMethod("OpenUnitViewFromProductUnit", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(viewModel, null);
                    break;
            }
        });

        Assert.True(invalidated);
        Assert.Empty(CachedViews(viewModel!));
        Assert.Null(viewModel!.CurrentView);
        Assert.Equal("Nhân viên bán hàng", viewModel.CurrentUser!.RoleCode);
    }

    [Fact]
    public void Protected_command_denies_deactivated_identity_and_releases_cached_view()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var db = CreateContext(connection))
        {
            db.Database.EnsureCreated();
            db.AppUsers.Add(User(10, "Quản trị viên"));
            db.SaveChanges();
        }

        AppUser loginIdentity;
        using (var loginContext = CreateContext(connection))
        {
            loginIdentity = loginContext.AppUsers.AsNoTracking().Single();
        }
        using (var db = CreateContext(connection))
        {
            db.AppUsers.Single().IsActive = false;
            db.SaveChanges();
        }

        var invalidated = false;
        MainViewModel? viewModel = null;
        RunSta(() =>
        {
            viewModel = new MainViewModel(
                loginIdentity,
                () => CreateContext(connection),
                () => invalidated = true);
            CachedViews(viewModel)["Identity"] = new UserControl();
            viewModel.OpenStockOutViewCommand.Execute(null);
        });

        Assert.True(invalidated);
        Assert.Empty(CachedViews(viewModel!));
        Assert.False(viewModel!.CurrentUser!.IsActive);
    }

    [Fact]
    public void Refresh_gate_keeps_unchanged_authorized_identity_valid()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var db = CreateContext(connection))
        {
            db.Database.EnsureCreated();
            db.AppUsers.Add(User(10, "Quản trị viên"));
            db.SaveChanges();
        }

        using var loginContext = CreateContext(connection);
        var loginIdentity = loginContext.AppUsers.AsNoTracking().Single();
        var invalidated = false;
        MainViewModel? viewModel = null;
        bool? authorized = null;
        RunSta(() =>
        {
            viewModel = new MainViewModel(
                loginIdentity,
                () => CreateContext(connection),
                () => invalidated = true);
            authorized = (bool)typeof(MainViewModel)
                .GetMethod("RefreshAndAuthorize", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(viewModel, [PermissionAction.ManageUsers])!;
        });

        Assert.True(authorized);
        Assert.False(invalidated);
        Assert.NotSame(loginIdentity, viewModel!.CurrentUser);
    }

    [Fact]
    public void Unchanged_manager_is_denied_without_invalidating_session()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var db = CreateContext(connection))
        {
            db.Database.EnsureCreated();
            db.AppUsers.Add(User(10, "Quản lý"));
            db.SaveChanges();
        }

        using var loginContext = CreateContext(connection);
        var loginIdentity = loginContext.AppUsers.AsNoTracking().Single();
        var invalidated = false;
        MainViewModel? viewModel = null;
        RunSta(() => viewModel = new MainViewModel(
            loginIdentity,
            () => CreateContext(connection),
            () => invalidated = true));

        Assert.False(viewModel!.OpenAppUserViewCommand.CanExecute(null));
        Assert.False(invalidated);
    }

    [Theory]
    [InlineData(
        typeof(PurchaseInvoiceViewModel),
        "AvailableSuppliers",
        "AvailableStockIns")]
    [InlineData(
        typeof(SalesInvoiceViewModel),
        "AvailableCustomers",
        "AvailableStockOuts")]
    public void Cached_invoice_navigation_reloads_reference_data_from_database(
        Type viewModelType,
        string partyProperty,
        string stockDocumentProperty)
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"cached-invoice-{Guid.NewGuid():N}.db");
        try
        {
            using (var db = CreateFileContext(databasePath))
            {
                DatabaseHelper.SeedBasicData(db);
                db.Products.Add(Product(10, "P10"));
                if (viewModelType == typeof(PurchaseInvoiceViewModel))
                {
                    db.StockIns.Add(new StockIn
                    {
                        Id = 1,
                        DocumentCode = "IN-1",
                        SupplierId = 1,
                        WarehouseId = 1,
                        PurposeCode = "Purchase",
                        Status = "Draft",
                        CreatedBy = 2,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    db.StockOuts.Add(new StockOut
                    {
                        Id = 1,
                        DocumentCode = "OUT-1",
                        CustomerId = 1,
                        WarehouseId = 1,
                        PurposeCode = "Sale",
                        Status = "Draft",
                        CreatedBy = 2,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                db.SaveChanges();
            }

            Func<AppDbContext> contextFactory = () => CreateFileContext(databasePath);
            RunSta(() =>
            {
                AppUser currentUser;
                using (var db = contextFactory())
                {
                    currentUser = db.AppUsers.AsNoTracking()
                        .Single(user => user.Id == 2);
                }

                var invoiceViewModel = Activator.CreateInstance(
                    viewModelType,
                    currentUser,
                    contextFactory)!;
                Assert.True(SpinWait.SpinUntil(
                    () => !IsInvoiceLoading(viewModelType, invoiceViewModel),
                    TimeSpan.FromSeconds(5)));
                Assert.Equal(1, CollectionCount(
                    viewModelType,
                    invoiceViewModel,
                    "AvailableProducts"));
                Assert.Equal(1, CollectionCount(
                    viewModelType,
                    invoiceViewModel,
                    partyProperty));
                Assert.Equal(1, CollectionCount(
                    viewModelType,
                    invoiceViewModel,
                    stockDocumentProperty));

                var mainViewModel = new MainViewModel(
                    currentUser,
                    contextFactory,
                    () => { });
                var view = new UserControl { DataContext = invoiceViewModel };
                Func<UserControl> viewFactory = () => view;
                var navigate = typeof(MainViewModel)
                    .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                    .Single(method => method.Name == "NavigateToView")
                    .MakeGenericMethod(typeof(UserControl));
                navigate.Invoke(mainViewModel, ["invoice-probe", viewFactory, "Probe", "Probe"]);

                using (var db = contextFactory())
                {
                    db.Products.Add(Product(11, "P11"));
                    if (viewModelType == typeof(PurchaseInvoiceViewModel))
                    {
                        db.Suppliers.Add(new Supplier
                        {
                            Id = 2,
                            SupplierCode = "SUP2",
                            DisplayName = "Supplier 2",
                            IsActive = true
                        });
                        db.StockIns.Add(new StockIn
                        {
                            Id = 2,
                            DocumentCode = "IN-2",
                            SupplierId = 2,
                            WarehouseId = 1,
                            PurposeCode = "Purchase",
                            Status = "Draft",
                            CreatedBy = 2,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        db.Customers.Add(new Customer
                        {
                            Id = 2,
                            CustomerCode = "CUS2",
                            DisplayName = "Customer 2",
                            IsActive = true
                        });
                        db.StockOuts.Add(new StockOut
                        {
                            Id = 2,
                            DocumentCode = "OUT-2",
                            CustomerId = 2,
                            WarehouseId = 1,
                            PurposeCode = "Sale",
                            Status = "Draft",
                            CreatedBy = 2,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                    db.SaveChanges();
                }

                navigate.Invoke(mainViewModel, ["invoice-probe", viewFactory, "Probe", "Probe"]);
                Assert.True(SpinWait.SpinUntil(
                    () => !IsInvoiceLoading(viewModelType, invoiceViewModel),
                    TimeSpan.FromSeconds(5)));
                Assert.Equal(2, CollectionCount(
                    viewModelType,
                    invoiceViewModel,
                    "AvailableProducts"));
                Assert.Equal(2, CollectionCount(
                    viewModelType,
                    invoiceViewModel,
                    partyProperty));
                Assert.Equal(2, CollectionCount(
                    viewModelType,
                    invoiceViewModel,
                    stockDocumentProperty));
            });
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Fact]
    public void Cached_dashboard_navigation_reloads_database_stats()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"cached-dashboard-{Guid.NewGuid():N}.db");
        try
        {
            using (var db = CreateFileContext(databasePath))
            {
                DatabaseHelper.SeedBasicData(db);
                db.Products.Add(Product(10, "P10"));
                db.StockBalances.Add(new StockBalance
                {
                    Id = 1,
                    WarehouseId = 1,
                    ProductId = 10,
                    OnHandQuantity = 1m,
                    AvailableQuantity = 1m
                });
                db.SaveChanges();
            }

            Func<AppDbContext> contextFactory = () => CreateFileContext(databasePath);
            RunSta(() =>
            {
                AppUser currentUser;
                using (var db = contextFactory())
                    currentUser = db.AppUsers.AsNoTracking().Single(user => user.Id == 2);
                var mainViewModel = new MainViewModel(
                    currentUser,
                    contextFactory,
                    () => { });
                var dashboard = new DashboardViewModel(
                    new DatabaseDashboardService(contextFactory),
                    mainViewModel);
                NavigateCachedView(mainViewModel, "dashboard-probe", dashboard);
                dashboard.LoadStatsAsync().GetAwaiter().GetResult();
                Assert.True(
                    string.IsNullOrEmpty(dashboard.LoadErrorMessage),
                    dashboard.LoadErrorMessage);
                Assert.Equal(1, dashboard.Stats.TotalInventoryCount);

                using (var db = contextFactory())
                {
                    var balance = db.StockBalances.Single();
                    balance.OnHandQuantity = 2m;
                    balance.AvailableQuantity = 2m;
                    db.SaveChanges();
                }

                NavigateCachedView(mainViewModel, "dashboard-probe", dashboard);

                Assert.True(SpinWait.SpinUntil(
                    () => !dashboard.IsLoading
                        && dashboard.Stats.TotalInventoryCount == 2,
                    TimeSpan.FromSeconds(5)));
            });
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Fact]
    public void Cached_report_navigation_reloads_filter_data_from_database()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"cached-report-{Guid.NewGuid():N}.db");
        try
        {
            using (var db = CreateFileContext(databasePath))
            {
                DatabaseHelper.SeedBasicData(db);
                db.Products.Add(Product(10, "P10"));
                db.SaveChanges();
            }

            Func<AppDbContext> contextFactory = () => CreateFileContext(databasePath);
            RunSta(() =>
            {
                AppUser currentUser;
                using (var db = contextFactory())
                    currentUser = db.AppUsers.AsNoTracking().Single(user => user.Id == 2);
                var mainViewModel = new MainViewModel(
                    currentUser,
                    contextFactory,
                    () => { });
                var report = new ReportViewModel(contextFactory);

                Assert.True(SpinWait.SpinUntil(
                    () => report.Products.Count == 1
                        || !string.IsNullOrEmpty(report.LoadErrorMessage),
                    TimeSpan.FromSeconds(5)));
                Assert.True(
                    string.IsNullOrEmpty(report.LoadErrorMessage),
                    report.LoadErrorMessage);
                NavigateCachedView(mainViewModel, "report-probe", report);

                using (var db = contextFactory())
                {
                    db.Products.Add(Product(11, "P11"));
                    db.SaveChanges();
                }

                NavigateCachedView(mainViewModel, "report-probe", report);

                Assert.True(SpinWait.SpinUntil(
                    () => report.Products.Count == 2
                        || !string.IsNullOrEmpty(report.LoadErrorMessage),
                    TimeSpan.FromSeconds(5)));
                Assert.True(
                    string.IsNullOrEmpty(report.LoadErrorMessage),
                    report.LoadErrorMessage);
                Assert.Equal(2, report.Products.Count);
            });
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Fact]
    public void Cached_warranty_coverage_navigation_reloads_database_rows()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"cached-warranty-coverage-{Guid.NewGuid():N}.db");
        try
        {
            using (var db = CreateFileContext(databasePath))
            {
                DatabaseHelper.SeedBasicData(db);
                db.Products.Add(Product(10, "P10"));
                db.SaveChanges();
            }

            Func<AppDbContext> contextFactory = () => CreateFileContext(databasePath);
            RunSta(() =>
            {
                AppUser currentUser;
                using (var db = contextFactory())
                    currentUser = db.AppUsers.AsNoTracking().Single(user => user.Id == 2);
                var mainViewModel = new MainViewModel(
                    currentUser,
                    contextFactory,
                    () => { });
                var warrantyCoverage = new WarrantyCoverageViewModel(contextFactory);
                warrantyCoverage.LoadData().GetAwaiter().GetResult();
                Assert.True(
                    string.IsNullOrEmpty(warrantyCoverage.LoadErrorMessage),
                    warrantyCoverage.LoadErrorMessage);
                Assert.Empty(warrantyCoverage.Coverages);
                NavigateCachedView(
                    mainViewModel,
                    "warranty-coverage-probe",
                    warrantyCoverage);

                using (var db = contextFactory())
                {
                    db.StockIns.Add(new StockIn
                    {
                        Id = 1,
                        DocumentCode = "IN-WARRANTY-1",
                        SupplierId = 1,
                        WarehouseId = 1,
                        PurposeCode = "Purchase",
                        Status = "Posted",
                        CreatedBy = 2,
                        CreatedAt = DateTime.UtcNow,
                        PostedAt = DateTime.UtcNow,
                        PostedBy = 2
                    });
                    db.StockInLines.Add(new StockInLine
                    {
                        Id = 1,
                        StockInId = 1,
                        ProductId = 10,
                        UnitId = 1,
                        Quantity = 1m,
                        BaseQuantity = 1m,
                        UnitPrice = 10m
                    });
                    db.ProductSerials.Add(new ProductSerial
                    {
                        Id = 1,
                        ProductId = 10,
                        SerialNumber = "SERIAL-CACHE-1",
                        CurrentStatus = "Sold",
                        CurrentWarehouseId = null,
                        LastStockInLineId = 1
                    });
                    db.WarrantyCoverages.Add(new WarrantyCoverage
                    {
                        Id = 1,
                        ProductSerialId = 1,
                        CustomerId = 1,
                        WarrantyStartDate = DateTime.Today,
                        WarrantyEndDate = DateTime.Today.AddYears(1),
                        CoverageStatus = "Active"
                    });
                    db.SaveChanges();
                }

                NavigateCachedView(
                    mainViewModel,
                    "warranty-coverage-probe",
                    warrantyCoverage);

                Assert.True(SpinWait.SpinUntil(
                    () => warrantyCoverage.Coverages.Count == 1
                        || !string.IsNullOrEmpty(warrantyCoverage.LoadErrorMessage),
                    TimeSpan.FromSeconds(5)));
                Assert.True(
                    string.IsNullOrEmpty(warrantyCoverage.LoadErrorMessage),
                    warrantyCoverage.LoadErrorMessage);
                var coverage = Assert.Single(warrantyCoverage.Coverages);
                Assert.Equal("SERIAL-CACHE-1", coverage.ProductSerial.SerialNumber);
            });
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Fact]
    public void Cached_navigation_reuses_view_and_refreshes_its_data_context()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var db = CreateContext(connection))
        {
            db.Database.EnsureCreated();
            db.AppUsers.Add(User(10, "Quản lý"));
            db.SaveChanges();
        }

        var factoryCalls = 0;
        var refreshable = new RefreshableProbe();
        UserControl? createdView = null;
        MainViewModel? viewModel = null;
        RunSta(() =>
        {
            using var db = CreateContext(connection);
            var currentUser = db.AppUsers.AsNoTracking().Single();
            viewModel = new MainViewModel(
                currentUser,
                () => CreateContext(connection),
                () => { });
            Func<UserControl> viewFactory = () =>
            {
                factoryCalls++;
                createdView = new UserControl { DataContext = refreshable };
                return createdView;
            };
            var navigate = typeof(MainViewModel)
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(method => method.Name == "NavigateToView")
                .MakeGenericMethod(typeof(UserControl));

            navigate.Invoke(viewModel, ["probe", viewFactory, "Probe", "Probe"]);
            navigate.Invoke(viewModel, ["probe", viewFactory, "Probe", "Probe"]);
        });

        Assert.Equal(1, factoryCalls);
        Assert.Equal(1, refreshable.RefreshCount);
        Assert.Same(createdView, viewModel!.CurrentView);
    }

    [Fact]
    public void Product_unit_navigation_requires_master_data_permission()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var db = CreateContext(connection))
        {
            db.Database.EnsureCreated();
            db.AppUsers.AddRange(
                User(10, "Quản lý"),
                User(11, "Nhân viên bán hàng"));
            db.SaveChanges();
        }

        bool? managerCanOpen = null;
        bool? salesCanOpen = null;
        RunSta(() =>
        {
            using var db = CreateContext(connection);
            var users = db.AppUsers.AsNoTracking().OrderBy(user => user.Id).ToList();
            var managerViewModel = new MainViewModel(
                users[0],
                () => CreateContext(connection),
                () => { });
            var salesViewModel = new MainViewModel(
                users[1],
                () => CreateContext(connection),
                () => { });
            managerCanOpen = managerViewModel.OpenProductUnitViewCommand.CanExecute(null);
            salesCanOpen = salesViewModel.OpenProductUnitViewCommand.CanExecute(null);
        });

        Assert.True(managerCanOpen);
        Assert.False(salesCanOpen);
    }

    [Fact]
    public void WarrantyCoverage_navigation_command_is_generated()
    {
        Assert.NotNull(typeof(MainViewModel).GetProperty("OpenWarrantyCoverageViewCommand"));
    }

    [Theory]
    [InlineData("CanAccessStockIn")]
    [InlineData("CanAccessStockOut")]
    [InlineData("CanAccessStockAdjustment")]
    [InlineData("CanAccessPurchaseInvoices")]
    [InlineData("CanAccessSalesInvoices")]
    [InlineData("CanAccessWarranty")]
    [InlineData("CanAccessReports")]
    public void Navigation_permission_is_exposed_for_sidebar_binding(string propertyName)
    {
        Assert.NotNull(typeof(MainViewModel).GetProperty(propertyName));
    }


    private static Dictionary<string, UserControl> CachedViews(MainViewModel viewModel) =>
        (Dictionary<string, UserControl>)typeof(MainViewModel)
            .GetField("_viewCache", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(viewModel)!;

    private sealed class DatabaseDashboardService : DashboardService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public DatabaseDashboardService(Func<AppDbContext> contextFactory)
            : base(contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public override async Task<DashboardStats> GetStatsAsync()
        {
            using var db = _contextFactory();
            var quantities = await db.StockBalances
                .AsNoTracking()
                .Select(balance => balance.OnHandQuantity)
                .ToListAsync();
            return new DashboardStats { TotalInventoryCount = (int)quantities.Sum() };
        }
    }

    private sealed class RefreshableProbe : IRefreshable
    {
        public int RefreshCount { get; private set; }

        public void RefreshData() => RefreshCount++;
    }

    private static void NavigateCachedView(
        MainViewModel mainViewModel,
        string cacheKey,
        object dataContext)
    {
        var view = new UserControl { DataContext = dataContext };
        Func<UserControl> viewFactory = () => view;
        var navigate = typeof(MainViewModel)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(method => method.Name == "NavigateToView")
            .MakeGenericMethod(typeof(UserControl));
        navigate.Invoke(mainViewModel, [cacheKey, viewFactory, "Probe", "Probe"]);
    }

    private static bool IsInvoiceLoading(Type viewModelType, object viewModel) =>
        (bool)viewModelType
            .GetField("_isLoading", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(viewModel)!;

    private static int CollectionCount(
        Type viewModelType,
        object viewModel,
        string propertyName) =>
        ((ICollection)viewModelType.GetProperty(propertyName)!.GetValue(viewModel)!).Count;

    private static Product Product(int id, string code) => new()
    {
        Id = id,
        ProductCode = code,
        DisplayName = $"Product {id}",
        CategoryId = 1,
        BrandId = 1,
        DefaultUnitId = 1,
        DefaultPrice = 10m,
        IsActive = true
    };

    private static AppDbContext CreateFileContext(string databasePath)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath};Pooling=False")
            .Options;
        return new AppDbContext(options);
    }

    private static AppUser User(int id, string role) => new()
    {
        Id = id,
        Username = $"user-{id}",
        PasswordHash = "hash",
        FullName = $"User {id}",
        RoleCode = role,
        IsActive = true
    };

    private static AppDbContext CreateContext(SqliteConnection connection) =>
        DatabaseHelper.CreateContext(connection);

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
