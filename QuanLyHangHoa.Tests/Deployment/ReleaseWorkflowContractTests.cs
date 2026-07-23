using System.Text.Json;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.Tests.Deployment;

public class ReleaseWorkflowContractTests
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
        var setupHelper = Read("WarePro.SetupHelper", "SetupCommands.cs");
        var policy = document.RootElement;

        Assert.Equal("1.1.0", policy.GetProperty("minimumClientVersion").GetString());
        Assert.Equal(
            DatabaseCompatibilityService.MinimumSupportedSchemaVersion,
            policy.GetProperty("minimumSchemaVersion").GetInt32());
        Assert.Equal(
            DatabaseCompatibilityService.MaximumSupportedSchemaVersion,
            policy.GetProperty("maximumSchemaVersion").GetInt32());
        Assert.Contains("private const int SupportedSchema = 8;", setupHelper, StringComparison.Ordinal);
    }

    [Fact]
    public void Manifest_script_generates_lowercase_sha_and_validates_semver()
    {
        var script = Read("scripts", "release", "New-WareProUpdateManifest.ps1");

        Assert.Contains("ValidatePattern", script, StringComparison.Ordinal);
        Assert.Contains("^[0-9]+\\.[0-9]+\\.[0-9]+$", script, StringComparison.Ordinal);
        Assert.Contains("ToLowerInvariant", script, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash", script, StringComparison.Ordinal);
        Assert.Contains("warepro-update.json", script, StringComparison.Ordinal);
        Assert.Contains("release-policy.json", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_script_publishes_self_contained_signs_and_compiles_pinned_inno()
    {
        var script = Read("scripts", "release", "Build-WareProRelease.ps1");

        Assert.Contains("--self-contained", script, StringComparison.Ordinal);
        Assert.Contains("win-x64", script, StringComparison.Ordinal);
        Assert.Contains("signtool", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("http://timestamp.digicert.com", script, StringComparison.Ordinal);
        Assert.Contains("Inno Setup 6", script, StringComparison.Ordinal);
        Assert.Contains("6.7.3", script, StringComparison.Ordinal);
        Assert.Contains("WAREPRO_SIGNING_CERT_PFX_BASE64", script, StringComparison.Ordinal);
        Assert.Contains("finally", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Remove-Item", script, StringComparison.Ordinal);
        Assert.DoesNotContain("SharedDatabaseSmokeApproved", script, StringComparison.Ordinal);
        Assert.Contains("WAREPRO_EXPECTED_PUBLISHER_THUMBPRINT", script, StringComparison.Ordinal);
        Assert.Contains("$testProject", script, StringComparison.Ordinal);
        Assert.Contains("dotnet test $testProject", script, StringComparison.Ordinal);
        Assert.Contains("-m:1", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Verification_script_checks_signature_hash_size_version_and_secret_absence()
    {
        var script = Read("scripts", "release", "Verify-WareProRelease.ps1");

        Assert.Contains("Get-AuthenticodeSignature", script, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash", script, StringComparison.Ordinal);
        Assert.Contains("installerSize", script, StringComparison.Ordinal);
        Assert.Contains("FileVersion", script, StringComparison.Ordinal);
        Assert.Contains("*.pdb", script, StringComparison.Ordinal);
        Assert.Contains("*.pfx", script, StringComparison.Ordinal);
        Assert.Contains("*.key", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Workflow_is_tag_scoped_least_privilege_and_only_creates_draft()
    {
        var workflow = Read(".github", "workflows", "warepro-release.yml");

        Assert.Contains("v[0-9]+.[0-9]+.[0-9]+", workflow, StringComparison.Ordinal);
        Assert.Contains("contents: read", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("contents: write", workflow, StringComparison.Ordinal);
        Assert.Contains("WAREPRO_SIGNING_CERT_PFX_BASE64", workflow, StringComparison.Ordinal);
        Assert.Contains("WAREPRO_SIGNING_CERT_PASSWORD", workflow, StringComparison.Ordinal);
        Assert.Contains("WAREPRO_RELEASE_TOKEN", workflow, StringComparison.Ordinal);
        Assert.Contains("vars.WAREPRO_PUBLISHER_THUMBPRINT", workflow, StringComparison.Ordinal);
        Assert.Contains("ExpectedPublisherThumbprint", workflow, StringComparison.Ordinal);
        Assert.Contains("--draft", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("--latest", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("gh release edit", workflow, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Workflow_cleans_certificate_even_when_build_fails()
    {
        var workflow = Read(".github", "workflows", "warepro-release.yml");

        Assert.Contains("if: always()", workflow, StringComparison.Ordinal);
        Assert.Contains("WAREPRO_TEMP_PFX", workflow, StringComparison.Ordinal);
        Assert.Contains("Remove-Item", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Workflow_keeps_release_as_draft_until_external_smoke_approval()
    {
        var workflow = Read(".github", "workflows", "warepro-release.yml");

        Assert.Contains("draft", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("smoke", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("publish release", workflow, StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(params string[] segments)
    {
        var path = segments.Aggregate(Root, Path.Combine);
        return File.ReadAllText(path);
    }
}
