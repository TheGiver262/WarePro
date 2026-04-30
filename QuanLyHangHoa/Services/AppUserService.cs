using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using BCrypt.Net;

namespace QuanLyHangHoa.Services
{
    public class AppUserService
    {
        private readonly AppDbContext? _persistentContext;
        private readonly Func<AppDbContext> _contextFactory;

        public AppUserService()
            : this(() => new AppDbContext())
        {
        }

        public AppUserService(AppDbContext persistentContext)
        {
            _persistentContext = persistentContext;
            _contextFactory = () => _persistentContext;
        }

        public AppUserService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public List<AppUser> GetAllUsers()
        {
            var db = _contextFactory();
            return db.AppUsers.ToList();
        }

        public void AddUser(AppUser user, int performedByUserId)
        {
            var db = _contextFactory();
            
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
            user.CreatedAt = DateTime.UtcNow;
            
            db.AppUsers.Add(user);
            db.SaveChanges();

            AddAudit(db, "AppUser", user.Id, "Create", performedByUserId);
            db.SaveChanges();
        }

        public void UpdateUser(AppUser updatedUser, int performedByUserId)
        {
            var db = _contextFactory();
            var user = db.AppUsers.Find(updatedUser.Id);
            if (user == null)
            {
                throw new InvalidOperationException("User not found.");
            }

            user.FullName = updatedUser.FullName;
            user.RoleCode = updatedUser.RoleCode;
            user.IsActive = updatedUser.IsActive;

            // Optional: update password if provided in plaintext (handle with care in UI)
            if (!string.IsNullOrWhiteSpace(updatedUser.PasswordHash) && !updatedUser.PasswordHash.StartsWith("$2"))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(updatedUser.PasswordHash);
                user.LastPasswordChangedAt = DateTime.UtcNow;
            }

            db.SaveChanges();
            AddAudit(db, "AppUser", user.Id, "Update", performedByUserId);
            db.SaveChanges();
        }

        public void DeleteUser(int id, int performedByUserId)
        {
            if (id == 1) // Prevent deleting super admin
            {
                throw new InvalidOperationException("Cannot delete the system administrator.");
            }

            var db = _contextFactory();
            var user = db.AppUsers.Find(id);
            if (user == null)
            {
                return;
            }

            // Soft delete or hard delete? Schema doesn't have IsDeleted for AppUser.
            // But we can deactivate it.
            user.IsActive = false;
            
            db.SaveChanges();
            AddAudit(db, "AppUser", id, "Deactivate", performedByUserId);
            db.SaveChanges();
        }

        private void AddAudit(AppDbContext db, string entityName, int entityId, string action, int performedByUserId)
        {
            db.AuditLogs.Add(new AuditLog
            {
                EntityName = entityName,
                EntityId = entityId,
                ActionCode = action,
                PerformedBy = performedByUserId,
                PerformedAt = DateTime.UtcNow
            });
        }
    }
}
