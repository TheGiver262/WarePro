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
        var admin = new Employee { Role = "Admin" };

        Assert.True(service.CanPerform(admin, PermissionAction.ManageUsers));
        Assert.True(service.CanPerform(admin, PermissionAction.PostStockAdjustment));
        Assert.True(service.CanPerform(admin, PermissionAction.CreateWarrantyClaim));
    }

    [Fact]
    public void Sales_staff_can_create_sales_invoice_but_cannot_post_stock_adjustment()
    {
        var service = new AuthorizationService();
        var sales = new Employee { Role = "SalesStaff" };

        Assert.True(service.CanPerform(sales, PermissionAction.CreateSalesInvoice));
        Assert.False(service.CanPerform(sales, PermissionAction.PostStockAdjustment));
    }

    [Fact]
    public void Warehouse_staff_can_post_stock_documents_and_create_purchase_invoice()
    {
        var service = new AuthorizationService();
        var warehouse = new Employee { Role = "WarehouseStaff" };

        Assert.True(service.CanPerform(warehouse, PermissionAction.PostStockIn));
        Assert.True(service.CanPerform(warehouse, PermissionAction.PostStockOut));
        Assert.True(service.CanPerform(warehouse, PermissionAction.CreatePurchaseInvoice));
        Assert.False(service.CanPerform(warehouse, PermissionAction.ManageUsers));
    }
}
