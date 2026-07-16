using System.IO;
using System.Xml.Linq;

namespace QuanLyHangHoa.Tests.Deployment;

public class PublishContractTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void ReadyToRun_profile_is_self_contained_win_x64_without_debug_artifacts()
    {
        var profile = XDocument.Load(Path.Combine(
            RepoRoot,
            "QuanLyHangHoa",
            "Properties",
            "PublishProfiles",
            "ReadyToRun-x64.pubxml"));

        Assert.Equal("win-x64", Property(profile, "RuntimeIdentifier"));
        Assert.Equal("true", Property(profile, "SelfContained"));
        Assert.Equal("true", Property(profile, "PublishReadyToRun"));
        Assert.Equal("false", Property(profile, "PublishSingleFile"));
        Assert.Equal("None", Property(profile, "DebugType"));
        Assert.Equal("false", Property(profile, "DebugSymbols"));
    }

    [Fact]
    public void Project_uses_one_release_version_for_all_Windows_metadata()
    {
        var project = XDocument.Load(Path.Combine(RepoRoot, "QuanLyHangHoa", "QuanLyHangHoa.csproj"));

        Assert.Equal("1.0.0", Property(project, "VersionPrefix"));
        Assert.Equal("$(VersionPrefix)", Property(project, "Version"));
        Assert.Equal("$(VersionPrefix).0", Property(project, "AssemblyVersion"));
        Assert.Equal("$(VersionPrefix).0", Property(project, "FileVersion"));
        Assert.Equal("$(VersionPrefix)", Property(project, "InformationalVersion"));
        Assert.Equal("true", Property(project, "GenerateAssemblyInfo"));
        Assert.Contains(project.Descendants(), element =>
            element.Name.LocalName == "WareProPublisherThumbprint");

        var publisherMetadata = project.Descendants().SingleOrDefault(element =>
            element.Name.LocalName == "AssemblyMetadata"
            && (string?)element.Attribute("Include") == "WareProPublisherThumbprint");
        Assert.NotNull(publisherMetadata);
        Assert.Equal("$(WareProPublisherThumbprint)",
            (string?)publisherMetadata.Attribute("Value"));
    }

    [Fact]
    public void Project_publishes_the_seed_workbook_at_the_runtime_database_path()
    {
        var project = XDocument.Load(Path.Combine(RepoRoot, "QuanLyHangHoa", "QuanLyHangHoa.csproj"));
        var seed = project.Descendants().SingleOrDefault(element =>
            element.Name.LocalName == "Content"
            && (string?)element.Attribute("Include") == @"..\Database\warepro_database_seed.xlsx");

        Assert.NotNull(seed);
        Assert.Equal(@"Database\warepro_database_seed.xlsx", Property(seed, "Link"));
        Assert.Equal("PreserveNewest", Property(seed, "CopyToOutputDirectory"));
        Assert.Equal("PreserveNewest", Property(seed, "CopyToPublishDirectory"));
    }

    private static string? Property(XContainer document, string name) =>
        document.Descendants().SingleOrDefault(element => element.Name.LocalName == name)?.Value;
}
