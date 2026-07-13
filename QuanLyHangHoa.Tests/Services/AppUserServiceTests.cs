using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;
using System.IO;

namespace QuanLyHangHoa.Tests.Services;

public class AppUserServiceTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    private const string AdministratorRole = "Quản trị viên";
    private const string ManagerRole = "Quản lý";

    [Fact]
    public void AddUser_rejects_non_administrator_actor_without_writes()
    {
        using var connection = CreateDatabase(User(20, ManagerRole));
        var service = CreateService(connection);

        Assert.Throws<InvalidOperationException>(() =>
            service.AddUser(User(30, ManagerRole), performedByUserId: 20));

        using var db = CreateContext(connection);
        Assert.DoesNotContain(db.AppUsers, user => user.Id == 30);
        Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public void UpdateUser_rejects_inactive_administrator_actor_without_writes()
    {
        using var connection = CreateDatabase(
            User(10, ManagerRole),
            User(11, AdministratorRole, isActive: false));
        var service = CreateService(connection);

        Assert.Throws<InvalidOperationException>(() =>
            service.UpdateUser(10, User(10, AdministratorRole), performedByUserId: 11));

        using var db = CreateContext(connection);
        Assert.Equal(ManagerRole, db.AppUsers.Single(user => user.Id == 10).RoleCode);
        Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public void ToggleUserStatus_rejects_non_administrator_actor_without_writes()
    {
        using var connection = CreateDatabase(
            User(10, ManagerRole),
            User(20, ManagerRole));
        var service = CreateService(connection);

        Assert.Throws<InvalidOperationException>(() => service.ToggleUserStatus(10, 20));

        using var db = CreateContext(connection);
        Assert.True(db.AppUsers.Single(user => user.Id == 10).IsActive);
        Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public void DeleteUser_rejects_missing_actor_without_writes()
    {
        using var connection = CreateDatabase(User(10, ManagerRole));
        var service = CreateService(connection);

        Assert.Throws<InvalidOperationException>(() => service.DeleteUser(10, 99));

        using var db = CreateContext(connection);
        Assert.Contains(db.AppUsers, user => user.Id == 10);
        Assert.Empty(db.AuditLogs);
    }

    [Theory]
    [InlineData("AddUser(", "db.AppUsers.Any(u => u.Username")]
    [InlineData("UpdateUser(", "var existing = db.AppUsers")]
    [InlineData("ToggleUserStatus(", "var user = db.AppUsers")]
    [InlineData("DeleteUser(", "var user = db.AppUsers")]
    public void Mutation_starts_serializable_transaction_before_actor_and_target_queries(
        string methodMarker,
        string targetMarker)
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot,
            "QuanLyHangHoa",
            "Services",
            "AppUserService.cs"));
        var method = ExtractMethod(source, methodMarker);

        var transactionIndex = method.IndexOf(
            "BeginTransaction(IsolationLevel.Serializable)",
            StringComparison.Ordinal);
        var actorIndex = method.IndexOf("var actor = db.AppUsers", StringComparison.Ordinal);
        var targetIndex = method.IndexOf(targetMarker, StringComparison.Ordinal);

        Assert.True(transactionIndex >= 0, $"{methodMarker} must request Serializable isolation.");
        Assert.True(actorIndex > transactionIndex, $"{methodMarker} must reload the actor after starting its transaction.");
        Assert.True(targetIndex > actorIndex, $"{methodMarker} must load/check the target after actor revalidation.");
    }

    [Fact]
    public void ToggleUserStatus_rejects_self_deactivation()
    {
        using var connection = CreateDatabase(
            User(10, AdministratorRole),
            User(11, AdministratorRole));
        var service = CreateService(connection);

        Assert.Throws<InvalidOperationException>(() => service.ToggleUserStatus(10, 10));

        using var db = CreateContext(connection);
        Assert.True(db.AppUsers.Single(user => user.Id == 10).IsActive);
        Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public void UpdateUser_rejects_self_demotion()
    {
        using var connection = CreateDatabase(
            User(10, AdministratorRole),
            User(11, AdministratorRole));
        var service = CreateService(connection);

        Assert.Throws<InvalidOperationException>(() =>
            service.UpdateUser(10, User(10, ManagerRole), performedByUserId: 10));

        using var db = CreateContext(connection);
        Assert.Equal(AdministratorRole, db.AppUsers.Single(user => user.Id == 10).RoleCode);
        Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public void UpdateUser_rejects_demotion_of_last_active_administrator()
    {
        using var connection = CreateDatabase(
            User(10, AdministratorRole),
            User(20, ManagerRole));
        var service = CreateService(connection);

        Assert.Throws<InvalidOperationException>(() =>
            service.UpdateUser(10, User(10, ManagerRole), performedByUserId: 20));

        using var db = CreateContext(connection);
        Assert.Equal(AdministratorRole, db.AppUsers.Single(user => user.Id == 10).RoleCode);
    }

    [Fact]
    public void ToggleUserStatus_rejects_deactivation_of_last_active_administrator()
    {
        using var connection = CreateDatabase(
            User(10, AdministratorRole),
            User(20, ManagerRole));
        var service = CreateService(connection);

        Assert.Throws<InvalidOperationException>(() => service.ToggleUserStatus(10, 20));

        using var db = CreateContext(connection);
        Assert.True(db.AppUsers.Single(user => user.Id == 10).IsActive);
    }

    [Fact]
    public void DeleteUser_rejects_deletion_of_last_active_administrator()
    {
        using var connection = CreateDatabase(
            User(10, AdministratorRole),
            User(20, ManagerRole));
        var service = CreateService(connection);

        Assert.Throws<InvalidOperationException>(() => service.DeleteUser(10, 20));

        using var db = CreateContext(connection);
        Assert.True(db.AppUsers.Single(user => user.Id == 10).IsActive);
    }

    [Fact]
    public void DeleteUser_deactivates_referenced_user()
    {
        using var connection = CreateDatabase(
            User(10, AdministratorRole),
            User(20, ManagerRole));
        using (var db = CreateContext(connection))
        {
            db.AuditLogs.Add(new AuditLog
            {
                EntityName = "Product",
                EntityId = 1,
                ActionCode = "UPDATE",
                PerformedBy = 20,
                PerformedAt = DateTime.UtcNow
            });
            db.SaveChanges();
        }
        var service = CreateService(connection);

        service.DeleteUser(20, 10);

        using var assertContext = CreateContext(connection);
        Assert.False(assertContext.AppUsers.Single(user => user.Id == 20).IsActive);
        Assert.Contains(assertContext.AuditLogs, log =>
            log.EntityName == "AppUser" && log.EntityId == 20 && log.ActionCode == "DEACTIVATE");
    }

    private static AppUser User(int id, string role, bool isActive = true) => new()
    {
        Id = id,
        Username = $"user-{id}",
        PasswordHash = "hash",
        FullName = $"User {id}",
        RoleCode = role,
        IsActive = isActive
    };

    private static SqliteConnection CreateDatabase(params AppUser[] users)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);
        db.Database.EnsureCreated();
        db.AppUsers.AddRange(users);
        db.SaveChanges();
        return connection;
    }

    private static AppUserService CreateService(SqliteConnection connection) =>
        new(() => CreateContext(connection));

    private static AppDbContext CreateContext(SqliteConnection connection) =>
        DatabaseHelper.CreateContext(connection);

    private static string ExtractMethod(string source, string methodMarker)
    {
        var start = source.IndexOf(methodMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing method marker: {methodMarker}");
        var end = source.IndexOf("\n        public ", start + methodMarker.Length, StringComparison.Ordinal);
        if (end < 0)
        {
            end = source.IndexOf("\n        private ", start + methodMarker.Length, StringComparison.Ordinal);
        }

        return source[start..(end < 0 ? source.Length : end)];
    }
}
