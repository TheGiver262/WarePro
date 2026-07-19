using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;
using Xunit;
using System;
using System.Linq;

namespace QuanLyHangHoa.Tests.Services;

public class AuthenticationServiceTests
{
    [Fact]
    public async Task ChangePassword_updates_password_when_current_password_matches()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        
        var oldPassHash = BCrypt.Net.BCrypt.HashPassword("old-pass");
        
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            seedContext.AppUsers.Add(new AppUser
            {
                Id = 10,
                FullName = "Test User",
                Username = "tester",
                PasswordHash = oldPassHash,
                RoleCode = "Staff",
                IsActive = true
            });
            seedContext.SaveChanges();
        }

        var service = new AuthenticationService(() => CreateContext(connection));

        await service.ChangePasswordAsync(10, "old-pass", "new-pass", RowVersion(connection, 10), Guid.NewGuid());

        using var assertContext = CreateContext(connection);
        var user = assertContext.AppUsers.Single(u => u.Id == 10);
        Assert.True(BCrypt.Net.BCrypt.Verify("new-pass", user.PasswordHash));
    }

    [Fact]
    public async Task ChangePassword_rejects_wrong_current_password()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        
        var oldPassHash = BCrypt.Net.BCrypt.HashPassword("old-pass");
        
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            seedContext.AppUsers.Add(new AppUser
            {
                Id = 11,
                FullName = "Test User",
                Username = "tester",
                PasswordHash = oldPassHash,
                RoleCode = "Staff",
                IsActive = true
            });
            seedContext.SaveChanges();
        }

        var service = new AuthenticationService(() => CreateContext(connection));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ChangePasswordAsync(
                11,
                "wrong-pass",
                "new-pass",
                RowVersion(connection, 11),
                Guid.NewGuid()));

        Assert.Equal("Mật khẩu hiện tại không chính xác.", ex.Message);
        
        using var assertContext = CreateContext(connection);
        var user = assertContext.AppUsers.Single(u => u.Id == 11);
        Assert.True(BCrypt.Net.BCrypt.Verify("old-pass", user.PasswordHash));
    }

    [Fact]
    public void Login_write_defines_natural_commit_verification()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));
        var source = File.ReadAllText(Path.Combine(
            repoRoot, "QuanLyHangHoa", "Services", "AuthenticationService.cs"));
        var methodStart = source.IndexOf("AuthenticateAsync(", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("ChangePasswordAsync(", methodStart, StringComparison.Ordinal);
        var method = source[methodStart..methodEnd];

        Assert.Contains("verifySucceeded: (db, token) => VerifyLoginWriteAsync(", method);
    }

    [Fact]
    public void Login_audit_uses_the_commit_verification_timestamp()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));
        var source = File.ReadAllText(Path.Combine(
            repoRoot, "QuanLyHangHoa", "Services", "AuthenticationService.cs"));

        Assert.Contains(
            "AddLoginAudit(db, \"LoginFailed\", attemptedUsername, attemptedAt)",
            source);
        Assert.Contains("PerformedAt = performedAt", source);
    }

    private static byte[] RowVersion(SqliteConnection connection, int userId)
    {
        using var db = CreateContext(connection);
        return db.AppUsers.AsNoTracking().Single(user => user.Id == userId).RowVersion;
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        return DatabaseHelper.CreateContext(connection);
    }
}
