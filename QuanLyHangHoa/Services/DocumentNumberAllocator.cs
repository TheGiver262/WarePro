using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using QuanLyHangHoa.Data;
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyHangHoa.Services;

public static class DocumentNumberAllocator
{
    public static async Task<string> AllocateAsync(
        AppDbContext db,
        string documentType,
        string prefix,
        DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        documentType = documentType?.Trim() ?? string.Empty;
        prefix = prefix?.Trim().ToUpperInvariant() ?? string.Empty;
        if (documentType.Length is 0 or > 32)
            throw new ArgumentException("Document type must contain 1-32 characters.", nameof(documentType));
        if (prefix.Length is 0 or > 12)
            throw new ArgumentException("Document prefix must contain 1-12 characters.", nameof(prefix));

        var value = string.Equals(
            db.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.Sqlite",
            StringComparison.Ordinal)
            ? await AllocateSqliteAsync(db, documentType, businessDate, cancellationToken)
            : await AllocateSqlServerAsync(db, documentType, businessDate, cancellationToken);
        return $"{prefix}-{businessDate:yyyyMMdd}-{value:D6}";
    }

    private static async Task<long> AllocateSqliteAsync(
        AppDbContext db,
        string documentType,
        DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO DocumentNumberCounter (DocumentType, BusinessDate, LastValue, RowVersion)
            VALUES (@DocumentType, @BusinessDate, 1, randomblob(16))
            ON CONFLICT(DocumentType, BusinessDate) DO UPDATE
            SET LastValue = LastValue + 1,
                RowVersion = randomblob(16)
            RETURNING LastValue;
            """;
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        var typeParameter = command.CreateParameter();
        typeParameter.ParameterName = "@DocumentType";
        typeParameter.Value = documentType;
        command.Parameters.Add(typeParameter);
        var dateParameter = command.CreateParameter();
        dateParameter.ParameterName = "@BusinessDate";
        dateParameter.Value = businessDate.ToString("yyyy-MM-dd");
        command.Parameters.Add(dateParameter);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<long> AllocateSqlServerAsync(
        AppDbContext db,
        string documentType,
        DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "EXEC sys.sp_set_session_context @key = N'WareProClientSchema', @value = @ClientSchema; EXEC dbo.AllocateDocumentNumber @DocumentType, @BusinessDate;";
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        var schemaParameter = command.CreateParameter();
        schemaParameter.ParameterName = "@ClientSchema";
        schemaParameter.DbType = DbType.Int32;
        schemaParameter.Value = DatabaseCompatibilityService.CurrentSchemaVersion;
        command.Parameters.Add(schemaParameter);
        var typeParameter = command.CreateParameter();
        typeParameter.ParameterName = "@DocumentType";
        typeParameter.DbType = DbType.String;
        typeParameter.Size = 32;
        typeParameter.Value = documentType;
        command.Parameters.Add(typeParameter);
        var dateParameter = command.CreateParameter();
        dateParameter.ParameterName = "@BusinessDate";
        dateParameter.DbType = DbType.Date;
        dateParameter.Value = businessDate.ToDateTime(TimeOnly.MinValue);
        command.Parameters.Add(dateParameter);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }
}
