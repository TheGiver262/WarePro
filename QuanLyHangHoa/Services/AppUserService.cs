using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class AppUserService
    {
        private const string AdministratorRole = "Quản trị viên";
        private readonly Func<AppDbContext> _contextFactory;
        private readonly DatabaseWriteExecutor _writeExecutor;

        public AppUserService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            _writeExecutor = new DatabaseWriteExecutor(contextFactory);
        }

        public List<AppUser> GetAllUsers()
        {
            using var db = _contextFactory();
            return db.AppUsers.AsNoTracking().ToList();
        }

        public Task<int> AddUserAsync(
            AppUser user,
            int performedByUserId,
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(user);
            // tách scalar khỏi model đầu vào vì callback có thể chạy lại với context mới
            var requestedId = user.Id;
            var username = user.Username.Trim();
            var fullName = user.FullName.Trim();
            var roleCode = user.RoleCode;
            var isActive = user.IsActive;
            var password = string.IsNullOrWhiteSpace(user.PasswordHash)
                ? username
                : user.PasswordHash;
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
            var createdAt = DateTime.Now;

            return _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest(
                    "app-user.add",
                    operationId,
                    IsolationLevel.Serializable),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(
                        db,
                        performedByUserId,
                        PermissionAction.ManageUsers);

                    if (await db.AppUsers.AnyAsync(item => item.Username == username, token))
                    {
                        throw new InvalidOperationException(
                            $"Tên tài khoản '{username}' đã tồn tại trong hệ thống. Vui lòng chọn tên khác.");
                    }

                    var created = new AppUser
                    {
                        Username = username,
                        PasswordHash = passwordHash,
                        FullName = fullName,
                        RoleCode = roleCode,
                        IsActive = isActive,
                        MustChangePassword = true,
                        CreatedBy = performedByUserId,
                        CreatedAt = createdAt
                    };
                    if (requestedId > 0)
                    {
                        created.Id = requestedId;
                    }

                    db.AppUsers.Add(created);
                    // cần flush để lấy id do DB sinh; audit vẫn được executor commit cùng transaction với user
                    await db.SaveChangesAsync(token);
                    AddAudit(
                        db,
                        created.Id,
                        "CREATE",
                        performedByUserId,
                        null,
                        Snapshot(created));
                    return created.Id;
                },
                (db, token) => db.AppUsers.AnyAsync(item =>
                    item.Username == username &&
                    item.FullName == fullName &&
                    item.RoleCode == roleCode &&
                    item.IsActive == isActive &&
                    item.CreatedBy == performedByUserId,
                    token),
                cancellationToken: cancellationToken);
        }

        public Task UpdateUserAsync(
            int targetUserId,
            AppUser updatedUser,
            byte[] expectedRowVersion,
            int performedByUserId,
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(updatedUser);
            ArgumentNullException.ThrowIfNull(expectedRowVersion);
            var rowVersion = expectedRowVersion.ToArray();
            var fullName = updatedUser.FullName.Trim();
            var roleCode = updatedUser.RoleCode;
            var isActive = updatedUser.IsActive;
            var passwordHash = !string.IsNullOrWhiteSpace(updatedUser.PasswordHash) &&
                !updatedUser.PasswordHash.StartsWith("$2", StringComparison.Ordinal)
                    ? BCrypt.Net.BCrypt.HashPassword(updatedUser.PasswordHash)
                    : null;
            var changedAt = passwordHash is null ? (DateTime?)null : DateTime.Now;

            // Serializable giữ kiểm tra "quản trị viên cuối cùng" và thay đổi quyền trong cùng một lát cắt dữ liệu
            return _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest(
                    "app-user.update",
                    operationId,
                    IsolationLevel.Serializable),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(
                        db,
                        performedByUserId,
                        PermissionAction.ManageUsers);
                    var existing = await db.AppUsers.SingleOrDefaultAsync(
                        item => item.Id == targetUserId,
                        token);
                    if (existing is null)
                    {
                        throw new StaleEntityException("Tài khoản đã bị xóa hoặc không còn tồn tại. Vui lòng tải lại dữ liệu.");
                    }

                    db.Entry(existing).Property(item => item.RowVersion).OriginalValue = rowVersion;
                    var requestedState = new AppUser
                    {
                        RoleCode = roleCode,
                        IsActive = isActive
                    };
                    var isSelf = targetUserId == performedByUserId;
                    if (isSelf && existing.IsActive && !isActive)
                    {
                        throw new InvalidOperationException("Bạn không thể tự dừng tài khoản của chính mình.");
                    }

                    if (isSelf && IsAdministrator(existing) && !IsAdministrator(requestedState))
                    {
                        throw new InvalidOperationException("Bạn không thể tự hạ quyền quản trị của chính mình.");
                    }

                    if (IsActiveAdministrator(existing) && !IsActiveAdministrator(requestedState))
                    {
                        await EnsureAnotherActiveAdministratorAsync(db, targetUserId, token);
                    }

                    var before = Snapshot(existing);
                    existing.FullName = fullName;
                    existing.RoleCode = roleCode;
                    existing.IsActive = isActive;
                    if (passwordHash is not null)
                    {
                        existing.PasswordHash = passwordHash;
                        existing.LastPasswordChangedAt = changedAt;
                    }

                    AddAudit(
                        db,
                        existing.Id,
                        "UPDATE",
                        performedByUserId,
                        before,
                        Snapshot(existing));
                },
                (db, token) => db.AppUsers.AnyAsync(item =>
                    item.Id == targetUserId &&
                    item.FullName == fullName &&
                    item.RoleCode == roleCode &&
                    item.IsActive == isActive &&
                    (passwordHash == null || item.PasswordHash == passwordHash) &&
                    item.RowVersion != rowVersion,
                    token),
                cancellationToken: cancellationToken);
        }

        public Task ToggleUserStatusAsync(
            int userId,
            byte[] expectedRowVersion,
            int performedByUserId,
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(expectedRowVersion);
            var rowVersion = expectedRowVersion.ToArray();
            // giá trị này nối callback ghi với callback xác minh khi kết quả commit không rõ; null nghĩa là chưa có user để đổi
            bool? resultingStatus = null;

            return _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest(
                    "app-user.toggle-status",
                    operationId,
                    IsolationLevel.Serializable),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(
                        db,
                        performedByUserId,
                        PermissionAction.ManageUsers);
                    var user = await db.AppUsers.SingleOrDefaultAsync(item => item.Id == userId, token);
                    if (user is null)
                    {
                        throw new StaleEntityException("Tài khoản đã bị xóa hoặc không còn tồn tại. Vui lòng tải lại dữ liệu.");
                    }

                    db.Entry(user).Property(item => item.RowVersion).OriginalValue = rowVersion;
                    if (user.IsActive && userId == performedByUserId)
                    {
                        throw new InvalidOperationException("Bạn không thể tự dừng tài khoản của chính mình.");
                    }

                    if (IsActiveAdministrator(user))
                    {
                        await EnsureAnotherActiveAdministratorAsync(db, userId, token);
                    }

                    var before = Snapshot(user);
                    user.IsActive = !user.IsActive;
                    resultingStatus = user.IsActive;
                    AddAudit(
                        db,
                        user.Id,
                        user.IsActive ? "ACTIVATE" : "DEACTIVATE",
                        performedByUserId,
                        before,
                        Snapshot(user));
                },
                (db, token) => resultingStatus.HasValue
                    ? db.AppUsers.AnyAsync(item =>
                        item.Id == userId &&
                        item.IsActive == resultingStatus.Value &&
                        item.RowVersion != rowVersion,
                        token)
                    : Task.FromResult(false),
                cancellationToken: cancellationToken);
        }

        public Task DeleteUserAsync(
            int id,
            byte[] expectedRowVersion,
            int performedByUserId,
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(expectedRowVersion);
            var rowVersion = expectedRowVersion.ToArray();

            return _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest(
                    "app-user.delete",
                    operationId,
                    IsolationLevel.Serializable),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(
                        db,
                        performedByUserId,
                        PermissionAction.ManageUsers);
                    if (id == performedByUserId)
                    {
                        throw new InvalidOperationException("Bạn không thể tự xoá tài khoản của chính mình.");
                    }

                    var user = await db.AppUsers.SingleOrDefaultAsync(item => item.Id == id, token);
                    if (user is null)
                    {
                        return;
                    }

                    // ép EF so khớp token client đã đọc; bản ghi đổi ở máy khác sẽ phát sinh lỗi concurrency thay vì bị ghi đè
                    db.Entry(user).Property(item => item.RowVersion).OriginalValue = rowVersion;
                    if (IsActiveAdministrator(user))
                    {
                        await EnsureAnotherActiveAdministratorAsync(db, id, token);
                    }

                    var before = Snapshot(user);
                    if (await HasDependenciesAsync(db, id, token))
                    {
                        user.IsActive = false;
                        AddAudit(
                            db,
                            id,
                            "DEACTIVATE",
                            performedByUserId,
                            before,
                            Snapshot(user));
                    }
                    else
                    {
                        db.AppUsers.Remove(user);
                        AddAudit(db, id, "DELETE", performedByUserId, before, null);
                    }
                },
                (db, token) => db.AppUsers.AllAsync(item => item.Id != id || !item.IsActive, token),
                cancellationToken: cancellationToken);
        }

        public bool HasDependencies(int userId)
        {
            using var db = _contextFactory();
            return HasDependencies(db, userId);
        }

        private static bool IsAdministrator(AppUser user) =>
            string.Equals(user.RoleCode, AdministratorRole, StringComparison.OrdinalIgnoreCase);

        private static bool IsActiveAdministrator(AppUser user) =>
            user.IsActive && IsAdministrator(user);

        private static async Task EnsureAnotherActiveAdministratorAsync(
            AppDbContext db,
            int targetUserId,
            CancellationToken cancellationToken)
        {
            var exists = await db.AppUsers.AnyAsync(user =>
                user.Id != targetUserId &&
                user.IsActive &&
                user.RoleCode == AdministratorRole,
                cancellationToken);
            if (!exists)
            {
                throw new InvalidOperationException(
                    "Hệ thống phải luôn có ít nhất một quản trị viên đang hoạt động.");
            }
        }

        private static bool HasDependencies(AppDbContext db, int userId) =>
            db.AppUsers.Any(user => user.CreatedBy == userId) ||
            db.AuditLogs.Any(log => log.PerformedBy == userId) ||
            db.PurchaseInvoices.Any(invoice => invoice.CreatedBy == userId) ||
            db.SalesInvoices.Any(invoice => invoice.CreatedBy == userId) ||
            db.StockAdjustments.Any(document =>
                document.CreatedBy == userId || document.ApprovedBy == userId || document.PostedBy == userId) ||
            db.StockCountSessions.Any(document =>
                document.CreatedBy == userId || document.ApprovedBy == userId || document.PostedBy == userId) ||
            db.StockIns.Any(document =>
                document.CreatedBy == userId || document.ApprovedBy == userId || document.PostedBy == userId) ||
            db.StockLedgers.Any(ledger => ledger.PostedBy == userId) ||
            db.StockOuts.Any(document =>
                document.CreatedBy == userId || document.ApprovedBy == userId || document.PostedBy == userId) ||
            db.StockTransfers.Any(document =>
                document.CreatedBy == userId || document.ApprovedBy == userId || document.PostedBy == userId) ||
            db.WarrantyClaims.Any(claim => claim.ApprovedBy == userId || claim.ProcessedBy == userId);

        private static async Task<bool> HasDependenciesAsync(
            AppDbContext db,
            int userId,
            CancellationToken cancellationToken) =>
            await db.AppUsers.AnyAsync(user => user.CreatedBy == userId, cancellationToken) ||
            await db.AuditLogs.AnyAsync(log => log.PerformedBy == userId, cancellationToken) ||
            await db.PurchaseInvoices.AnyAsync(invoice => invoice.CreatedBy == userId, cancellationToken) ||
            await db.SalesInvoices.AnyAsync(invoice => invoice.CreatedBy == userId, cancellationToken) ||
            await db.StockAdjustments.AnyAsync(document =>
                document.CreatedBy == userId || document.ApprovedBy == userId || document.PostedBy == userId,
                cancellationToken) ||
            await db.StockCountSessions.AnyAsync(document =>
                document.CreatedBy == userId || document.ApprovedBy == userId || document.PostedBy == userId,
                cancellationToken) ||
            await db.StockIns.AnyAsync(document =>
                document.CreatedBy == userId || document.ApprovedBy == userId || document.PostedBy == userId,
                cancellationToken) ||
            await db.StockLedgers.AnyAsync(ledger => ledger.PostedBy == userId, cancellationToken) ||
            await db.StockOuts.AnyAsync(document =>
                document.CreatedBy == userId || document.ApprovedBy == userId || document.PostedBy == userId,
                cancellationToken) ||
            await db.StockTransfers.AnyAsync(document =>
                document.CreatedBy == userId || document.ApprovedBy == userId || document.PostedBy == userId,
                cancellationToken) ||
            await db.WarrantyClaims.AnyAsync(claim =>
                claim.ApprovedBy == userId || claim.ProcessedBy == userId,
                cancellationToken);

        private static object Snapshot(AppUser user) => new
        {
            user.Username,
            user.FullName,
            user.RoleCode,
            user.IsActive
        };

        private static void AddAudit(
            AppDbContext db,
            int entityId,
            string action,
            int userId,
            object? oldValues,
            object? newValues) =>
            db.AuditLogs.Add(new AuditLog
            {
                EntityName = "AppUser",
                EntityId = entityId,
                ActionCode = action,
                PerformedBy = userId,
                PerformedAt = DateTime.Now,
                BeforeJson = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
                AfterJson = newValues is null ? null : JsonSerializer.Serialize(newValues)
            });
    }
}
