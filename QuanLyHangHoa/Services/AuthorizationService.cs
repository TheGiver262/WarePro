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
        ViewReports,
        ManageMasterData,
        ManageAuditLogs
    }

    public class AuthorizationService
    {
        private static readonly Dictionary<string, HashSet<PermissionAction>> RolePermissions = new(StringComparer.OrdinalIgnoreCase)
        {
            // Admin Role (Full Access)
            ["Admin"] = AllPermissions(),
            ["Quản trị viên"] = AllPermissions(),

            // Manager Role (Full Access except User Management)
            ["Manager"] = AllPermissionsExcept(PermissionAction.ManageUsers),
            ["Quản lý"] = AllPermissionsExcept(PermissionAction.ManageUsers),

            // Staff Roles (Specific tasks, View Reports, NO Master Data management)
            ["Staff"] = ViewOnlyPermissions(),
            ["Nhân viên bảo hành"] = new()
            {
                PermissionAction.CreateWarrantyClaim,
                PermissionAction.ViewReports
            },
            ["Nhân viên bán hàng"] = new()
            {
                PermissionAction.CreateSalesInvoice,
                PermissionAction.ViewReports
            },
            ["Nhân viên kho"] = new()
            {
                PermissionAction.PostStockIn,
                PermissionAction.PostStockOut,
                PermissionAction.PostStockAdjustment,
                PermissionAction.ViewReports
            }
        };

        private static HashSet<PermissionAction> ViewOnlyPermissions()
        {
            return new HashSet<PermissionAction>
            {
                PermissionAction.ViewReports
            };
        }

        public static bool CanPerform(AppUser? user, PermissionAction action)
        {
            if (user == null || !user.IsActive || string.IsNullOrWhiteSpace(user.RoleCode))
            {
                return false;
            }

            return RolePermissions.TryGetValue(user.RoleCode, out var permissions)
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
