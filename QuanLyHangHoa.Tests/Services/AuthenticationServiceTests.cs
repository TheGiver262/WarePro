using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.Tests.Services;

public class AuthenticationServiceTests
{
    [Fact]
    public void ChangePassword_updates_password_when_current_password_matches()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            seedContext.Employees.Add(new Employee
            {
                Id = 10,
                FullName = "Test User",
                Username = "tester",
                PasswordHash = "old-pass",
                Role = "Staff"
            });
            seedContext.SaveChanges();
        }

        var service = new AuthenticationService(() => CreateContext(connection));

        service.ChangePassword("tester", "old-pass", "new-pass");

        using var assertContext = CreateContext(connection);
        Assert.Equal("new-pass", assertContext.Employees.Single(employee => employee.Username == "tester").PasswordHash);
    }

    [Fact]
    public void ChangePassword_rejects_wrong_current_password()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            seedContext.Employees.Add(new Employee
            {
                Id = 11,
                FullName = "Test User",
                Username = "tester",
                PasswordHash = "old-pass",
                Role = "Staff"
            });
            seedContext.SaveChanges();
        }

        var service = new AuthenticationService(() => CreateContext(connection));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            service.ChangePassword("tester", "wrong-pass", "new-pass"));

        Assert.Equal("Current password is incorrect.", ex.Message);
        using var assertContext = CreateContext(connection);
        Assert.Equal("old-pass", assertContext.Employees.Single(employee => employee.Username == "tester").PasswordHash);
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }
}
