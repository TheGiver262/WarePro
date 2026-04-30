using System;
using System.Linq;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using BCrypt.Net;

namespace QuanLyHangHoa.Services
{
    public class AuthenticationService
    {
        private readonly AppDbContext? _persistentContext;
        private readonly Func<AppDbContext> _contextFactory;

        public AuthenticationService()
            : this(() => new AppDbContext())
        {
        }

        public AuthenticationService(AppDbContext persistentContext)
        {
            _persistentContext = persistentContext;
            _contextFactory = () => _persistentContext;
        }

        public AuthenticationService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public LoginResult Authenticate(string username, string password)
        {
            var db = _contextFactory();
            var user = db.AppUsers.FirstOrDefault(u => u.Username == username);
            
            if (user == null)
            {
                return LoginResult.Invalid();
            }

            if (!user.IsActive)
            {
                return LoginResult.Inactive();
            }

            // Check if user is currently locked out
            if (user.LockoutUntil.HasValue && user.LockoutUntil.Value > DateTime.UtcNow)
            {
                return LoginResult.Locked(user.LockoutUntil);
            }

            // Verify password using BCrypt
            if (BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                // Success: Update last login and reset failed count
                user.LastLoginAt = DateTime.UtcNow;
                user.FailedLoginCount = 0;
                user.LockoutUntil = null;
                db.SaveChanges();
                return LoginResult.Success(user);
            }
            else
            {
                // Failure: Increment failed login count
                user.FailedLoginCount++;
                user.LastFailedLoginAt = DateTime.UtcNow;
                
                // Progressive lockout logic:
                if (user.FailedLoginCount == 5)
                {
                    // First level: 5 minutes
                    user.LockoutUntil = DateTime.UtcNow.AddMinutes(5);
                }
                else if (user.FailedLoginCount == 10)
                {
                    // Second level: 15 minutes
                    user.LockoutUntil = DateTime.UtcNow.AddMinutes(15);
                }
                else if (user.FailedLoginCount > 10)
                {
                    // Final level: Disable account and log security incident
                    user.IsActive = false;
                    db.AuditLogs.Add(new AuditLog
                    {
                        EntityName = "AppUser",
                        EntityId = user.Id,
                        ActionCode = "SECURITY_BREACH_LOCKOUT",
                        AfterJson = $"User '{user.Username}' account disabled after {user.FailedLoginCount} consecutive failed login attempts.",
                        PerformedBy = user.Id,
                        PerformedAt = DateTime.UtcNow
                    });
                }
                
                db.SaveChanges();

                if (!user.IsActive)
                {
                    return LoginResult.Inactive();
                }

                if (user.LockoutUntil.HasValue && user.LockoutUntil.Value > DateTime.UtcNow)
                {
                    return LoginResult.Locked(user.LockoutUntil);
                }

                return LoginResult.Invalid();
            }
        }

        public void ChangePassword(int userId, string currentPassword, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword))
            {
                throw new InvalidOperationException("New password is required.");
            }

            var db = _contextFactory();
            var user = db.AppUsers.Find(userId)
                ?? throw new InvalidOperationException("User does not exist.");

            if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            {
                throw new InvalidOperationException("Current password is incorrect.");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.LastPasswordChangedAt = DateTime.UtcNow;
            user.MustChangePassword = false;
            db.SaveChanges();
        }
    }
}
