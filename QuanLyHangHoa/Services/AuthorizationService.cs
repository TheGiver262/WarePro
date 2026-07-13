using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
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
        ManageAuditLogs,
        ApproveStock
    }

    public class AuthorizationService
    {
        private static readonly Dictionary<string, HashSet<PermissionAction>> RolePermissions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Quản trị viên"] = AllPermissions(),

            ["Quản lý"] = AllPermissionsExcept(PermissionAction.ManageUsers),

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


        public static AppUser RequireFreshActor(
            AppDbContext db,
            int actorId,
            PermissionAction action)
        {
            ArgumentNullException.ThrowIfNull(db);
            var actor = db.AppUsers.AsNoTracking().SingleOrDefault(user => user.Id == actorId);
            if (!CanPerform(actor, action))
            {
                throw new InvalidOperationException("The current user is not authorized for this action.");
            }
            return actor!;
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
