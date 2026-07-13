using QuanLyHangHoa.ViewModels;
using System.Runtime.ExceptionServices;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;


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
