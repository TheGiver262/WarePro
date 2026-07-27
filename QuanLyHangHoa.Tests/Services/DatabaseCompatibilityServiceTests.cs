using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.Tests.Services;

public class DatabaseCompatibilityServiceTests
{
    [Theory]
    [InlineData(0, DatabaseCompatibilityStatus.UpgradeRequired)]
    [InlineData(6, DatabaseCompatibilityStatus.UpgradeRequired)]
    [InlineData(7, DatabaseCompatibilityStatus.UpgradeRequired)]
    [InlineData(8, DatabaseCompatibilityStatus.UpgradeRequired)]
    [InlineData(9, DatabaseCompatibilityStatus.Compatible)]
    [InlineData(10, DatabaseCompatibilityStatus.ClientUpdateRequired)]
    public void Evaluate_returns_the_expected_schema_status(
        int databaseSchemaVersion,
        DatabaseCompatibilityStatus expected)
    {
        var result = new DatabaseCompatibilityService().Evaluate(databaseSchemaVersion);

        Assert.Equal(expected, result.Status);
        Assert.Equal(databaseSchemaVersion, result.DatabaseSchemaVersion);
        Assert.Equal(DatabaseCompatibilityService.CurrentSchemaVersion, result.CurrentSchemaVersion);
    }

    [Fact]
    public void Minimum_client_version_blocks_an_older_binary_even_when_schema_matches()
    {
        var service = new DatabaseCompatibilityService();

        var result = service.Evaluate(
            DatabaseCompatibilityService.CurrentSchemaVersion,
            minimumClientVersion: "1.1.0",
            currentClientVersion: "1.0.0");

        Assert.Equal(DatabaseCompatibilityStatus.ClientUpdateRequired, result.Status);
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0")]
    [InlineData("1.0.0", "1.0.1")]
    [InlineData("1.0.0", "1.0.0.0")]
    public void Supported_client_version_keeps_a_matching_schema_compatible(
        string minimumClientVersion,
        string currentClientVersion)
    {
        var result = new DatabaseCompatibilityService().Evaluate(
            DatabaseCompatibilityService.CurrentSchemaVersion,
            minimumClientVersion,
            currentClientVersion);

        Assert.Equal(DatabaseCompatibilityStatus.Compatible, result.Status);
    }

    [Fact]
    public void Binary_only_release_does_not_require_a_database_backup()
    {
        var service = new DatabaseCompatibilityService();

        Assert.False(service.RequiresBackup(
            DatabaseCompatibilityService.CurrentSchemaVersion,
            hasExistingBusinessData: true));
        Assert.True(service.RequiresBackup(
            DatabaseCompatibilityService.CurrentSchemaVersion - 1,
            hasExistingBusinessData: true));
        Assert.False(service.RequiresBackup(
            DatabaseCompatibilityService.CurrentSchemaVersion - 1,
            hasExistingBusinessData: false));
    }
}
