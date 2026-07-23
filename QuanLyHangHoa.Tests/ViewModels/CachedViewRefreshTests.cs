using System.Collections;
using System.Data.Common;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Tests.Helpers;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Tests.ViewModels;

public class CachedViewRefreshTests
{
    [Theory]
    [InlineData(typeof(PurchaseInvoiceViewModel))]
    [InlineData(typeof(SalesInvoiceViewModel))]
    [InlineData(typeof(DashboardViewModel))]
    [InlineData(typeof(ReportViewModel))]
    [InlineData(typeof(WarrantyCoverageViewModel))]
    public void Cached_data_view_models_are_refreshable(Type viewModelType)
    {
        Assert.True(typeof(IRefreshable).IsAssignableFrom(viewModelType));
    }

    [Theory]
    [InlineData(typeof(PurchaseInvoiceViewModel))]
    [InlineData(typeof(SalesInvoiceViewModel))]
    public void Invoice_refresh_command_invalidates_reference_cache(Type viewModelType)
    {
        var viewModel = RuntimeHelpers.GetUninitializedObject(viewModelType);
        var field = viewModelType.GetField(
            "_referenceDataLoaded",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(viewModel, true);

        var command = (ICommand)viewModelType
            .GetProperty("RefreshCommand")!.GetValue(viewModel)!;
        command.Execute(null);

        Assert.False((bool)field.GetValue(viewModel)!);
    }

    [Theory]
    [InlineData(typeof(PurchaseInvoiceViewModel))]
    [InlineData(typeof(SalesInvoiceViewModel))]
    public async Task Invoice_refresh_failure_preserves_rows_and_paging(Type viewModelType)
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"inventory-refresh-{Guid.NewGuid():N}.db");
        try
        {
            using (var db = CreateFileContext(databasePath))
                db.Database.EnsureCreated();
            var failRefresh = false;
            Func<AppDbContext> contextFactory = () =>
                failRefresh
                    ? throw new InvalidOperationException("invoice database unavailable")
                    : CreateFileContext(databasePath);
            var currentUser = new AppUser
            {
                Id = 2,
                Username = "manager",
                PasswordHash = "hash",
                FullName = "Manager",
                RoleCode = "Quản lý",
                IsActive = true
            };
            var viewModel = (IRefreshable)Activator.CreateInstance(
                viewModelType,
                currentUser,
                contextFactory)!;
            var isLoading = viewModelType.GetField(
                "_isLoading",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            var skip = viewModelType.GetField(
                "_skip",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            await WaitUntilAsync(() => !(bool)isLoading.GetValue(viewModel)!);
            var invoices = (IList)viewModelType.GetProperty("Invoices")!.GetValue(viewModel)!;
            object existing = viewModelType == typeof(PurchaseInvoiceViewModel)
                ? new PurchaseInvoice { Id = 71 }
                : new SalesInvoice { Id = 72 };
            invoices.Add(existing);
            skip.SetValue(viewModel, 7);

            failRefresh = true;
            viewModel.RefreshData();

            await WaitUntilAsync(() =>
                !string.IsNullOrWhiteSpace((string?)viewModelType
                    .GetProperty("LoadErrorMessage")!.GetValue(viewModel)));
            var currentInvoices = (IList)viewModelType
                .GetProperty("Invoices")!.GetValue(viewModel)!;
            var visible = Assert.Single(currentInvoices.Cast<object>());
            Assert.Same(existing, visible);
            Assert.Equal(7, skip.GetValue(viewModel));
        }
        finally
        {
            File.Delete(databasePath);
        }
    }

    [Fact]
    public void Report_requires_an_injected_context_factory()
    {
        Assert.DoesNotContain(
            typeof(ReportViewModel).GetConstructors(),
            constructor => constructor.GetParameters().Length == 0);
    }

    [Fact]
    public async Task Report_excludes_voided_invoices_from_revenue_and_cost()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"report-voided-{Guid.NewGuid():N}.db");
        try
        {
            SeedInvoiceStatusDatabase(databasePath);
            var viewModel = new ReportViewModel(() => CreateFileContext(databasePath));
            await WaitUntilAsync(() => viewModel.Categories.Count > 0);
            viewModel.FromDate = DateTime.Today.AddDays(-1);
            viewModel.ToDate = DateTime.Today.AddDays(1);

            await viewModel.Refresh();

            Assert.Equal(100m, viewModel.TotalRevenue);
            Assert.Equal(40m, viewModel.TotalCost);
            Assert.Equal(60m, viewModel.TotalProfit);
            var daily = Assert.Single(viewModel.DailyReports);
            Assert.Equal(100m, daily.Revenue);
            Assert.Equal(40m, daily.Cost);
        }
        finally
        {
            await DeleteFileWhenUnlockedAsync(databasePath);
        }
    }

    [Fact]
    public async Task Report_load_failure_preserves_visible_rows_and_exposes_retry_error()
    {
        var viewModel = new ReportViewModel(
            () => throw new InvalidOperationException("report database unavailable"));
        var existing = new DailyReportItem { Date = DateTime.Today, Revenue = 10m };
        viewModel.DailyReports.Add(existing);

        ((IRefreshable)viewModel).RefreshData();

        await WaitUntilAsync(() => !string.IsNullOrWhiteSpace(viewModel.LoadErrorMessage));
        Assert.Same(existing, Assert.Single(viewModel.DailyReports));
        Assert.Contains("report database unavailable", viewModel.LoadErrorMessage);
    }

    [Fact]
    public async Task Report_filter_failure_stops_the_refresh_and_keeps_its_error()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var db = DatabaseHelper.CreateContext(connection))
            db.Database.EnsureCreated();

        var contextCalls = 0;
        var viewModel = new ReportViewModel(() =>
        {
            if (Interlocked.Increment(ref contextCalls) == 1)
                throw new InvalidOperationException("filter load failed");
            return DatabaseHelper.CreateContext(connection);
        });

        await WaitUntilAsync(() => !string.IsNullOrWhiteSpace(viewModel.LoadErrorMessage));
        await Task.Delay(100);

        Assert.Equal(1, contextCalls);
        Assert.Contains("filter load failed", viewModel.LoadErrorMessage);
    }

    [Fact]
    public async Task Report_newer_refresh_cancels_older_filter_load()
    {
        var oldDatabasePath = Path.Combine(
            Path.GetTempPath(),
            $"report-old-{Guid.NewGuid():N}.db");
        var newDatabasePath = Path.Combine(
            Path.GetTempPath(),
            $"report-new-{Guid.NewGuid():N}.db");
        var blocker = new CancelAwareQueryBlocker();
        try
        {
            SeedReportDatabase(oldDatabasePath, 10, "Old product");
            SeedReportDatabase(newDatabasePath, 11, "New product");
            var contextCalls = 0;
            var viewModel = new ReportViewModel(() =>
            {
                if (Interlocked.Increment(ref contextCalls) == 1)
                    return CreateFileContext(oldDatabasePath, blocker);
                return CreateFileContext(newDatabasePath);
            });
            await blocker.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));

            viewModel.RefreshData();

            await blocker.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await WaitUntilAsync(() =>
                viewModel.Products.Any(product => product.DisplayName == "New product"));
            Assert.DoesNotContain(
                viewModel.Products,
                product => product.DisplayName == "Old product");
            Assert.Null(viewModel.LoadErrorMessage);
        }
        finally
        {
            blocker.Release.TrySetResult();
            await DeleteFileWhenUnlockedAsync(oldDatabasePath);
            await DeleteFileWhenUnlockedAsync(newDatabasePath);
        }
    }

    [Fact]
    public async Task Dashboard_load_failure_preserves_visible_stats_and_exposes_retry_error()
    {
        var service = new DashboardService(
            () => throw new InvalidOperationException("dashboard database unavailable"));
        var viewModel = new DashboardViewModel(
            service,
            (MainViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MainViewModel)));
        var existing = new DashboardStats();
        viewModel.Stats = existing;

        ((IRefreshable)viewModel).RefreshData();

        await WaitUntilAsync(() => !string.IsNullOrWhiteSpace(viewModel.LoadErrorMessage));
        Assert.Same(existing, viewModel.Stats);
        Assert.Contains("dashboard database unavailable", viewModel.LoadErrorMessage);
    }

    [Fact]
    public void Dashboard_invoice_aggregates_require_active_status_filters()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null &&
               !File.Exists(Path.Combine(root.FullName, "QuanLyHangHoa", "QuanLyHangHoa.csproj")))
        {
            root = root.Parent;
        }
        Assert.NotNull(root);
        var source = File.ReadAllText(Path.Combine(
            root!.FullName,
            "QuanLyHangHoa",
            "Services",
            "DashboardService.cs"));

        Assert.Equal(6, source.Split("InvoiceStatus.Active").Length - 1);
        foreach (var viewModelFile in new[]
                 {
                     "SalesInvoiceViewModel.cs",
                     "PurchaseInvoiceViewModel.cs"
                 })
        {
            var viewModelSource = File.ReadAllText(Path.Combine(
                root.FullName,
                "QuanLyHangHoa",
                "ViewModels",
                viewModelFile));
            Assert.Contains(
                "query = query.Where(invoice => invoice.Status == InvoiceStatus.Active);",
                viewModelSource);
        }
    }

    [Fact]
    public void Dashboard_warranty_count_matches_open_claim_invariant()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null &&
               !File.Exists(Path.Combine(root.FullName, "QuanLyHangHoa", "QuanLyHangHoa.csproj")))
        {
            root = root.Parent;
        }
        Assert.NotNull(root);
        var source = File.ReadAllText(Path.Combine(
            root!.FullName,
            "QuanLyHangHoa",
            "Services",
            "DashboardService.cs"));

        Assert.Contains(
            "w.Status != \"Closed\" && w.Status != \"Rejected\"",
            source);
        Assert.DoesNotContain(
            "w.Status == \"Active\" || w.Status == \"Processing\"",
            source);
    }

    [Fact]
    public async Task Dashboard_older_refresh_cannot_overwrite_newer_result()
    {
        var firstResult = new TaskCompletionSource<DashboardStats>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondResult = new TaskCompletionSource<DashboardStats>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var callCount = 0;
        var service = new Mock<DashboardService>(new object[] { null! });
        service
            .Setup(item => item.GetStatsAsync())
            .Returns(() => Interlocked.Increment(ref callCount) == 1
                ? firstResult.Task
                : secondResult.Task);
        var viewModel = new DashboardViewModel(
            service.Object,
            (MainViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MainViewModel)));
        var olderStats = new DashboardStats { TotalInventoryCount = 1 };
        var newerStats = new DashboardStats { TotalInventoryCount = 2 };

        var olderRefresh = viewModel.LoadStatsAsync();
        var newerRefresh = viewModel.LoadStatsAsync();
        secondResult.SetResult(newerStats);
        await newerRefresh;
        firstResult.SetResult(olderStats);
        await olderRefresh;

        Assert.Same(newerStats, viewModel.Stats);
        Assert.False(viewModel.IsLoading);
        Assert.Null(viewModel.LoadErrorMessage);
    }

    [Fact]
    public void Dashboard_successful_empty_refresh_clears_all_old_charts()
    {
        var service = new DashboardService(
            () => throw new InvalidOperationException("not used"));
        var viewModel = new DashboardViewModel(
            service,
            (MainViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MainViewModel)));
        var updateCharts = typeof(DashboardViewModel).GetMethod(
            "UpdateCharts",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        viewModel.Stats = new DashboardStats
        {
            RevenueExpenseChart =
            [
                new RevenueExpenseData { Month = "01", Revenue = 10m, Expense = 5m }
            ],
            StockMovementChart =
            [
                new StockMovementData { Date = "01/01", StockInCount = 1, StockOutCount = 1 }
            ],
            InventoryStructureChart =
            [
                new InventoryStructureData { CategoryName = "A", TotalValue = 10m }
            ],
            TopSellingProductsChart =
            [
                new TopSellingProductData { ProductName = "P", TotalSold = 1 }
            ]
        };
        updateCharts.Invoke(viewModel, null);
        Assert.NotEmpty(viewModel.RevenueExpenseSeries);
        Assert.NotEmpty(viewModel.StockMovementSeries);
        Assert.NotEmpty(viewModel.InventoryPieSeries);
        Assert.NotEmpty(viewModel.InventoryLegendItems);
        Assert.NotEmpty(viewModel.TopProductsSeries);

        viewModel.Stats = new DashboardStats();
        updateCharts.Invoke(viewModel, null);

        Assert.Empty(viewModel.RevenueExpenseSeries);
        Assert.Empty(viewModel.RevenueExpenseXAxes);
        Assert.Empty(viewModel.StockMovementSeries);
        Assert.Empty(viewModel.StockMovementXAxes);
        Assert.Empty(viewModel.InventoryPieSeries);
        Assert.Empty(viewModel.InventoryLegendItems);
        Assert.Empty(viewModel.TopProductsSeries);
        Assert.Empty(viewModel.TopProductsYAxes);
    }

    [Fact]
    public async Task Coverage_load_failure_preserves_visible_rows_and_exposes_retry_error()
    {
        var viewModel = new WarrantyCoverageViewModel(
            () => throw new InvalidOperationException("coverage database unavailable"));
        var existing = new WarrantyCoverage { Id = 91 };
        viewModel.Coverages.Add(existing);

        ((IRefreshable)viewModel).RefreshData();

        await WaitUntilAsync(() => !string.IsNullOrWhiteSpace(viewModel.LoadErrorMessage));
        Assert.Same(existing, Assert.Single(viewModel.Coverages));
        Assert.Contains("coverage database unavailable", viewModel.LoadErrorMessage);
    }

    private static void SeedReportDatabase(
        string databasePath,
        int productId,
        string productName)
    {
        using var db = CreateFileContext(databasePath);
        DatabaseHelper.SeedBasicData(db);
        db.Products.Add(new Product
        {
            Id = productId,
            ProductCode = $"P{productId}",
            DisplayName = productName,
            CategoryId = 1,
            BrandId = 1,
            DefaultUnitId = 1,
            DefaultPrice = 10m,
            IsActive = true
        });
        db.SaveChanges();
    }

    private static void SeedInvoiceStatusDatabase(string databasePath)
    {
        using var db = CreateFileContext(databasePath);
        DatabaseHelper.SeedBasicData(db);
        var now = DateTime.Now;
        db.SalesInvoices.AddRange(
            new SalesInvoice
            {
                InvoiceCode = "SALE-ACTIVE",
                CustomerId = 1,
                InvoiceDate = now,
                SubTotal = 100m,
                GrandTotal = 100m,
                PaymentStatus = PaymentStatus.Unpaid,
                Status = InvoiceStatus.Active,
                CreatedBy = 1,
                CreatedAt = now
            },
            new SalesInvoice
            {
                InvoiceCode = "SALE-VOIDED",
                CustomerId = 1,
                InvoiceDate = now,
                SubTotal = 900m,
                GrandTotal = 900m,
                PaymentStatus = PaymentStatus.Unpaid,
                Status = InvoiceStatus.Voided,
                CreatedBy = 1,
                CreatedAt = now
            });
        db.PurchaseInvoices.AddRange(
            new PurchaseInvoice
            {
                InvoiceCode = "PURCHASE-ACTIVE",
                SupplierId = 1,
                InvoiceDate = now,
                SubTotal = 40m,
                GrandTotal = 40m,
                PaymentStatus = PaymentStatus.Unpaid,
                Status = InvoiceStatus.Active,
                CreatedBy = 1,
                CreatedAt = now
            },
            new PurchaseInvoice
            {
                InvoiceCode = "PURCHASE-VOIDED",
                SupplierId = 1,
                InvoiceDate = now,
                SubTotal = 400m,
                GrandTotal = 400m,
                PaymentStatus = PaymentStatus.Unpaid,
                Status = InvoiceStatus.Voided,
                CreatedBy = 1,
                CreatedAt = now
            });
        db.SaveChanges();
    }

    private static AppDbContext CreateFileContext(
        string databasePath,
        IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath};Pooling=False");
        if (interceptor != null)
            options.AddInterceptors(interceptor);
        return new AppDbContext(options.Options);
    }

    private sealed class CancelAwareQueryBlocker : DbCommandInterceptor
    {
        private int _hasBlocked;

        public TaskCompletionSource Entered { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Cancelled { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _hasBlocked, 1) != 0)
                return result;

            Entered.TrySetResult();
            try
            {
                await Release.Task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Cancelled.TrySetResult();
                throw;
            }

            return result;
        }
    }

    private static async Task DeleteFileWhenUnlockedAsync(string path)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (true)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        Assert.True(condition(), "Expected asynchronous refresh result was not observed.");
    }
}
