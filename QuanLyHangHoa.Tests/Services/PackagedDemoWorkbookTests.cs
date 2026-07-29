using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services.DataImport;

namespace QuanLyHangHoa.Tests.Services;

public sealed class PackagedDemoWorkbookTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public async Task SeedAsync_imports_the_packaged_demo_workbook_into_a_new_catalog()
    {
        var workbookPath = Path.Combine(
            RepoRoot, "Database", "warepro_database_seed.xlsx");
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var foreignKeyCommand = connection.CreateCommand())
        {
            foreignKeyCommand.CommandText = "PRAGMA foreign_keys = ON;";
            foreignKeyCommand.ExecuteNonQuery();
        }

        using (var setup = CreateContext(connection))
        {
            setup.Database.EnsureCreated();
            setup.AppUsers.Add(new AppUser
            {
                Id = 1,
                Username = "admin",
                PasswordHash = "test-hash",
                FullName = "Administrator",
                RoleCode = "Admin",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
            await setup.SaveChangesAsync();
        }

        var seeder = new DatabaseSeeder(
            () => CreateContext(connection),
            workbookPath);

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        using var verification = CreateContext(connection);
        Assert.NotEmpty(verification.Products);
        Assert.NotEmpty(verification.StockIns);
        Assert.NotEmpty(verification.StockOuts);
        Assert.All(verification.StockOuts, stockOut => Assert.Equal(1, stockOut.CreatedBy));
        Assert.NotEmpty(verification.ProductSerials);
        Assert.NotEmpty(verification.WarrantyCoverages);
        Assert.NotEmpty(verification.WarrantyClaims);
        Assert.Equal(220, verification.WarrantyCoverages.Count());
        Assert.Equal(50, verification.WarrantyClaims.Count());
        Assert.All(verification.WarrantyClaims, claim =>
        {
            Assert.Equal(1, claim.ProcessedBy);
            Assert.True(claim.ApprovedBy is null or 1);
            Assert.Contains(
                verification.WarrantyCoverages,
                coverage => coverage.Id == claim.WarrantyCoverageId
                    && coverage.ProductSerialId == claim.ProductSerialId);
        });
    }

    private static AppDbContext CreateContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options);
}
