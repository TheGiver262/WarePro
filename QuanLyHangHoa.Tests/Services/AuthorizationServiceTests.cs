using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using Xunit;

namespace QuanLyHangHoa.Tests.Services;

public class AuthorizationServiceTests
{
    [Fact]
    public void Admin_can_perform_every_known_action()
    {
        var service = new AuthorizationService();
        var admin = new AppUser { RoleCode = "Admin", IsActive = true };

        Assert.True(service.CanPerform(admin, PermissionAction.ManageUsers));
        Assert.True(service.CanPerform(admin, PermissionAction.PostStockAdjustment));
        Assert.True(service.CanPerform(admin, PermissionAction.CreateWarrantyClaim));
    }

    [Fact]
    public void Staff_can_create_sales_invoice_but_cannot_manage_users()
    {
        var service = new AuthorizationService();
        var staff = new AppUser { RoleCode = "Staff", IsActive = true };

        Assert.True(service.CanPerform(staff, PermissionAction.CreateSalesInvoice));
        Assert.False(service.CanPerform(staff, PermissionAction.ManageUsers));
    }

    [Fact]
    public void Inactive_user_cannot_perform_any_action()
    {
        var service = new AuthorizationService();
        var inactiveAdmin = new AppUser { RoleCode = "Admin", IsActive = false };

        Assert.False(service.CanPerform(inactiveAdmin, PermissionAction.ManageUsers));
    }
}
