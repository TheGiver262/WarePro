using Microsoft.Data.Sqlite;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;

namespace QuanLyHangHoa.Tests.Services;

public class AuthenticationAuditAttributionTests
{
    [Fact]
    public void Authenticate_unknown_username_does_not_attribute_audit_to_another_user()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            seedContext.AppUsers.Add(new AppUser
            {
                Id = 9,
                FullName = "Known User",
                Username = "known",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("known-pass"),
                RoleCode = "Staff",
                IsActive = true
            });
            seedContext.SaveChanges();
        }

        var service = new AuthenticationService(() => CreateContext(connection));

        var result = service.Authenticate("missing", "wrong-pass");

        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
        using var assertContext = CreateContext(connection);
        Assert.Empty(assertContext.AuditLogs);
    }

    private static AppDbContext CreateContext(SqliteConnection connection) =>
        DatabaseHelper.CreateContext(connection);
}
