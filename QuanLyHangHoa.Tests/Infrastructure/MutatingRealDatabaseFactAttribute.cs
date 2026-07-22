using Xunit;

namespace QuanLyHangHoa.Tests.Infrastructure;

public sealed class MutatingRealDatabaseFactAttribute : FactAttribute
{
    public const string OptInEnvironmentVariable = "WAREPRO_RUN_MUTATING_REAL_DATABASE_TESTS";

    public MutatingRealDatabaseFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable(OptInEnvironmentVariable),
                "1",
                StringComparison.Ordinal))
        {
            Skip = $"Set {OptInEnvironmentVariable}=1 only against an approved disposable database.";
        }
    }
}