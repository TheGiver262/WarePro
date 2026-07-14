using System.Text.Json;
using Microsoft.Data.Sqlite;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;

namespace QuanLyHangHoa.Tests.Services;

public class AuthenticationAuditAttributionTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("known")]
    public void Failed_login_is_system_owned_and_records_attempted_username(string attemptedUsername)
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

        var result = service.Authenticate(attemptedUsername, "wrong-pass");

        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
        using var assertContext = CreateContext(connection);
        var audit = Assert.Single(assertContext.AuditLogs);
        Assert.Null(audit.PerformedBy);
        Assert.Equal("LoginFailed", audit.ActionCode);
        using var payload = JsonDocument.Parse(audit.AfterJson!);
        Assert.Equal(attemptedUsername, payload.RootElement.GetProperty("attemptedUsername").GetString());
    }

    private static AppDbContext CreateContext(SqliteConnection connection) =>
        DatabaseHelper.CreateContext(connection);
}