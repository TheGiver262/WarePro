using System;
using System.IO;

namespace QuanLyHangHoa.Tests.Services;

public class RealDatabaseIsolationContractTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void Real_database_tests_use_the_configured_connection_string()
    {
        var testRoot = Path.Combine(RepoRoot, "QuanLyHangHoa.Tests");
        var forbiddenConnection = "Database=" + "ProductManagementDb";
        var realDatabaseSources = Directory
            .EnumerateFiles(testRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Select(path => new { Path = path, Source = File.ReadAllText(path) })
            .Where(file => file.Source.Contains("[Trait(\"Category\", \"RealDatabase\")]", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(realDatabaseSources);
        foreach (var file in realDatabaseSources)
        {
            Assert.DoesNotContain(forbiddenConnection, file.Source, StringComparison.Ordinal);
            if (file.Source.Contains("SaveChanges(", StringComparison.Ordinal) ||
                file.Source.Contains("SaveChangesAsync(", StringComparison.Ordinal))
            {
                Assert.Contains("[MutatingRealDatabaseFact]", file.Source, StringComparison.Ordinal);
            }
            if (file.Source.Contains("UseSqlServer(", StringComparison.Ordinal))
            {
                Assert.Contains("AppDbContext.GetConnectionString()", file.Source, StringComparison.Ordinal);
            }
        }
    }
}