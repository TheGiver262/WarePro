using System;
using System.Collections.Generic;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public enum PermissionAction
    {
        ManageUsers,
        PostStockIn,
        PostStockOut,
        PostStockAdjustment,
        CreatePurchaseInvoice,
        CreateSalesInvoice,
        CreateWarrantyClaim,
        ViewReports
    }

    public class AuthorizationService
    {
        private static readonly Dictionary<string, HashSet<PermissionAction>> RolePermissions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Admin"] = AllPermissions(),
            ["Manager"] = AllPermissionsExcept(PermissionAction.ManageUsers),
            ["WarehouseStaff"] = new()
            {
                PermissionAction.PostStockIn,
                PermissionAction.PostStockOut,
                PermissionAction.PostStockAdjustment,
                PermissionAction.CreatePurchaseInvoice,
                PermissionAction.ViewReports
            },
            ["SalesStaff"] = new()
            {
                PermissionAction.PostStockOut,
                PermissionAction.CreateSalesInvoice,
                PermissionAction.ViewReports
            },
            ["WarrantyStaff"] = new()
            {
                PermissionAction.CreateWarrantyClaim,
                PermissionAction.ViewReports
            },
            ["Staff"] = new()
            {
                PermissionAction.PostStockIn,
                PermissionAction.PostStockOut,
                PermissionAction.CreatePurchaseInvoice,
                PermissionAction.CreateSalesInvoice,
                PermissionAction.CreateWarrantyClaim
            }
        };

        public bool CanPerform(Employee? employee, PermissionAction action)
        {
            if (employee is null || string.IsNullOrWhiteSpace(employee.Role))
            {
                return false;
            }

            return RolePermissions.TryGetValue(employee.Role, out var permissions)
                && permissions.Contains(action);
        }

        private static HashSet<PermissionAction> AllPermissions()
        {
            return new HashSet<PermissionAction>((PermissionAction[])Enum.GetValues(typeof(PermissionAction)));
        }

        private static HashSet<PermissionAction> AllPermissionsExcept(params PermissionAction[] excluded)
        {
            var permissions = AllPermissions();
            foreach (var action in excluded)
            {
                permissions.Remove(action);
            }

            return permissions;
        }
    }
}
