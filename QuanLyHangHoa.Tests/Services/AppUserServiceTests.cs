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
    public async Task AddUser_rejects_non_administrator_actor_without_writes()
    {
        using var connection = CreateDatabase(User(20, ManagerRole));
        var service = CreateService(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddUserAsync(User(30, ManagerRole), performedByUserId: 20, Guid.NewGuid()));

        using var db = CreateContext(connection);
        Assert.DoesNotContain(db.AppUsers, user => user.Id == 30);
        Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public async Task UpdateUser_rejects_inactive_administrator_actor_without_writes()
    {
        using var connection = CreateDatabase(
            User(10, ManagerRole),
            User(11, AdministratorRole, isActive: false));
        var service = CreateService(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateUserAsync(
                10, User(10, AdministratorRole), RowVersion(connection, 10),
                performedByUserId: 11, Guid.NewGuid()));

        using var db = CreateContext(connection);
        Assert.Equal(ManagerRole, db.AppUsers.Single(user => user.Id == 10).RoleCode);
        Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public async Task ToggleUserStatus_rejects_non_administrator_actor_without_writes()
    {
        using var connection = CreateDatabase(
            User(10, ManagerRole),
            User(20, ManagerRole));
        var service = CreateService(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ToggleUserStatusAsync(10, RowVersion(connection, 10), 20, Guid.NewGuid()));

        using var db = CreateContext(connection);
        Assert.True(db.AppUsers.Single(user => user.Id == 10).IsActive);
        Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public async Task DeleteUser_rejects_missing_actor_without_writes()
    {
        using var connection = CreateDatabase(User(10, ManagerRole));
        var service = CreateService(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteUserAsync(10, RowVersion(connection, 10), 99, Guid.NewGuid()));

        using var db = CreateContext(connection);
        Assert.Contains(db.AppUsers, user => user.Id == 10);
        Assert.Empty(db.AuditLogs);
    }

    [Theory]
    [InlineData("AddUserAsync(", "if (await db.AppUsers.AnyAsync")]
    [InlineData("UpdateUserAsync(", "var existing = await db.AppUsers")]
    [InlineData("ToggleUserStatusAsync(", "var user = await db.AppUsers")]
    [InlineData("DeleteUserAsync(", "var user = await db.AppUsers")]
    public void Mutation_uses_executor_with_serializable_isolation_before_actor_and_target_queries(
        string methodMarker,
        string targetMarker)
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot,
            "QuanLyHangHoa",
            "Services",
            "AppUserService.cs"));
        var method = ExtractMethod(source, methodMarker);

        var executorIndex = method.IndexOf("_writeExecutor.ExecuteAsync", StringComparison.Ordinal);
        var isolationIndex = method.IndexOf("IsolationLevel.Serializable", StringComparison.Ordinal);
        var actorIndex = method.IndexOf("AuthorizationService.RequireFreshActor", StringComparison.Ordinal);
        var targetIndex = method.IndexOf(targetMarker, StringComparison.Ordinal);

        Assert.True(executorIndex >= 0, $"{methodMarker} must use the common write executor.");
        Assert.True(isolationIndex > executorIndex, $"{methodMarker} must request Serializable isolation.");
        Assert.True(actorIndex > isolationIndex, $"{methodMarker} must reload the actor inside the executor callback.");
        Assert.True(targetIndex > actorIndex, $"{methodMarker} must query the target after actor revalidation.");
    }

    [Fact]
    public async Task ToggleUserStatus_rejects_self_deactivation()
    {
        using var connection = CreateDatabase(
            User(10, AdministratorRole),
            User(11, AdministratorRole));
        var service = CreateService(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ToggleUserStatusAsync(10, RowVersion(connection, 10), 10, Guid.NewGuid()));

        using var db = CreateContext(connection);
        Assert.True(db.AppUsers.Single(user => user.Id == 10).IsActive);
        Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public async Task UpdateUser_rejects_self_demotion()
    {
        using var connection = CreateDatabase(
            User(10, AdministratorRole),
            User(11, AdministratorRole));
        var service = CreateService(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateUserAsync(
                10, User(10, ManagerRole), RowVersion(connection, 10),
                performedByUserId: 10, Guid.NewGuid()));

        using var db = CreateContext(connection);
        Assert.Equal(AdministratorRole, db.AppUsers.Single(user => user.Id == 10).RoleCode);
        Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public async Task UpdateUser_rejects_demotion_of_last_active_administrator()
    {
        using var connection = CreateDatabase(
            User(10, AdministratorRole),
            User(20, ManagerRole));
        var service = CreateService(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateUserAsync(
                10, User(10, ManagerRole), RowVersion(connection, 10),
                performedByUserId: 20, Guid.NewGuid()));

        using var db = CreateContext(connection);
        Assert.Equal(AdministratorRole, db.AppUsers.Single(user => user.Id == 10).RoleCode);
    }

    [Fact]
    public async Task ToggleUserStatus_rejects_deactivation_of_last_active_administrator()
    {
        using var connection = CreateDatabase(
            User(10, AdministratorRole),
            User(20, ManagerRole));
        var service = CreateService(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ToggleUserStatusAsync(10, RowVersion(connection, 10), 20, Guid.NewGuid()));

        using var db = CreateContext(connection);
        Assert.True(db.AppUsers.Single(user => user.Id == 10).IsActive);
    }

    [Fact]
    public async Task DeleteUser_rejects_deletion_of_last_active_administrator()
    {
        using var connection = CreateDatabase(
            User(10, AdministratorRole),
            User(20, ManagerRole));
        var service = CreateService(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteUserAsync(10, RowVersion(connection, 10), 20, Guid.NewGuid()));

        using var db = CreateContext(connection);
        Assert.True(db.AppUsers.Single(user => user.Id == 10).IsActive);
    }

    [Fact]
    public async Task DeleteUser_deactivates_referenced_user()
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

        await service.DeleteUserAsync(20, RowVersion(connection, 20), 10, Guid.NewGuid());

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

    private static byte[] RowVersion(SqliteConnection connection, int userId)
    {
        using var db = CreateContext(connection);
        return db.AppUsers.AsNoTracking().Single(user => user.Id == userId).RowVersion;
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
