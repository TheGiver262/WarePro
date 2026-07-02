using System;

namespace QuanLyHangHoa.Services;

public static class StartupSeedPolicy
{
    public static bool ShouldSeed(bool seedFileExists, bool hasAnyUsers, bool forceSeed)
    {
        if (!seedFileExists)
        {
            return false;
        }

        return forceSeed || !hasAnyUsers;
    }

    public static bool IsForceSeedEnabled()
    {
        var value = Environment.GetEnvironmentVariable("WAREPRO_FORCE_SEED");
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }
}