using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using Xunit;
using System.Reflection;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Tests.Services;

public class AuthorizationServiceTests
{
    [Theory]
    [InlineData(typeof(AppUserViewModel))]
    [InlineData(typeof(MainViewModel))]
    [InlineData(typeof(StockInViewModel))]
    [InlineData(typeof(StockOutViewModel))]
    [InlineData(typeof(StockTransferViewModel))]
    [InlineData(typeof(StockAdjustmentViewModel))]
    [InlineData(typeof(StockCountViewModel))]
    [InlineData(typeof(StockReversalViewModel))]
    [InlineData(typeof(SalesInvoiceViewModel))]
    [InlineData(typeof(PurchaseInvoiceViewModel))]
    public void Authenticated_view_models_reject_null_user(Type viewModelType)
    {
        var constructor = viewModelType.GetConstructor([typeof(AppUser), typeof(Func<AppDbContext>)]);

        Assert.NotNull(constructor);
        Func<AppDbContext> unusedFactory = () => throw new InvalidOperationException("Context must not be created for a null user.");
        var exception = Assert.Throws<TargetInvocationException>(() =>
            constructor.Invoke([null, unusedFactory]));

        Assert.IsType<ArgumentNullException>(exception.InnerException);
    }

    [Fact]
    public void Admin_can_perform_every_known_action()
    {
        var admin = new AppUser { RoleCode = "Quản trị viên", IsActive = true };

        Assert.True(AuthorizationService.CanPerform(admin, PermissionAction.ManageUsers));
        Assert.True(AuthorizationService.CanPerform(admin, PermissionAction.PostStockAdjustment));
        Assert.True(AuthorizationService.CanPerform(admin, PermissionAction.CreateWarrantyClaim));
    }

    [Fact]
    public void Staff_can_create_sales_invoice_but_cannot_manage_users()
    {
        var staff = new AppUser { RoleCode = "Nhân viên bán hàng", IsActive = true };

        Assert.True(AuthorizationService.CanPerform(staff, PermissionAction.CreateSalesInvoice));
        Assert.False(AuthorizationService.CanPerform(staff, PermissionAction.ManageUsers));
    }

    [Theory]
    [InlineData("Nhân viên bán hàng", PermissionAction.CreateSalesInvoice)]
    [InlineData("Nhân viên kho", PermissionAction.PostStockIn)]
    [InlineData("Nhân viên kho", PermissionAction.PostStockOut)]
    [InlineData("Nhân viên bảo hành", PermissionAction.CreateWarrantyClaim)]
    public void Staff_role_can_only_access_its_workflow(string role, PermissionAction allowedAction)
    {
        var user = new AppUser { RoleCode = role, IsActive = true };

        Assert.True(AuthorizationService.CanPerform(user, allowedAction));
        Assert.False(AuthorizationService.CanPerform(user, PermissionAction.ManageUsers));
        Assert.False(AuthorizationService.CanPerform(user, PermissionAction.ManageMasterData));
    }

    [Fact]
    public void Inactive_user_cannot_perform_any_action()
    {
        var inactiveAdmin = new AppUser { RoleCode = "Quản trị viên", IsActive = false };

        Assert.False(AuthorizationService.CanPerform(inactiveAdmin, PermissionAction.ManageUsers));
    }

    [Fact]
    public void Manager_can_access_business_workflows_but_cannot_manage_users()
    {
        var manager = new AppUser { RoleCode = "Quản lý", IsActive = true };

        Assert.False(AuthorizationService.CanPerform(manager, PermissionAction.ManageUsers));
        Assert.All(
            Enum.GetValues<PermissionAction>().Where(action => action != PermissionAction.ManageUsers),
            action => Assert.True(AuthorizationService.CanPerform(manager, action)));
    }
}
