using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.Data;

public sealed class DatabaseWriteExecutor
{
    private readonly Func<AppDbContext> _contextFactory;
    private readonly DatabaseWriteDiagnostics _diagnostics;
    private readonly Func<AppDbContext, IExecutionStrategy>? _strategyFactory;

    public DatabaseWriteExecutor(
        Func<AppDbContext> contextFactory,
        DatabaseWriteDiagnostics? diagnostics = null)
        : this(contextFactory, diagnostics ?? DatabaseWriteDiagnostics.Shared, null)
    {
    }

    internal DatabaseWriteExecutor(
        Func<AppDbContext> contextFactory,
        DatabaseWriteDiagnostics diagnostics,
        Func<AppDbContext, IExecutionStrategy>? strategyFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _strategyFactory = strategyFactory;
    }

    public Task ExecuteAsync(
        DatabaseWriteRequest request,
        Func<AppDbContext, CancellationToken, Task> mutation,
        Func<AppDbContext, CancellationToken, Task<bool>>? verifySucceeded = null,
        string? entityKey = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync<object?>(
            request,
            async (db, token) =>
            {
                await mutation(db, token);
                return null;
            },
            verifySucceeded,
            entityKey,
            cancellationToken);

    public async Task<TResult> ExecuteAsync<TResult>(
        DatabaseWriteRequest request,
        Func<AppDbContext, CancellationToken, Task<TResult>> mutation,
        Func<AppDbContext, CancellationToken, Task<bool>>? verifySucceeded = null,
        string? entityKey = null,
        CancellationToken cancellationToken = default)
    {
        Validate(request, mutation);
        cancellationToken.ThrowIfCancellationRequested();

        using var diagnosticsScope = _diagnostics.Begin(request, entityKey);
        await using var strategyContext = _contextFactory();
        var strategy = _strategyFactory?.Invoke(strategyContext) ??
            strategyContext.Database.CreateExecutionStrategy();
        var attempt = 0;

        try
        {
            return await strategy.ExecuteAsync(
                request,
                async (_, state, token) =>
                {
                    attempt++;
                    _diagnostics.SetAttempt(attempt);
                    return await ExecuteAttemptAsync(
                        state,
                        mutation,
                        verifySucceeded,
                        token);
                },
                verifySucceeded: null,
                cancellationToken);
        }
        catch (DatabaseWriteConflictException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _diagnostics.RecordOutcome("cancelled");
            throw;
        }
        catch (Exception ex) when (attempt >= 3)
        {
            _diagnostics.RecordOutcome("retry-exhausted", ex);
            throw new DatabaseWriteRetryExhaustedException(request.OperationId, ex);
        }
        catch (Exception ex)
        {
            _diagnostics.RecordOutcome("failed", ex);
            throw;
        }
    }

    private async Task<TResult> ExecuteAttemptAsync<TResult>(
        DatabaseWriteRequest request,
        Func<AppDbContext, CancellationToken, Task<TResult>> mutation,
        Func<AppDbContext, CancellationToken, Task<bool>>? verifySucceeded,
        CancellationToken cancellationToken)
    {
        await using var context = _contextFactory();
        await using var transaction = await context.Database.BeginTransactionAsync(
            request.IsolationLevel,
            cancellationToken);
        await AcquireWriteGateAsync(context, transaction, cancellationToken);

        TResult result;
        try
        {
            result = await mutation(context, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _diagnostics.RecordOutcome("conflict", ex);
            throw new DatabaseWriteConflictException(request.OperationId, ex);
        }

        try
        {
            await transaction.CommitAsync(cancellationToken);
            _diagnostics.RecordOutcome("committed");
            return result;
        }
        catch (Exception) when (verifySucceeded is not null)
        {
            await using var verificationContext = _contextFactory();
            if (await verifySucceeded(verificationContext, cancellationToken))
            {
                _diagnostics.RecordOutcome("commit-verified");
                return result;
            }

            throw;
        }
    }

    private static async Task AcquireWriteGateAsync(
        AppDbContext context,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (!context.Database.IsSqlServer())
            return;

        var connection = (SqlConnection)context.Database.GetDbConnection();
        var sqlTransaction = (SqlTransaction)transaction.GetDbTransaction();
        await using var command = new SqlCommand("""
            EXEC sys.sp_set_session_context @key = N'WareProClientSchema', @value = @clientSchema;
            EXEC sys.sp_set_session_context @key = N'WareProClientVersion', @value = @clientVersion;

            DECLARE @LockResult INT;
            EXEC @LockResult = sys.sp_getapplock
                @Resource = @resource,
                @LockMode = 'Shared',
                @LockOwner = 'Transaction',
                @LockTimeout = 15000;
            IF @LockResult < 0
                THROW 51000, 'Database maintenance is in progress.', 1;

            DECLARE @SchemaVersion INT = 0;
            DECLARE @MinimumClientVersion NVARCHAR(32) = N'1.0.0';
            IF OBJECT_ID(N'dbo.__WareProSchemaVersion', N'U') IS NOT NULL
            BEGIN
                EXEC sys.sp_executesql
                    N'SELECT @value = ISNULL(MAX([Version]), 0) FROM dbo.__WareProSchemaVersion;',
                    N'@value INT OUTPUT',
                    @value = @SchemaVersion OUTPUT;
                IF COL_LENGTH(N'dbo.__WareProSchemaVersion', N'MinimumClientVersion') IS NOT NULL
                    EXEC sys.sp_executesql
                        N'SELECT @value = COALESCE(MAX(NULLIF([MinimumClientVersion], N'''')), N''1.0.0'') FROM dbo.__WareProSchemaVersion;',
                        N'@value NVARCHAR(32) OUTPUT',
                        @value = @MinimumClientVersion OUTPUT;
            END;
            SELECT @SchemaVersion, @MinimumClientVersion;
            """, connection, sqlTransaction);
        var appVersion = typeof(DatabaseWriteExecutor).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        command.Parameters.Add("@clientSchema", System.Data.SqlDbType.Int).Value =
            DatabaseCompatibilityService.CurrentSchemaVersion;
        command.Parameters.Add("@clientVersion", System.Data.SqlDbType.NVarChar, 32).Value = appVersion;
        command.Parameters.Add("@resource", System.Data.SqlDbType.NVarChar, 255).Value =
            SchemaMaintenanceLock.SharedResource(connection.Database);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Database compatibility metadata is unavailable.");

        var schemaVersion = reader.GetInt32(0);
        var minimumClientVersion = reader.GetString(1);

        var compatibility = new DatabaseCompatibilityService().Evaluate(
            schemaVersion,
            minimumClientVersion,
            appVersion);
        if (compatibility.Status != DatabaseCompatibilityStatus.Compatible)
            throw new DatabaseCompatibilityException(schemaVersion, minimumClientVersion, appVersion);
    }
    private static void Validate<TResult>(
        DatabaseWriteRequest request,
        Func<AppDbContext, CancellationToken, Task<TResult>> mutation)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(mutation);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationName);

        if (request.OperationId == Guid.Empty)
        {
            throw new ArgumentException("OperationId cannot be empty.", nameof(request));
        }

        if (!Enum.IsDefined(request.IsolationLevel))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "IsolationLevel is invalid.");
        }
    }
}
