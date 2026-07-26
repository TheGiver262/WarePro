using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.Tests.Infrastructure;

public sealed class SqlServerAuthenticationSecurityTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [SqlServerConcurrencyFact]
    [Trait("Category", "SqlServerConcurrency")]
    public async Task Authenticate_rejects_sql_injection_payloads_on_sql_server()
    {
        await using var database = SqlServerTestDatabase.FromEnvironment();
        await database.InitializeAsync();

        const string validUsername = "sqlserver-injection-user";
        const string validPassword = "correct-password";
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(validPassword);

        await using (var setupContext = database.CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();

            foreach (var schemaFile in new[]
                     {
                         "v6-common-write-safety.sql",
                         "v7-invoice-void-open-claim.sql",
                         "v8-unique-invoice-stock-links.sql"
                     })
            {
                var sql = await File.ReadAllTextAsync(Path.Combine(
                    RepoRoot,
                    "Database",
                    "Schema",
                    schemaFile));
                await setupContext.Database.ExecuteSqlRawAsync(sql);
            }

            setupContext.AppUsers.Add(new AppUser
            {
                FullName = "SQL Server Injection Test User",
                Username = validUsername,
                PasswordHash = passwordHash,
                RoleCode = "Staff",
                IsActive = true
            });
            await setupContext.SaveChangesAsync();
        }

        var service = new AuthenticationService(database.CreateContext);
        var attempts = new[]
        {
            (Username: "' OR 1=1 --", Password: "anything"),
            (Username: validUsername, Password: "' OR 1=1 --"),
            (Username: "'; DROP TABLE AppUser; --", Password: "anything")
        };

        foreach (var attempt in attempts)
        {
            var result = await service.AuthenticateAsync(
                attempt.Username,
                attempt.Password,
                Guid.NewGuid());

            Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
            Assert.Null(result.User);
        }

        await using var assertContext = database.CreateContext();
        var persistedUser = Assert.Single(await assertContext.AppUsers.ToListAsync());
        Assert.Equal(validUsername, persistedUser.Username);
        Assert.True(BCrypt.Net.BCrypt.Verify(validPassword, persistedUser.PasswordHash));
    }
}
