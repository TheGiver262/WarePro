using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;

namespace QuanLyHangHoa.Services
{
    public class AppUserService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public AppUserService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public List<AppUser> GetAllUsers()
        {
            using var db = _contextFactory();
            return db.AppUsers.AsNoTracking().ToList();
        }

        public void AddUser(AppUser user, int performedByUserId)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable);
            var actor = db.AppUsers.SingleOrDefault(existingUser => existingUser.Id == performedByUserId);
            EnsureActorCanManageUsers(actor);
            
            // Check for duplicate username
            if (db.AppUsers.Any(u => u.Username == user.Username))
            {
                throw new InvalidOperationException($"Tên tài khoản '{user.Username}' đã tồn tại trong hệ thống. Vui lòng chọn tên khác.");
            }

            // Default password is username if not set
            if (string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.Username);
            }
            else
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);
            }

            user.CreatedBy = performedByUserId;
            user.CreatedAt = DateTime.Now;
            user.MustChangePassword = true; 
            
            try
            {
                db.AppUsers.Add(user);
                db.SaveChanges();

                // Capture the new user state for audit using anonymous object to avoid serialization cycles
                var newState = new { user.Username, user.FullName, user.RoleCode, user.IsActive };
                AddAudit(db, "AppUser", user.Id, "CREATE", performedByUserId, null, newState);
                db.SaveChanges();
                transaction.Commit();
            }
            catch (Exception ex)
            {
                var message = ex.InnerException?.Message ?? ex.Message;
                throw new Exception($"Lỗi khi lưu người dùng: {message}", ex);
            }
        }

        public void UpdateUser(int targetUserId, AppUser updatedUser, int performedByUserId)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable);
            var actor = db.AppUsers.SingleOrDefault(user => user.Id == performedByUserId);
            EnsureActorCanManageUsers(actor);
            var existing = db.AppUsers.Find(targetUserId);
            if (existing != null)
            {
                var isSelf = targetUserId == performedByUserId;

                if (isSelf && existing.IsActive && !updatedUser.IsActive)
                {
                    throw new InvalidOperationException("Bạn không thể tự dừng tài khoản của chính mình.");
                }

                if (isSelf && IsAdministrator(existing) && !IsAdministrator(updatedUser))
                {
                    throw new InvalidOperationException("Bạn không thể tự hạ quyền quản trị của chính mình.");
                }

                if (IsActiveAdministrator(existing) && !IsActiveAdministrator(updatedUser))
                {
                    EnsureAnotherActiveAdministrator(db, targetUserId);
                }

                // Capture old state
                var oldState = new { existing.FullName, existing.RoleCode, existing.IsActive };
                
                existing.FullName = updatedUser.FullName;
                existing.RoleCode = updatedUser.RoleCode;
                existing.IsActive = updatedUser.IsActive;

                // Optional: update password if provided in plaintext (handle with care in UI)
                if (!string.IsNullOrWhiteSpace(updatedUser.PasswordHash) && !updatedUser.PasswordHash.StartsWith("$2"))
                {
                    existing.PasswordHash = BCrypt.Net.BCrypt.HashPassword(updatedUser.PasswordHash);
                    existing.LastPasswordChangedAt = DateTime.Now;
                }
                
                db.SaveChanges();
                
                // Capture new state
                var newState = new { existing.FullName, existing.RoleCode, existing.IsActive };
                
                AddAudit(db, "AppUser", existing.Id, "UPDATE", performedByUserId, oldState, newState);
                db.SaveChanges();
                transaction.Commit();
            }
        }

        public void ToggleUserStatus(int userId, int performedByUserId)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable);
            var actor = db.AppUsers.SingleOrDefault(existingUser => existingUser.Id == performedByUserId);
            EnsureActorCanManageUsers(actor);
            var user = db.AppUsers.Find(userId);
            if (user != null)
            {
                if (user.IsActive && userId == performedByUserId)
                {
                    throw new InvalidOperationException("Bạn không thể tự dừng tài khoản của chính mình.");
                }

                if (IsActiveAdministrator(user))
                {
                    EnsureAnotherActiveAdministrator(db, userId);
                }
                var oldState = new { user.IsActive, user.Username };
                user.IsActive = !user.IsActive;
                
                string action = user.IsActive ? "ACTIVATE" : "DEACTIVATE";
                
                db.SaveChanges();
                
                var newState = new { user.IsActive, user.Username };
                AddAudit(db, "AppUser", user.Id, action, performedByUserId, oldState, newState);
                db.SaveChanges();
                transaction.Commit();
            }
        }

        public void DeleteUser(int id, int performedByUserId)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable);
            var actor = db.AppUsers.SingleOrDefault(user => user.Id == performedByUserId);
            EnsureActorCanManageUsers(actor);

            if (id == performedByUserId)
            {
                throw new InvalidOperationException("Bạn không thể tự xoá tài khoản của chính mình.");
            }

            var user = db.AppUsers.Find(id);
            if (user == null)
            {
                return;
            }

            if (IsActiveAdministrator(user))
            {
                EnsureAnotherActiveAdministrator(db, id);
            }

            try
            {
                // Capture state before delete for audit
                var oldState = new { user.Username, user.FullName, user.RoleCode, user.IsActive };

                if (HasDependencies(db, id))
                {
                    user.IsActive = false;
                    db.SaveChanges();
                    AddAudit(
                        db,
                        "AppUser",
                        id,
                        "DEACTIVATE",
                        performedByUserId,
                        oldState,
                        new { user.Username, user.FullName, user.RoleCode, user.IsActive });
                }
                else
                {
                    db.AppUsers.Remove(user);
                    db.SaveChanges();
                    AddAudit(db, "AppUser", id, "DELETE", performedByUserId, oldState, null);
                }

                db.SaveChanges();
                transaction.Commit();
            }
            catch (Exception ex)
            {
                var message = ex.InnerException?.Message ?? ex.Message;
                throw new Exception($"Lỗi khi xoá người dùng: {message}", ex);
            }
        }


        private static void EnsureActorCanManageUsers(AppUser? actor)
        {
            if (actor == null || !IsActiveAdministrator(actor))
            {
                throw new InvalidOperationException("Only an active administrator can manage users.");
            }
        }

        private static bool IsAdministrator(AppUser user) =>
            string.Equals(user.RoleCode, "Quản trị viên", StringComparison.OrdinalIgnoreCase);

        private static bool IsActiveAdministrator(AppUser user) =>
            user.IsActive && IsAdministrator(user);

        private static void EnsureAnotherActiveAdministrator(AppDbContext db, int targetUserId)
        {
            var anotherActiveAdministratorExists = db.AppUsers.Any(user =>
                user.Id != targetUserId &&
                user.IsActive &&
                user.RoleCode == "Quản trị viên");

            if (!anotherActiveAdministratorExists)
            {
                throw new InvalidOperationException("Hệ thống phải luôn có ít nhất một quản trị viên đang hoạt động.");
            }
        }
        public bool HasDependencies(int userId)
        {
            using var db = _contextFactory();
            return HasDependencies(db, userId);
        }

        private static bool HasDependencies(AppDbContext db, int userId)
        {
            return db.AppUsers.Any(user => user.CreatedBy == userId) ||
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
                   db.WarrantyClaims.Any(claim =>
                       claim.ApprovedBy == userId || claim.ProcessedBy == userId);
        }

        private void AddAudit(AppDbContext db, string entityName, int entityId, string action, int userId, object? oldValues = null, object? newValues = null)
        {
            var log = new AuditLog
            {
                EntityName = entityName,
                EntityId = entityId,
                ActionCode = action,
                PerformedBy = userId,
                PerformedAt = DateTime.Now,
                BeforeJson = oldValues != null ? System.Text.Json.JsonSerializer.Serialize(oldValues) : null,
                AfterJson = newValues != null ? System.Text.Json.JsonSerializer.Serialize(newValues) : null
            };
            db.AuditLogs.Add(log);
        }
    }
}
