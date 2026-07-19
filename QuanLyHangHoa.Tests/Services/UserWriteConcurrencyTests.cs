using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;

namespace QuanLyHangHoa.Tests.Services;

public sealed class UserWriteConcurrencyTests
{
    [Fact]
    public async Task User_update_rejects_stale_rowversion()
    {
        using var connection = CreateDatabase();
        AppUser stale;
        using (var db = CreateContext(connection))
            stale = db.AppUsers.AsNoTracking().Single(item => item.Id == 2);
        Overwrite(connection, db => db.AppUsers.Single(item => item.Id == 2).FullName = "Concurrent user");

        var service = new AppUserService(() => CreateContext(connection));
        await Assert.ThrowsAsync<DatabaseWriteConflictException>(() => service.UpdateUserAsync(
            stale.Id,
            new AppUser
            {
                FullName = "Stale user",
                RoleCode = stale.RoleCode,
                IsActive = stale.IsActive,
                PasswordHash = stale.PasswordHash
            },
            stale.RowVersion,
            performedByUserId: 1,
            Guid.NewGuid()));

        using var verify = CreateContext(connection);
        Assert.Equal("Concurrent user", verify.AppUsers.Single(item => item.Id == 2).FullName);
    }

    [Fact]
    public async Task Change_password_rejects_stale_rowversion()
    {
        using var connection = CreateDatabase();
        AppUser stale;
        using (var db = CreateContext(connection))
        {
            var user = db.AppUsers.Single(item => item.Id == 2);
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("old-password");
            db.SaveChanges();
            stale = db.AppUsers.AsNoTracking().Single(item => item.Id == 2);
        }
        Overwrite(connection, db => db.AppUsers.Single(item => item.Id == 2).FullName = "Concurrent password owner");

        var service = new AuthenticationService(() => CreateContext(connection));
        await Assert.ThrowsAsync<DatabaseWriteConflictException>(() => service.ChangePasswordAsync(
            stale.Id,
            "old-password",
            "new-password",
            stale.RowVersion,
            Guid.NewGuid()));

        using var verify = CreateContext(connection);
        var persisted = verify.AppUsers.Single(item => item.Id == 2);
        Assert.True(BCrypt.Net.BCrypt.Verify("old-password", persisted.PasswordHash));
        Assert.False(BCrypt.Net.BCrypt.Verify("new-password", persisted.PasswordHash));
    }

    private static SqliteConnection CreateDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);
        DatabaseHelper.SeedBasicData(db);
        return connection;
    }

    private static void Overwrite(SqliteConnection connection, Action<AppDbContext> change)
    {
        using var db = CreateContext(connection);
        change(db);
        db.SaveChanges();
    }

    private static AppDbContext CreateContext(SqliteConnection connection) =>
        DatabaseHelper.CreateContext(connection);
}
