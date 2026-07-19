using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Data.SqlClient;

namespace QuanLyHangHoa.Services;

internal interface IClientSessionTransport : IAsyncDisposable
{
    Task AcquireAndRegisterAsync(Guid sessionId, string appVersion, CancellationToken cancellationToken);
    Task HeartbeatAsync(Guid sessionId, CancellationToken cancellationToken);
}

public sealed class ClientSessionLease : IAsyncDisposable
{
    private readonly Func<IClientSessionTransport> _transportFactory;
    private readonly string _appVersion;
    private readonly TimeSpan _heartbeatInterval;
    private readonly int _retryCount;
    private readonly TimeSpan _retryDelay;
    private readonly Func<Exception, Task> _leaseLost;
    private readonly Guid _sessionId;
    private readonly CancellationTokenSource _stop = new();
    private IClientSessionTransport _transport;
    private readonly Task _heartbeatLoop;
    private bool _disposed;

    private ClientSessionLease(
        Func<IClientSessionTransport> transportFactory,
        IClientSessionTransport transport,
        Guid sessionId,
        string appVersion,
        TimeSpan heartbeatInterval,
        int retryCount,
        TimeSpan retryDelay,
        Func<Exception, Task> leaseLost)
    {
        _transportFactory = transportFactory;
        _transport = transport;
        _sessionId = sessionId;
        _appVersion = appVersion;
        _heartbeatInterval = heartbeatInterval;
        _retryCount = retryCount;
        _retryDelay = retryDelay;
        _leaseLost = leaseLost;
        _heartbeatLoop = RunHeartbeatLoopAsync();
    }

    public static Task<ClientSessionLease> RegisterAsync(
        string connectionString,
        string appVersion,
        CancellationToken cancellationToken) =>
        RegisterAsync(
            () => new SqlClientSessionTransport(connectionString),
            appVersion,
            ClientSessionPolicy.HeartbeatInterval,
            retryCount: 3,
            retryDelay: TimeSpan.FromSeconds(2),
            ShutdownApplicationAsync,
            cancellationToken);

    internal static async Task<ClientSessionLease> RegisterAsync(
        Func<IClientSessionTransport> transportFactory,
        string appVersion,
        TimeSpan heartbeatInterval,
        int retryCount,
        TimeSpan retryDelay,
        Func<Exception, Task> leaseLost,
        CancellationToken cancellationToken)
    {
        if (retryCount < 1)
            throw new ArgumentOutOfRangeException(nameof(retryCount));
        var transport = transportFactory();
        var sessionId = Guid.NewGuid();
        try
        {
            await transport.AcquireAndRegisterAsync(sessionId, appVersion, cancellationToken);
            return new ClientSessionLease(
                transportFactory, transport, sessionId, appVersion, heartbeatInterval,
                retryCount, retryDelay, leaseLost);
        }
        catch
        {
            await transport.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task RunHeartbeatLoopAsync()
    {
        using var timer = new PeriodicTimer(_heartbeatInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(_stop.Token).ConfigureAwait(false))
            {
                try
                {
                    await _transport.HeartbeatAsync(_sessionId, _stop.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_stop.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception heartbeatError)
                {
                    var recoveryError = await RecoverAsync(heartbeatError).ConfigureAwait(false);
                    if (recoveryError is null)
                        continue;
                    await _leaseLost(recoveryError).ConfigureAwait(false);
                    _stop.Cancel();
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
        }
    }

    private async Task<Exception?> RecoverAsync(Exception initialError)
    {
        Exception lastError = initialError;
        try { await _transport.DisposeAsync().ConfigureAwait(false); } catch { }

        for (var attempt = 0; attempt < _retryCount; attempt++)
        {
            if (_retryDelay > TimeSpan.Zero)
                await Task.Delay(_retryDelay, _stop.Token).ConfigureAwait(false);

            var candidate = _transportFactory();
            try
            {
                await candidate.AcquireAndRegisterAsync(
                    _sessionId, _appVersion, _stop.Token).ConfigureAwait(false);
                _transport = candidate;
                return null;
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
                await candidate.DisposeAsync().ConfigureAwait(false);
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
                try { await candidate.DisposeAsync().ConfigureAwait(false); } catch { }
            }
        }

        return lastError;
    }

    private static async Task ShutdownApplicationAsync(Exception error)
    {
        var application = Application.Current;
        if (application is null)
        {
            Environment.FailFast("WarePro lost its database session lease.", error);
            return;
        }

        await application.Dispatcher.InvokeAsync(() => application.Shutdown(-1)).Task.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        _stop.Cancel();
        try
        {
            await _heartbeatLoop.ConfigureAwait(false);
        }
        finally
        {
            _stop.Dispose();
            await _transport.DisposeAsync().ConfigureAwait(false);
        }
    }
}

internal sealed class SqlClientSessionTransport : IClientSessionTransport
{
    private readonly string _connectionString;
    private SqlConnection? _connection;
    private Guid _sessionId;

    public SqlClientSessionTransport(string connectionString) => _connectionString = connectionString;

    public async Task AcquireAndRegisterAsync(
        Guid sessionId,
        string appVersion,
        CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await AcquireSharedLockAsync(connection, cancellationToken);
            await ExecuteAsync(
                connection,
                ClientSessionService.RegisterSql,
                cancellationToken,
                ("@sessionId", sessionId),
                ("@machineName", Environment.MachineName),
                ("@processId", Environment.ProcessId),
                ("@appVersion", appVersion));
            _connection = connection;
            _sessionId = sessionId;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task HeartbeatAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var connection = _connection ?? throw new InvalidOperationException("Client session is not registered.");
        var affected = await ExecuteAsync(
            connection,
            ClientSessionService.HeartbeatSql,
            cancellationToken,
            ("@sessionId", sessionId));
        if (affected != 1)
            throw new InvalidOperationException("Client session registration was lost.");
    }

    private static async Task AcquireSharedLockAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("""
            DECLARE @result INT;
            EXEC @result = sys.sp_getapplock
                @Resource = @resource,
                @LockMode = 'Shared',
                @LockOwner = 'Session',
                @LockTimeout = 15000;
            SELECT @result;
            """, connection);
        command.Parameters.Add("@resource", SqlDbType.NVarChar, 255).Value =
            SchemaMaintenanceLock.SharedResource(connection.Database);
        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) < 0)
            throw new InvalidOperationException("Database maintenance is in progress.");
    }

    private static async Task<int> ExecuteAsync(
        SqlConnection connection,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var command = new SqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        var connection = _connection;
        _connection = null;
        if (connection is null)
            return;
        try
        {
            if (connection.State == ConnectionState.Open && _sessionId != Guid.Empty)
                await ExecuteAsync(
                    connection,
                    ClientSessionService.ReleaseSql,
                    CancellationToken.None,
                    ("@sessionId", _sessionId));
        }
        catch
        {
            // Closing the connection still releases the session applock.
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}