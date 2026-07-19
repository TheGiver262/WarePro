using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Tests.Data;

public sealed class DatabaseWriteExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_retries_with_a_fresh_context_and_redacted_diagnostics()
    {
        await using var database = await TestDatabase.CreateAsync();
        var entries = new List<DatabaseWriteLogEntry>();
        using var diagnostics = new DatabaseWriteDiagnostics(entries.Add);
        var executor = CreateExecutor(database, diagnostics);
        var attemptContextIds = new List<Guid>();

        await executor.ExecuteAsync(
            new DatabaseWriteRequest("category.create", Guid.NewGuid()),
            async (db, token) =>
            {
                attemptContextIds.Add(db.ContextId.InstanceId);
                if (attemptContextIds.Count == 1)
                {
                    throw new TestTransientException();
                }

                db.Categories.Add(NewCategory("CAT-RETRY"));
                await Task.CompletedTask;
            },
            entityKey: "User ID=admin;Password=p@ss;token=abc123");

        Assert.Equal(2, attemptContextIds.Count);
        Assert.Equal(2, attemptContextIds.Distinct().Count());
        Assert.Contains(entries, entry => entry.Outcome == "retry" && entry.Attempt == 1);
        Assert.Contains(entries, entry => entry.Outcome == "committed" && entry.Attempt == 2);

        var serialized = JsonSerializer.Serialize(entries);
        Assert.DoesNotContain("admin", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("p@ss", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", serialized, StringComparison.Ordinal);

        await using var assertionContext = database.CreateAssertionContext();
        Assert.True(await assertionContext.Categories.AnyAsync(x => x.CategoryCode == "CAT-RETRY"));
    }

    [Fact]
    public async Task ExecuteAsync_maps_concurrency_conflict_without_retrying()
    {
        await using var database = await TestDatabase.CreateAsync();
        using var diagnostics = new DatabaseWriteDiagnostics(_ => { });
        var executor = CreateExecutor(database, diagnostics);
        var attempts = 0;

        var error = await Assert.ThrowsAsync<DatabaseWriteConflictException>(() =>
            executor.ExecuteAsync(
                new DatabaseWriteRequest("category.update", Guid.NewGuid()),
                (_, _) =>
                {
                    attempts++;
                    return Task.FromException(new DbUpdateConcurrencyException("stale row"));
                }));

        Assert.Equal("DB-WRITE-CONFLICT", error.Code);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_preserves_cancellation_without_creating_a_context()
    {
        await using var database = await TestDatabase.CreateAsync();
        using var diagnostics = new DatabaseWriteDiagnostics(_ => { });
        var executor = CreateExecutor(database, diagnostics);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            executor.ExecuteAsync(
                new DatabaseWriteRequest("category.create", Guid.NewGuid()),
                (_, _) => Task.CompletedTask,
                cancellationToken: cancellation.Token));

        Assert.Empty(database.CreatedContextIds);
    }

    [Fact]
    public async Task ExecuteAsync_maps_retry_exhaustion_after_exactly_three_attempts()
    {
        await using var database = await TestDatabase.CreateAsync();
        var entries = new List<DatabaseWriteLogEntry>();
        using var diagnostics = new DatabaseWriteDiagnostics(entries.Add);
        var executor = CreateExecutor(database, diagnostics);
        var attempts = 0;
        var operationId = Guid.NewGuid();

        var error = await Assert.ThrowsAsync<DatabaseWriteRetryExhaustedException>(() =>
            executor.ExecuteAsync(
                new DatabaseWriteRequest("category.create", operationId),
                (_, _) =>
                {
                    attempts++;
                    return Task.FromException(new TestTransientException());
                }));

        Assert.Equal("DB-WRITE-RETRY-EXHAUSTED", error.Code);
        Assert.Equal(operationId, error.OperationId);
        Assert.Equal(3, attempts);
        Assert.Equal(2, entries.Count(entry => entry.Outcome == "retry"));
        Assert.Contains(entries, entry => entry.Outcome == "retry-exhausted" && entry.Attempt == 3);
    }

    [Fact]
    public async Task ExecuteAsync_returns_success_when_uncertain_commit_is_verified()
    {
        var interceptor = new ThrowAfterFirstCommitInterceptor();
        await using var database = await TestDatabase.CreateAsync(interceptor);
        var entries = new List<DatabaseWriteLogEntry>();
        using var diagnostics = new DatabaseWriteDiagnostics(entries.Add);
        var executor = CreateExecutor(database, diagnostics);
        var mutationCalls = 0;

        var result = await executor.ExecuteAsync(
            new DatabaseWriteRequest("category.create", Guid.NewGuid()),
            async (db, token) =>
            {
                mutationCalls++;
                db.Categories.Add(NewCategory("CAT-VERIFY"));
                await Task.Yield();
                return 42;
            },
            (db, token) => db.Categories.AnyAsync(
                category => category.CategoryCode == "CAT-VERIFY",
                token));

        Assert.Equal(42, result);
        Assert.Equal(1, mutationCalls);
        Assert.Contains(entries, entry => entry.Outcome == "commit-verified");

        await using var assertionContext = database.CreateAssertionContext();
        Assert.Equal(1, await assertionContext.Categories.CountAsync(
            category => category.CategoryCode == "CAT-VERIFY"));
    }

    [Fact]
    public async Task ExecuteAsync_uses_requested_isolation_and_first_success_has_no_retry_warning()
    {
        await using var database = await TestDatabase.CreateAsync();
        var entries = new List<DatabaseWriteLogEntry>();
        using var diagnostics = new DatabaseWriteDiagnostics(entries.Add);
        var executor = CreateExecutor(database, diagnostics);
        IsolationLevel? observedIsolation = null;

        await executor.ExecuteAsync(
            new DatabaseWriteRequest(
                "category.create",
                Guid.NewGuid(),
                IsolationLevel.Serializable),
            (db, _) =>
            {
                observedIsolation = db.Database.CurrentTransaction?
                    .GetDbTransaction().IsolationLevel;
                db.Categories.Add(NewCategory("CAT-FIRST"));
                return Task.CompletedTask;
            });

        Assert.Equal(IsolationLevel.Serializable, observedIsolation);
        Assert.DoesNotContain(entries, entry => entry.Outcome == "retry");
        Assert.Single(entries, entry => entry.Outcome == "committed" && entry.Attempt == 1);
    }

    private static DatabaseWriteExecutor CreateExecutor(
        TestDatabase database,
        DatabaseWriteDiagnostics diagnostics) =>
        new(
            database.CreateContext,
            diagnostics,
            _ => new RetryingTestExecutionStrategy(diagnostics, maxAttempts: 3));

    private static Category NewCategory(string code) => new()
    {
        CategoryCode = code,
        DisplayName = code,
        IsActive = true
    };

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        private TestDatabase(
            SqliteConnection connection,
            DbContextOptions<AppDbContext> options)
        {
            _connection = connection;
            _options = options;
        }

        public List<Guid> CreatedContextIds { get; } = [];

        public static async Task<TestDatabase> CreateAsync(params IInterceptor[] interceptors)
        {
            var connection = new SqliteConnection(
                $"Data Source=write-executor-{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
            await connection.OpenAsync();

            var builder = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection);

            await using (var setup = new AppDbContext(builder.Options))
            {
                await setup.Database.EnsureCreatedAsync();
            }

            if (interceptors.Length > 0)
            {
                builder.AddInterceptors(interceptors);
            }

            var options = builder.Options;
            return new TestDatabase(connection, options);
        }

        public AppDbContext CreateContext()
        {
            var context = new AppDbContext(_options);
            CreatedContextIds.Add(context.ContextId.InstanceId);
            return context;
        }

        public AppDbContext CreateAssertionContext() => new(_options);

        public ValueTask DisposeAsync() => _connection.DisposeAsync();
    }

    private sealed class RetryingTestExecutionStrategy(
        DatabaseWriteDiagnostics diagnostics,
        int maxAttempts) : IExecutionStrategy
    {
        public bool RetriesOnFailure => maxAttempts > 1;

        public TResult Execute<TState, TResult>(
            TState state,
            Func<DbContext, TState, TResult> operation,
            Func<DbContext, TState, ExecutionResult<TResult>>? verifySucceeded)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return operation(null!, state);
                }
                catch (TestTransientException ex) when (attempt < maxAttempts)
                {
                    diagnostics.RecordRetry(ex, TimeSpan.FromMilliseconds(attempt * 10));
                }
            }
        }

        public async Task<TResult> ExecuteAsync<TState, TResult>(
            TState state,
            Func<DbContext, TState, CancellationToken, Task<TResult>> operation,
            Func<DbContext, TState, CancellationToken, Task<ExecutionResult<TResult>>>? verifySucceeded,
            CancellationToken cancellationToken = default)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return await operation(null!, state, cancellationToken);
                }
                catch (TestTransientException ex) when (attempt < maxAttempts)
                {
                    diagnostics.RecordRetry(ex, TimeSpan.FromMilliseconds(attempt * 10));
                }
            }
        }
    }

    private sealed class ThrowAfterFirstCommitInterceptor : DbTransactionInterceptor
    {
        private int _shouldThrow = 1;

        public override Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _shouldThrow, 0) == 1)
            {
                throw new TestTransientException();
            }

            return Task.CompletedTask;
        }
    }

    private sealed class TestTransientException : Exception;
}
