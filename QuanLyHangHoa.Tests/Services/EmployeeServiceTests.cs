using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.Tests.Services;

public class EmployeeServiceTests
{
    [Fact]
    public void AddEmployee_with_actor_creates_audit_log()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
        }

        var service = new EmployeeService(
            () => CreateContext(connection),
            () => new DateTime(2026, 4, 29, 10, 0, 0));

        service.AddEmployee(new Employee
        {
            FullName = "New User",
            Username = "newuser",
            Role = "Staff"
        }, performedByUserId: 1);

        using var assertContext = CreateContext(connection);
        var employee = Assert.Single(assertContext.Employees.Where(employee => employee.Username == "newuser"));
        Assert.Equal("newuser", employee.PasswordHash);
        var audit = Assert.Single(assertContext.AuditLogs);
        Assert.Equal("CreateEmployee", audit.ActionCode);
        Assert.Equal(1, audit.PerformedByUserId);
        Assert.Equal(new DateTime(2026, 4, 29, 10, 0, 0), audit.PerformedAt);
    }

    [Fact]
    public void DeleteEmployee_with_actor_creates_audit_log()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            seedContext.Employees.Add(new Employee
            {
                Id = 12,
                FullName = "Delete User",
                Username = "deleteuser",
                PasswordHash = "deleteuser",
                Role = "Staff"
            });
            seedContext.SaveChanges();
        }

        var service = new EmployeeService(
            () => CreateContext(connection),
            () => new DateTime(2026, 4, 29, 11, 0, 0));

        service.DeleteEmployee(12, performedByUserId: 1);

        using var assertContext = CreateContext(connection);
        Assert.Empty(assertContext.Employees.Where(employee => employee.Id == 12));
        var audit = Assert.Single(assertContext.AuditLogs);
        Assert.Equal("DeleteEmployee", audit.ActionCode);
        Assert.Equal(1, audit.PerformedByUserId);
        Assert.Equal(new DateTime(2026, 4, 29, 11, 0, 0), audit.PerformedAt);
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }
}
