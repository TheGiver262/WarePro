using System;
using System.Linq;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using BCrypt.Net;

namespace QuanLyHangHoa.Services
{
    public class AuthenticationService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public AuthenticationService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        private void WriteAudit(AppDbContext db, string entityName, int entityId, string actionCode, int performedBy)
        {
            try
            {
                db.AuditLogs.Add(new AuditLog
                {
                    EntityName = entityName,
                    EntityId = entityId,
                    ActionCode = actionCode,
                    PerformedBy = performedBy,
                    PerformedAt = DateTime.Now
                });
                db.SaveChanges();
            }
            catch
            {
                // Tránh lỗi ghi log làm gián đoạn luồng chính
            }
        }

        public LoginResult Authenticate(string username, string password)
        {
            using var db = _contextFactory();
            // Query user (DB might be case-insensitive depending on collation)
            var user = db.AppUsers.FirstOrDefault(u => u.Username == username);
            
            // Strict case-sensitive check in application logic
            if (user == null || user.Username != username)
            {
                return LoginResult.Invalid(0);
            }

            if (!user.IsActive)
            {
                WriteAudit(db, "AppUser", user.Id, "LoginFailed", user.Id);
                return LoginResult.Inactive();
            }
            
            // Check lockout
            if (user.LockoutUntil.HasValue && user.LockoutUntil.Value > DateTime.Now)
                return LoginResult.Locked(user.LockoutUntil);

            var stored = user.PasswordHash ?? "";
            bool verified = false;

            try 
            {
                // Standard BCrypt verification
                if (stored.StartsWith("$2") && stored.Contains('$'))
                {
                    verified = BCrypt.Net.BCrypt.Verify(password, stored);
                }

                if (verified)
                {
                    user.LastLoginAt = DateTime.Now;
                    user.FailedLoginCount = 0;
                    user.LockoutUntil = null;
                    db.SaveChanges();
                    return LoginResult.Success(user);
                }
                else
                {
                    // Increment failed attempts and handle lockout
                    user.FailedLoginCount++;
                    user.LastFailedLoginAt = DateTime.Now;
                    
                    if (user.FailedLoginCount >= 10)
                    {
                        user.LockoutUntil = DateTime.Now.AddMinutes(15);
                        db.SaveChanges();
                        WriteAudit(db, "AppUser", user.Id, "SuspiciousLoginAttempt", user.Id);
                        return LoginResult.Locked(user.LockoutUntil);
                    }
                    else if (user.FailedLoginCount >= 5)
                    {
                        user.LockoutUntil = DateTime.Now.AddMinutes(5);
                        db.SaveChanges();
                        WriteAudit(db, "AppUser", user.Id, "LoginLocked", user.Id);
                        return LoginResult.Locked(user.LockoutUntil);
                    }
                    else
                    {
                        db.SaveChanges();
                        WriteAudit(db, "AppUser", user.Id, "LoginFailed", user.Id);
                        return LoginResult.Invalid(user.FailedLoginCount);
                    }
                }
            }
            catch
            {
                // In case of invalid hash format or other errors
            }

            return LoginResult.Invalid(user?.FailedLoginCount ?? 0);
        }

        public void ChangePassword(int userId, string currentPassword, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword))
            {
                throw new InvalidOperationException("New password is required.");
            }

            using var db = _contextFactory();
            var user = db.AppUsers.Find(userId)
                ?? throw new InvalidOperationException("User does not exist.");

            if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            {
                throw new InvalidOperationException("Mật khẩu hiện tại không chính xác.");
            }

            if (currentPassword == newPassword)
            {
                throw new InvalidOperationException("Mật khẩu mới không được trùng với mật khẩu cũ.");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.LastPasswordChangedAt = DateTime.Now;
            user.MustChangePassword = false;
            db.SaveChanges();
        }
    }
}
