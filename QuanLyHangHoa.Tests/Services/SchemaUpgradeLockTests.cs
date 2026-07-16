using System.Reflection;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.Tests.Services;

public class SchemaUpgradeLockTests
{
    [Fact]
    public void Lock_uses_a_session_scoped_SQL_application_lock()
    {
        var sqlField = typeof(SchemaUpgradeLock).GetField(
            "AcquireSql",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(sqlField);
        var sql = (string)sqlField.GetRawConstantValue()!;
        Assert.Contains("sp_getapplock", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WarePro.SchemaUpgrade", sql, StringComparison.Ordinal);
        Assert.Contains("Session", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@lockTimeout", sql, StringComparison.Ordinal);
    }
}
