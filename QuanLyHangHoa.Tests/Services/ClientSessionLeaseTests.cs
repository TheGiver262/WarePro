using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.Tests.Services;

public sealed class ClientSessionLeaseTests
{
    [Fact]
    public async Task Heartbeat_failure_reconnects_reacquires_lock_and_registers_same_session()
    {
        var first = new FakeTransport(heartbeatFailure: new IOException("network lost"));
        var recovered = new FakeTransport();
        var transports = new Queue<IClientSessionTransport>([first, recovered]);
        var recoveredSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        recovered.OnRegistered = () => recoveredSignal.TrySetResult();

        await using var lease = await ClientSessionLease.RegisterAsync(
            () => transports.Dequeue(), "1.1.0", TimeSpan.FromMilliseconds(1),
            retryCount: 3, retryDelay: TimeSpan.Zero, _ => Task.CompletedTask,
            CancellationToken.None);

        await recoveredSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(first.SessionId, recovered.SessionId);
        Assert.Equal(1, recovered.AcquireAndRegisterCalls);
    }

    [Fact]
    public async Task Exhausted_recovery_invokes_fail_closed_callback()
    {
        var first = new FakeTransport(heartbeatFailure: new IOException("network lost"));
        var transports = new Queue<IClientSessionTransport>([
            first,
            new FakeTransport(registerFailure: new IOException("retry 1")),
            new FakeTransport(registerFailure: new IOException("retry 2")),
            new FakeTransport(registerFailure: new IOException("retry 3"))
        ]);
        var fatal = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var lease = await ClientSessionLease.RegisterAsync(
            () => transports.Dequeue(), "1.1.0", TimeSpan.FromMilliseconds(1),
            retryCount: 3, retryDelay: TimeSpan.Zero,
            ex => { fatal.TrySetResult(ex); return Task.CompletedTask; },
            CancellationToken.None);

        var error = await fatal.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.IsType<IOException>(error);
        Assert.Empty(transports);
    }

    [Fact]
    public async Task Heartbeat_does_not_start_until_initial_registration_completes()
    {
        var registrationGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new FakeTransport(registrationGate: registrationGate.Task);

        var registration = ClientSessionLease.RegisterAsync(
            () => transport, "1.1.0", TimeSpan.FromMilliseconds(10),
            retryCount: 1, retryDelay: TimeSpan.Zero, _ => Task.CompletedTask,
            CancellationToken.None);

        await Task.Delay(50);
        Assert.Equal(0, transport.HeartbeatCalls);
        Assert.False(registration.IsCompleted);

        registrationGate.SetResult();
        await using var lease = await registration.WaitAsync(TimeSpan.FromSeconds(2));
    }
    private sealed class FakeTransport : IClientSessionTransport
    {
        private readonly Exception? _heartbeatFailure;
        private readonly Exception? _registerFailure;
        private readonly Task? _registrationGate;
        public FakeTransport(Exception? heartbeatFailure = null, Exception? registerFailure = null, Task? registrationGate = null)
        {
            _heartbeatFailure = heartbeatFailure;
            _registerFailure = registerFailure;
            _registrationGate = registrationGate;
        }

        public Guid SessionId { get; private set; }
        public int AcquireAndRegisterCalls { get; private set; }
        public int HeartbeatCalls { get; private set; }
        public Action? OnRegistered { get; set; }

        public async Task AcquireAndRegisterAsync(Guid sessionId, string appVersion, CancellationToken cancellationToken)
        {
            AcquireAndRegisterCalls++;
            SessionId = sessionId;
            if (_registerFailure is not null)
                throw _registerFailure;
            if (_registrationGate is not null)
                await _registrationGate.WaitAsync(cancellationToken);
            OnRegistered?.Invoke();
        }

        public Task HeartbeatAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            HeartbeatCalls++;
            return _heartbeatFailure is null ? Task.CompletedTask : Task.FromException(_heartbeatFailure);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}