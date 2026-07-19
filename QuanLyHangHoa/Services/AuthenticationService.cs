using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using BCrypt.Net;

namespace QuanLyHangHoa.Services
{
    public class AuthenticationService
    {
        private enum LoginWriteKind
        {
            None,
            Audit,
            FailedAttempt,
            Success
        }

        private readonly DatabaseWriteExecutor _writeExecutor;

        public AuthenticationService(Func<AppDbContext> contextFactory)
        {
            _writeExecutor = new DatabaseWriteExecutor(contextFactory);
        }

        // log đăng nhập không gắn PerformedBy vì chưa có danh tính đã xác thực; chỉ lưu username đã thử
        private static void AddLoginAudit(
            AppDbContext db,
            string actionCode,
            string attemptedUsername,
            DateTime performedAt)
        {
            db.AuditLogs.Add(new AuditLog
            {
                EntityName = "Authentication",
                EntityId = 0,
                ActionCode = actionCode,
                PerformedBy = null,
                PerformedAt = performedAt,
                AfterJson = JsonSerializer.Serialize(new { attemptedUsername })
            });
        }
        public Task<LoginResult> AuthenticateAsync(
            string username,
            string password,
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            var attemptedUsername = username;
            var attemptedPassword = password;
            var attemptedAt = DateTime.Now;
            var auditPayload = JsonSerializer.Serialize(new { attemptedUsername });
            var writeKind = LoginWriteKind.None;

            return _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("authentication.login", operationId),
                async (db, token) =>
                {
                    var user = await db.AppUsers.SingleOrDefaultAsync(
                        item => item.Username == attemptedUsername,
                        token);

                    if (user is null || user.Username != attemptedUsername)
                    {
                        writeKind = LoginWriteKind.Audit;
                        AddLoginAudit(db, "LoginFailed", attemptedUsername, attemptedAt);
                        return LoginResult.Invalid(0);
                    }

                    if (!user.IsActive)
                    {
                        writeKind = LoginWriteKind.Audit;
                        AddLoginAudit(db, "LoginFailed", attemptedUsername, attemptedAt);
                        return LoginResult.Inactive();
                    }

                    if (user.LockoutUntil.HasValue && user.LockoutUntil.Value > attemptedAt)
                    {
                        return LoginResult.Locked(user.LockoutUntil);
                    }

                    var verified = false;
                    try
                    {
                        var stored = user.PasswordHash ?? string.Empty;
                        if (stored.StartsWith("$2", StringComparison.Ordinal) && stored.Contains('$'))
                        {
                            verified = BCrypt.Net.BCrypt.Verify(attemptedPassword, stored);
                        }
                    }
                    catch
                    {
                        verified = false;
                    }

                    if (verified)
                    {
                        writeKind = LoginWriteKind.Success;
                        user.LastLoginAt = attemptedAt;
                        user.FailedLoginCount = 0;
                        user.LockoutUntil = null;
                        return LoginResult.Success(user);
                    }

                    writeKind = LoginWriteKind.FailedAttempt;
                    user.FailedLoginCount++;
                    user.LastFailedLoginAt = attemptedAt;
                    if (user.FailedLoginCount >= 10)
                    {
                        user.LockoutUntil = attemptedAt.AddMinutes(15);
                        AddLoginAudit(db, "SuspiciousLoginAttempt", attemptedUsername, attemptedAt);
                        return LoginResult.Locked(user.LockoutUntil);
                    }

                    if (user.FailedLoginCount >= 5)
                    {
                        user.LockoutUntil = attemptedAt.AddMinutes(5);
                        AddLoginAudit(db, "LoginLocked", attemptedUsername, attemptedAt);
                        return LoginResult.Locked(user.LockoutUntil);
                    }

                    AddLoginAudit(db, "LoginFailed", attemptedUsername, attemptedAt);
                    return LoginResult.Invalid(user.FailedLoginCount);
                },
                verifySucceeded: (db, token) => VerifyLoginWriteAsync(
                    db,
                    attemptedUsername,
                    attemptedAt,
                    auditPayload,
                    writeKind,
                    token),
                cancellationToken: cancellationToken);
        }

        private static Task<bool> VerifyLoginWriteAsync(
            AppDbContext db,
            string attemptedUsername,
            DateTime attemptedAt,
            string auditPayload,
            LoginWriteKind writeKind,
            CancellationToken cancellationToken) =>
            writeKind switch
            {
                LoginWriteKind.None => Task.FromResult(true),
                LoginWriteKind.Audit => db.AuditLogs.AnyAsync(log =>
                    log.EntityName == "Authentication" &&
                    log.ActionCode == "LoginFailed" &&
                    log.PerformedAt == attemptedAt &&
                    log.AfterJson == auditPayload,
                    cancellationToken),
                LoginWriteKind.FailedAttempt => db.AppUsers.AnyAsync(user =>
                    user.Username == attemptedUsername &&
                    user.LastFailedLoginAt == attemptedAt,
                    cancellationToken),
                LoginWriteKind.Success => db.AppUsers.AnyAsync(user =>
                    user.Username == attemptedUsername &&
                    user.LastLoginAt == attemptedAt,
                    cancellationToken),
                _ => Task.FromResult(false)
            };

        public Task ChangePasswordAsync(
            int userId,
            string currentPassword,
            string newPassword,
            byte[] expectedRowVersion,
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(newPassword))
            {
                throw new InvalidOperationException("New password is required.");
            }

            if (currentPassword == newPassword)
            {
                throw new InvalidOperationException("Mật khẩu mới không được trùng với mật khẩu cũ.");
            }

            ArgumentNullException.ThrowIfNull(expectedRowVersion);
            var rowVersion = expectedRowVersion.ToArray();
            var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            var changedAt = DateTime.Now;

            return _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("authentication.change-password", operationId),
                async (db, token) =>
                {
                    var user = await db.AppUsers.SingleOrDefaultAsync(item => item.Id == userId, token)
                        ?? throw new InvalidOperationException("User does not exist.");
                    db.Entry(user).Property(item => item.RowVersion).OriginalValue = rowVersion;
                    if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
                    {
                        throw new InvalidOperationException("Mật khẩu hiện tại không chính xác.");
                    }

                    user.PasswordHash = newHash;
                    user.LastPasswordChangedAt = changedAt;
                    user.MustChangePassword = false;
                },
                (db, token) => db.AppUsers.AnyAsync(item =>
                    item.Id == userId && item.PasswordHash == newHash &&
                    !item.MustChangePassword && item.RowVersion != rowVersion,
                    token),
                cancellationToken: cancellationToken);
        }
    }
}
