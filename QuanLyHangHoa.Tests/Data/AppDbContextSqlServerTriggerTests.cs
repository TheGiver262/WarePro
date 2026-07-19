using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;

namespace QuanLyHangHoa.Tests.Data;

public sealed class AppDbContextSqlServerTriggerTests
{
    [Fact]
    public void Sql_server_tables_disable_output_clause_for_write_gate_triggers()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=WareProModelTest;Trusted_Connection=True")
            .Options;
        using var context = new AppDbContext(options);

        var mappedTables = context.Model.GetEntityTypes()
            .Where(entity => entity.GetTableName() is not null)
            .ToList();

        Assert.NotEmpty(mappedTables);
        Assert.All(
            mappedTables,
            entity => Assert.Equal(false, entity.FindAnnotation("SqlServer:UseSqlOutputClause")?.Value));
    }
}