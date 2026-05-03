using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using Xunit;
using System;
using System.Linq;

namespace QuanLyHangHoa.Tests.Services;

public class AuthenticationServiceTests
{
    [Fact]
    public void ChangePassword_updates_password_when_current_password_matches()
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

        service.ChangePassword(10, "old-pass", "new-pass");

        using var assertContext = CreateContext(connection);
        var user = assertContext.AppUsers.Single(u => u.Id == 10);
        Assert.True(BCrypt.Net.BCrypt.Verify("new-pass", user.PasswordHash));
    }

    [Fact]
    public void ChangePassword_rejects_wrong_current_password()
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

        var ex = Assert.Throws<InvalidOperationException>(() =>
            service.ChangePassword(11, "wrong-pass", "new-pass"));

        Assert.Equal("Current password is incorrect.", ex.Message);
        
        using var assertContext = CreateContext(connection);
        var user = assertContext.AppUsers.Single(u => u.Id == 11);
        Assert.True(BCrypt.Net.BCrypt.Verify("old-pass", user.PasswordHash));
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }
}
