using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using Xunit;

namespace QuanLyHangHoa.Tests.Services;

public class AuthorizationServiceTests
{
    [Fact]
    public void Admin_can_perform_every_known_action()
    {
        var admin = new AppUser { RoleCode = "Admin", IsActive = true };

        Assert.True(AuthorizationService.CanPerform(admin, PermissionAction.ManageUsers));
        Assert.True(AuthorizationService.CanPerform(admin, PermissionAction.PostStockAdjustment));
        Assert.True(AuthorizationService.CanPerform(admin, PermissionAction.CreateWarrantyClaim));
    }

    [Fact]
    public void Staff_can_create_sales_invoice_but_cannot_manage_users()
    {
        var staff = new AppUser { RoleCode = "Staff", IsActive = true };

        Assert.True(AuthorizationService.CanPerform(staff, PermissionAction.CreateSalesInvoice));
        Assert.False(AuthorizationService.CanPerform(staff, PermissionAction.ManageUsers));
    }

    [Fact]
    public void Inactive_user_cannot_perform_any_action()
    {
        var inactiveAdmin = new AppUser { RoleCode = "Admin", IsActive = false };

        Assert.False(AuthorizationService.CanPerform(inactiveAdmin, PermissionAction.ManageUsers));
    }
}
