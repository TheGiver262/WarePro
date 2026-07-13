using QuanLyHangHoa.ViewModels;
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
    public void Privileged_command_reloads_changed_role_and_invalidates_session()
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
        service.UpdateUser(10, User(10, "Quản lý"), performedByUserId: 11);

        Assert.False(viewModel!.OpenAppUserViewCommand.CanExecute(null));
        Assert.True(invalidated);
        Assert.NotSame(loginIdentity, viewModel.CurrentUser);
        Assert.Equal("Quản lý", viewModel.CurrentUser!.RoleCode);
    }

    [Theory]
    [InlineData("user")]
    [InlineData("audit")]
    [InlineData("stock")]
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
