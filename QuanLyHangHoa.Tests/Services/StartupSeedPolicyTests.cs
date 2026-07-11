using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.Tests.Services;

public class StartupSeedPolicyTests
{
    [Fact]
    public void ShouldSeed_returns_true_when_seed_file_exists_and_database_has_no_users()
    {
        Assert.True(StartupSeedPolicy.ShouldSeed(seedFileExists: true, hasAnyUsers: false, forceSeed: false));
    }

    [Fact]
    public void ShouldSeed_returns_false_when_database_already_has_users()
    {
        Assert.False(StartupSeedPolicy.ShouldSeed(seedFileExists: true, hasAnyUsers: true, forceSeed: false));
    }

    [Fact]
    public void ShouldSeed_returns_true_when_force_seed_is_enabled()
    {
        Assert.True(StartupSeedPolicy.ShouldSeed(seedFileExists: true, hasAnyUsers: true, forceSeed: true));
    }

    [Fact]
    public void ShouldSeed_returns_false_when_seed_file_is_missing()
    {
        Assert.False(StartupSeedPolicy.ShouldSeed(seedFileExists: false, hasAnyUsers: false, forceSeed: true));
    }

    [Theory]
    [InlineData(2, 2, true, false, true)]
    [InlineData(1, 2, true, false, false)]
    [InlineData(2, 2, false, false, false)]
    [InlineData(2, 2, true, true, false)]
    public void CanSkipInitialization_requires_current_seeded_database(
        int schemaVersion,
        int requiredSchemaVersion,
        bool hasAnyUsers,
        bool forceSeed,
        bool expected)
    {
        Assert.Equal(
            expected,
            StartupSeedPolicy.CanSkipInitialization(
                schemaVersion,
                requiredSchemaVersion,
                hasAnyUsers,
                forceSeed));
    }
}
