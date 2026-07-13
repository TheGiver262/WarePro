using System.Reflection;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.Tests.Services;

public class DatabaseInitializerSchemaTests
{
    [Fact]
    public void Current_schema_repairs_warehouse_posting_columns()
    {
        var type = typeof(DatabaseInitializer);
        var versionField = type.GetField("CurrentSchemaVersion", BindingFlags.NonPublic | BindingFlags.Static);
        var sqlField = type.GetField("SchemaVersion4Sql", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(versionField);
        Assert.NotNull(sqlField);
        var version = (int)versionField.GetRawConstantValue()!;
        var sql = (string)sqlField.GetRawConstantValue()!;

        Assert.Equal(5, version);
        foreach (var table in new[] { "StockIn", "StockOut", "StockAdjustment", "StockTransfer" })
        {
            Assert.Contains($"COL_LENGTH('{table}', 'ApprovedAt')", sql);
            Assert.Contains($"COL_LENGTH('{table}', 'PostedAt')", sql);
        }
    }
}
