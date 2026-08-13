using System.Data;

namespace WarePro.SqlServer.Tests;

public sealed class DocumentNumberAllocatorTests
{
    [SqlServerFact]
    [Trait("Category", "RealDatabase")]
    public async Task Stored_procedure_allocates_unique_contiguous_numbers_concurrently()
    {
        await using var database = await SqlServerTestDatabase.CreateMigratedAsync();

        var values = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var connection = await database.OpenConnectionAsync(clientSchema: 11);
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC dbo.AllocateDocumentNumber @DocumentType, @BusinessDate;";
            command.Parameters.Add("@DocumentType", SqlDbType.NVarChar, 32).Value = "StockIn";
            command.Parameters.Add("@BusinessDate", SqlDbType.Date).Value = new DateTime(2026, 8, 13);
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }));

        Assert.Equal(Enumerable.Range(1, 20).Select(value => (long)value), values.Order());
    }
}
