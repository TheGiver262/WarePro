using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QuanLyHangHoa.Configuration;

namespace QuanLyHangHoa.Data;

public sealed record DatabaseWriteLogEntry(
    string OperationName,
    Guid OperationId,
    int Attempt,
    int? SqlErrorNumber,
    TimeSpan? Delay,
    IsolationLevel IsolationLevel,
    TimeSpan Elapsed,
    string Outcome,
    string? EntityKey);

public sealed class DatabaseWriteDiagnostics : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
{
    private readonly AsyncLocal<OperationState?> _current = new();
    private readonly Action<DatabaseWriteLogEntry> _write;
    private readonly IDisposable _allListenersSubscription;
    private readonly List<IDisposable> _listenerSubscriptions = [];
    private readonly object _subscriptionLock = new();
    private int _disposed;

    public static DatabaseWriteDiagnostics Shared { get; } = new();

    public DatabaseWriteDiagnostics(Action<DatabaseWriteLogEntry>? write = null)
    {
        _write = write ?? (entry => Trace.WriteLine(JsonSerializer.Serialize(entry)));
        _allListenersSubscription = DiagnosticListener.AllListeners.Subscribe(this);
    }

    internal IDisposable Begin(DatabaseWriteRequest request, string? entityKey)
    {
        var previous = _current.Value;
        _current.Value = new OperationState(request, SensitiveDataRedactor.Redact(entityKey));
        return new Scope(() => _current.Value = previous);
    }

    internal void SetAttempt(int attempt)
    {
        if (_current.Value is { } state)
        {
            state.Attempt = attempt;
        }
    }

    internal void RecordRetry(Exception exception, TimeSpan delay) =>
        Write("retry", exception, delay);

    internal void RecordOutcome(string outcome, Exception? exception = null) =>
        Write(outcome, exception, null);

    public void OnNext(DiagnosticListener listener)
    {
        if (Volatile.Read(ref _disposed) != 0 || listener.Name != DbLoggerCategory.Name)
        {
            return;
        }

        var subscription = listener.Subscribe(
            this,
            (eventName, _, _) => eventName == CoreEventId.ExecutionStrategyRetrying.Name);
        lock (_subscriptionLock)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                subscription.Dispose();
                return;
            }

            _listenerSubscriptions.Add(subscription);
        }
    }

    public void OnNext(KeyValuePair<string, object?> value)
    {
        if (value.Key == CoreEventId.ExecutionStrategyRetrying.Name &&
            value.Value is ExecutionStrategyEventData eventData)
        {
            RecordRetry(eventData.ExceptionsEncountered.Last(), eventData.Delay);
        }
    }

    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _allListenersSubscription.Dispose();
        lock (_subscriptionLock)
        {
            foreach (var subscription in _listenerSubscriptions)
            {
                subscription.Dispose();
            }

            _listenerSubscriptions.Clear();
        }
    }

    private void Write(string outcome, Exception? exception, TimeSpan? delay)
    {
        if (_current.Value is not { } state)
        {
            return;
        }

        var entry = new DatabaseWriteLogEntry(
            state.Request.OperationName,
            state.Request.OperationId,
            state.Attempt,
            GetSqlErrorNumber(exception),
            delay,
            state.Request.IsolationLevel,
            state.Stopwatch.Elapsed,
            outcome,
            state.EntityKey);

        try
        {
            _write(entry);
        }
        catch
        {
            // logging must never change the result of a database write
        }
    }

    private static int? GetSqlErrorNumber(Exception? exception)
    {
        if (exception is SqlException sqlException)
        {
            return sqlException.Number;
        }

        if (exception is AggregateException aggregateException)
        {
            return aggregateException.InnerExceptions
                .Select(GetSqlErrorNumber)
                .FirstOrDefault(number => number.HasValue);
        }

        return exception?.InnerException is null
            ? null
            : GetSqlErrorNumber(exception.InnerException);
    }

    private sealed class OperationState(DatabaseWriteRequest request, string entityKey)
    {
        public DatabaseWriteRequest Request { get; } = request;

        public string? EntityKey { get; } = string.IsNullOrEmpty(entityKey) ? null : entityKey;

        public Stopwatch Stopwatch { get; } = Stopwatch.StartNew();

        public int Attempt { get; set; }
    }

    private sealed class Scope(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}
