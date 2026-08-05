using System.Text.Json;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.Tests.Deployment;

public class ReleasePolicyContractTests
{
    private static readonly string Root = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void Update_manifest_schema_requires_integrity_and_compatibility_fields()
    {
        using var document = JsonDocument.Parse(Read("installer", "update-manifest.schema.json"));
        var required = document.RootElement.GetProperty("required")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToHashSet(StringComparer.Ordinal);

        foreach (var name in new[]
                 {
                     "schemaVersion", "version", "minimumClientVersion",
                     "minimumSchemaVersion", "maximumSchemaVersion",
                     "installerAssetName", "installerSize", "sha256",
                     "mandatory", "backupRequired", "publishedAtUtc", "releaseNotes"
                 })
        {
            Assert.Contains(name, required);
        }
    }

    [Fact]
    public void Release_policy_matches_the_database_compatibility_contract()
    {
        using var document = JsonDocument.Parse(Read("installer", "release-policy.json"));
        var installer = Read("installer", "WarePro.iss");
        var setupHelper = Read("WarePro.SetupHelper", "SetupCommands.cs");
        var policy = document.RootElement;

        Assert.Equal("1.1.0", policy.GetProperty("minimumClientVersion").GetString());
        Assert.Equal(
            DatabaseCompatibilityService.MinimumSupportedSchemaVersion,
            policy.GetProperty("minimumSchemaVersion").GetInt32());
        Assert.Equal(
            DatabaseCompatibilityService.MaximumSupportedSchemaVersion,
            policy.GetProperty("maximumSchemaVersion").GetInt32());
        Assert.Contains("private const int SupportedSchema = 10;", setupHelper, StringComparison.Ordinal);
        Assert.Contains("#define MySchemaRelease 10", installer, StringComparison.Ordinal);
    }

    private static string Read(params string[] segments)
    {
        var path = segments.Aggregate(Root, Path.Combine);
        return File.ReadAllText(path);
    }
}
