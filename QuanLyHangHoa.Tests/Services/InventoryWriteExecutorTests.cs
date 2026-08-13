using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;

namespace QuanLyHangHoa.Tests.Services;

public sealed class InventoryWriteExecutorTests
{
    [Fact]
    public async Task SaveDraftAsync_retries_with_a_fresh_graph_after_generated_key_flush()
    {
        var interceptor = new ThrowAfterFirstSaveInterceptor();
        await using var database = await ExecutorTestDatabase.CreateAsync(interceptor);
        var executor = new DatabaseWriteExecutor(
            database.CreateContext,
            new DatabaseWriteDiagnostics(_ => { }),
            _ => new RetryingTestExecutionStrategy(maxAttempts: 2));
        var service = new StockInService(database.CreateContext, executor);
        var document = new StockIn
        {
            DocumentCode = "SI-RETRY-FRESH-GRAPH",
            WarehouseId = 1,
            SupplierId = 1,
            ImportDate = DateTime.UtcNow,
            PurposeCode = "Purchase"
        };

        await service.SaveDraftAsync(
            document,
            [new StockInLine { ProductId = 10, UnitId = 1, Quantity = 1, UnitPrice = 10 }],
            1,
            Guid.NewGuid());

        Assert.True(document.Id > 0);
        Assert.Equal(2, interceptor.ObservedRoots.Count);
        Assert.NotSame(interceptor.ObservedRoots[0], interceptor.ObservedRoots[1]);
        using var verify = database.CreateContext();
        Assert.Single(verify.StockIns.Where(item => item.DocumentCode == document.DocumentCode));
        Assert.Single(verify.StockInLines.Where(item => item.StockInId == document.Id));
    }
    [Fact]
    public async Task SaveDraftAsync_maps_stale_root_rowversion_to_common_conflict()
    {
        using var connection = OpenDatabase();
        var document = new StockIn
        {
            DocumentCode = "SI-STALE-ROOT",
            WarehouseId = 1,
            ImportDate = DateTime.UtcNow,
            PurposeCode = "Purchase",
            Status = DocumentStatus.Draft,
            CreatedBy = 1,
            CreatedAt = DateTime.UtcNow
        };
        Add(connection, db => db.StockIns.Add(document));

        StockIn stale;
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            stale = db.StockIns.AsNoTracking().Single(item => item.Id == document.Id);
        }

        using (var db = DatabaseHelper.CreateContext(connection))
        {
            var current = db.StockIns.Single(item => item.Id == document.Id);
            current.Notes = "concurrent edit";
            db.SaveChanges();
        }

        var service = new StockInService(() => DatabaseHelper.CreateContext(connection));
        var error = await Assert.ThrowsAsync<DatabaseWriteConflictException>(() =>
            service.SaveDraftAsync(stale, new List<StockInLine>(), 1, Guid.NewGuid()));

        Assert.Equal("DB-WRITE-CONFLICT", error.Code);
    }

    [Fact]
    public async Task SaveDraftAsync_preserves_pre_cancelled_token_without_opening_context()
    {
        using var connection = OpenDatabase();
        var contextCount = 0;
        var service = new StockInService(() =>
        {
            contextCount++;
            return DatabaseHelper.CreateContext(connection);
        });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SaveDraftAsync(
            new StockIn(), new List<StockInLine>(), 1, Guid.NewGuid(), cancellation.Token));

        Assert.Equal(0, contextCount);
    }


    [Fact]
    public async Task Stock_in_save_verifies_committed_update_after_ambiguous_commit()
    {
        var interceptor = new ThrowAfterFirstCommitInterceptor();
        await using var database = await ExecutorTestDatabase.CreateAsync(interceptor);
        var document = new StockIn
        {
            DocumentCode = "SI-AMBIGUOUS-UPDATE",
            WarehouseId = 1,
            SupplierId = 1,
            ImportDate = DateTime.UtcNow,
            PurposeCode = "Purchase",
            Status = DocumentStatus.Draft,
            CreatedBy = 1,
            CreatedAt = DateTime.UtcNow,
            Lines =
            [
                new StockInLine { ProductId = 10, UnitId = 1, Quantity = 1, UnitPrice = 10 }
            ]
        };
        using (var db = database.CreateContext())
        {
            db.StockIns.Add(document);
            db.SaveChanges();
        }

        StockIn stale;
        using (var db = database.CreateContext())
        {
            stale = db.StockIns.AsNoTracking().Single(item => item.Id == document.Id);
        }
        stale.Notes = "changed header";
        var changedLines = new List<StockInLine>
        {
            new() { ProductId = 11, UnitId = 1, Quantity = 2, UnitPrice = 20 }
        };

        var service = new StockInService(database.CreateContext);
        await service.SaveDraftAsync(stale, changedLines, 1, Guid.NewGuid());

        using var verify = database.CreateContext();
        var committed = verify.StockIns.AsNoTracking()
            .Include(item => item.Lines)
            .Single(item => item.Id == document.Id);
        Assert.Equal("changed header", committed.Notes);
        Assert.Single(committed.Lines);
        Assert.Equal(11, committed.Lines.Single().ProductId);
    }

    [Fact]
    public async Task Stock_out_save_verifies_committed_create_after_ambiguous_commit()
    {
        var interceptor = new ThrowAfterFirstCommitInterceptor();
        await using var database = await ExecutorTestDatabase.CreateAsync(interceptor);
        var service = new StockOutService(database.CreateContext);
        var document = new StockOut
        {
            CustomerId = 1,
            WarehouseId = 1,
            PurposeCode = "Sale",
            ExportDate = DateTime.UtcNow
        };

        await service.SaveDraftAsync(document, [], 1, Guid.NewGuid());

        using var verify = database.CreateContext();
        var committed = Assert.Single(verify.StockOuts.AsNoTracking());
        Assert.Equal(document.Id, committed.Id);
        Assert.Equal(document.DocumentCode, committed.DocumentCode);
    }

    [Fact]
    public async Task Stock_adjustment_save_verifies_committed_create_after_ambiguous_commit()
    {
        var interceptor = new ThrowAfterFirstCommitInterceptor();
        await using var database = await ExecutorTestDatabase.CreateAsync(interceptor);
        var service = new StockAdjustmentService(database.CreateContext);
        var document = new StockAdjustment
        {
            WarehouseId = 1,
            AdjustmentType = "Increase",
            ReasonCode = "Count"
        };

        await service.SaveDraftAsync(document, [], 1, Guid.NewGuid());

        using var verify = database.CreateContext();
        var committed = Assert.Single(verify.StockAdjustments.AsNoTracking());
        Assert.Equal(document.Id, committed.Id);
        Assert.Equal(document.DocumentCode, committed.DocumentCode);
    }



    [Fact]
    public async Task Stock_count_update_rejects_stale_line_rowversion()
    {
        using var connection = OpenDatabase();
        var session = new StockCountSession
        {
            SessionCode = "COUNT-STALE-LINE",
            WarehouseId = 1,
            Status = "nháp",
            CreatedBy = 1,
            CountDate = DateTime.UtcNow,
            Lines =
            [
                new StockCountLine
                {
                    ProductId = 10,
                    SystemQuantity = 5,
                    CountedQuantity = 5
                }
            ]
        };
        Add(connection, db => db.StockCountSessions.Add(session));

        StockCountLine stale;
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            stale = db.StockCountLines.AsNoTracking().Single(item => item.SessionId == session.Id);
        }
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            var current = db.StockCountLines.Single(item => item.Id == stale.Id);
            current.CountedQuantity = 6;
            db.SaveChanges();
        }
        stale.CountedQuantity = 4;

        var service = new StockCountService(() => DatabaseHelper.CreateContext(connection));
        var error = await Assert.ThrowsAsync<DatabaseWriteConflictException>(() =>
            service.UpdateDraftAsync(session.Id, new[] { stale }, 1, Guid.NewGuid()));

        Assert.Equal("DB-WRITE-CONFLICT", error.Code);
    }

    [Fact]
    public async Task Adjustment_line_only_edit_rejects_stale_root_rowversion()
    {
        using var connection = OpenDatabase();
        var adjustment = new StockAdjustment
        {
            DocumentCode = "ADJ-STALE-ROOT",
            WarehouseId = 1,
            AdjustmentType = "Increase",
            ReasonCode = "Count",
            Status = DocumentStatus.Draft,
            CreatedBy = 1,
            Lines = [new StockAdjustmentLine
            {
                ProductId = 10,
                QuantityDelta = 1,
                BaseQuantityDelta = 1,
                Direction = "In"
            }]
        };
        Add(connection, db => db.StockAdjustments.Add(adjustment));

        StockAdjustment stale;
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            stale = db.StockAdjustments.AsNoTracking().Single(item => item.Id == adjustment.Id);
        }
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            var current = db.StockAdjustments.Single(item => item.Id == adjustment.Id);
            db.Entry(current).Property(item => item.Notes).IsModified = true;
            db.SaveChanges();
        }

        var service = new StockAdjustmentService(() => DatabaseHelper.CreateContext(connection));
        var error = await Assert.ThrowsAsync<DatabaseWriteConflictException>(() => service.SaveDraftAsync(
            stale,
            [new StockAdjustmentLine
            {
                ProductId = 10,
                QuantityDelta = 2,
                BaseQuantityDelta = 2,
                Direction = "In"
            }],
            1,
            Guid.NewGuid()));

        Assert.Equal("DB-WRITE-CONFLICT", error.Code);
    }

    [Fact]
    public async Task DeleteAsync_rejects_stale_root_rowversion()
    {
        using var connection = OpenDatabase();
        var document = NewStockIn("SI-DELETE-CONFLICT");
        Add(connection, db => db.StockIns.Add(document));
        byte[] staleRowVersion;
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            staleRowVersion = db.StockIns.AsNoTracking().Single(item => item.Id == document.Id).RowVersion;
        }
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            var current = db.StockIns.Single(item => item.Id == document.Id);
            current.Notes = "concurrent edit";
            db.SaveChanges();
        }

        var service = new StockInService(() => DatabaseHelper.CreateContext(connection));
        var error = await Assert.ThrowsAsync<DatabaseWriteConflictException>(() => service.DeleteAsync(
            document.Id, staleRowVersion, 1, Guid.NewGuid()));

        Assert.Equal("DB-WRITE-CONFLICT", error.Code);
        using var verify = DatabaseHelper.CreateContext(connection);
        Assert.True(verify.StockIns.Any(item => item.Id == document.Id));
    }

    [Fact]
    public async Task DeleteAsync_rolls_back_document_and_audit_together()
    {
        var interceptor = new ThrowAfterFirstSaveInterceptor();
        await using var database = await ExecutorTestDatabase.CreateAsync(interceptor);
        interceptor.Disarm();
        var document = NewStockIn("SI-DELETE-ROLLBACK");
        using (var db = database.CreateContext())
        {
            db.StockIns.Add(document);
            db.SaveChanges();
        }
        interceptor.Arm();
        byte[] rowVersion;
        using (var db = database.CreateContext())
        {
            rowVersion = db.StockIns.AsNoTracking().Single(item => item.Id == document.Id).RowVersion;
        }
        var service = new StockInService(database.CreateContext);

        await Assert.ThrowsAsync<TestTransientException>(() => service.DeleteAsync(
            document.Id, rowVersion, 1, Guid.NewGuid()));

        using var verify = database.CreateContext();
        Assert.True(verify.StockIns.Any(item => item.Id == document.Id));
        Assert.False(verify.AuditLogs.Any(item =>
            item.EntityName == "StockIn" && item.EntityId == document.Id && item.ActionCode == "DELETE"));
    }

    [Fact]
    public async Task DeleteAsync_preserves_pre_cancelled_token_without_opening_context()
    {
        using var connection = OpenDatabase();
        var contextCount = 0;
        var service = new StockTransferService(() =>
        {
            contextCount++;
            return DatabaseHelper.CreateContext(connection);
        });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.DeleteAsync(
            1, [1], 1, Guid.NewGuid(), cancellation.Token));

        Assert.Equal(0, contextCount);
    }

    [Fact]
    public async Task Adjustment_existing_update_requires_rowversion_before_opening_context()
    {
        var contextCount = 0;
        var service = new StockAdjustmentService(() =>
        {
            contextCount++;
            throw new InvalidOperationException("context must not open");
        });
        var adjustment = new StockAdjustment
        {
            Id = 42,
            DocumentCode = "ADJ-NO-TOKEN",
            WarehouseId = 1,
            AdjustmentType = "Increase",
            ReasonCode = "Count",
            Status = DocumentStatus.Draft
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveDraftAsync(
            adjustment, [], 1, Guid.NewGuid()));

        Assert.Equal(0, contextCount);
    }

    [Fact]
    public async Task Stock_count_existing_line_requires_rowversion_before_opening_context()
    {
        var contextCount = 0;
        var service = new StockCountService(() =>
        {
            contextCount++;
            throw new InvalidOperationException("context must not open");
        });
        var line = new StockCountLine { Id = 7, CountedQuantity = 2 };

        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateDraftAsync(
            5, [line], 1, Guid.NewGuid()));

        Assert.Equal(0, contextCount);
    }
    private sealed class ExecutorTestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        private ExecutorTestDatabase(SqliteConnection connection, DbContextOptions<AppDbContext> options)
        {
            _connection = connection;
            _options = options;
        }

        public static async Task<ExecutorTestDatabase> CreateAsync(params IInterceptor[] interceptors)
        {
            var connection = new SqliteConnection(
                $"Data Source=inventory-write-{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
            await connection.OpenAsync();
            var setupOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            await using (var setup = new AppDbContext(setupOptions))
            {
                DatabaseHelper.SeedBasicData(setup);
            }

            var builder = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection);
            if (interceptors.Length > 0)
            {
                builder.AddInterceptors(interceptors);
            }

            return new ExecutorTestDatabase(connection, builder.Options);
        }

        public AppDbContext CreateContext()
        {
            var context = new AppDbContext(_options);
            context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");
            return context;
        }

        public ValueTask DisposeAsync() => _connection.DisposeAsync();
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

    private sealed class ThrowAfterFirstSaveInterceptor : SaveChangesInterceptor
    {
        private int _shouldThrow = 1;
        private readonly HashSet<Guid> _observedContexts = [];

        public List<StockIn> ObservedRoots { get; } = [];

        public void Arm() => Interlocked.Exchange(ref _shouldThrow, 1);

        public void Disarm() => Interlocked.Exchange(ref _shouldThrow, 0);

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            Observe(eventData.Context);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Observe(eventData.Context);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _shouldThrow, 0) == 1)
            {
                throw new TestTransientException();
            }

            return ValueTask.FromResult(result);
        }

        private void Observe(DbContext? context)
        {
            if (context is null || !_observedContexts.Add(context.ContextId.InstanceId))
            {
                return;
            }

            var root = context.ChangeTracker.Entries<StockIn>()
                .Select(entry => entry.Entity)
                .FirstOrDefault();
            if (root is not null)
            {
                ObservedRoots.Add(root);
            }
        }
    }

    private sealed class RetryingTestExecutionStrategy(int maxAttempts) : IExecutionStrategy
    {
        public bool RetriesOnFailure => maxAttempts > 1;

        public TResult Execute<TState, TResult>(
            TState state,
            Func<DbContext, TState, TResult> operation,
            Func<DbContext, TState, ExecutionResult<TResult>>? verifySucceeded) =>
            throw new NotSupportedException();

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
                catch (TestTransientException) when (attempt < maxAttempts)
                {
                }
            }
        }
    }

    private sealed class TestTransientException : Exception;

    private static StockIn NewStockIn(string code) => new()
    {
        DocumentCode = code,
        WarehouseId = 1,
        SupplierId = 1,
        ImportDate = DateTime.UtcNow,
        PurposeCode = "Purchase",
        Status = DocumentStatus.Draft,
        CreatedBy = 1,
        CreatedAt = DateTime.UtcNow
    };

    private static void Add(SqliteConnection connection, Action<AppDbContext> add)
    {
        using var db = DatabaseHelper.CreateContext(connection);
        add(db);
        db.SaveChanges();
    }

    private static SqliteConnection OpenDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = DatabaseHelper.CreateContext(connection);
        DatabaseHelper.SeedBasicData(db);
        return connection;
    }
}
