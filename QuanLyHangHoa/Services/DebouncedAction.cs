using System;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyHangHoa.Services;

public sealed class DebouncedAction : IDisposable
{
    private readonly TimeSpan _delay;
    private CancellationTokenSource? _cancellation;

    public DebouncedAction(int delayMilliseconds = 300)
    {
        _delay = TimeSpan.FromMilliseconds(delayMilliseconds);
    }

    public void Schedule(Action action)
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = new CancellationTokenSource();
        _ = ExecuteAsync(action, _cancellation.Token);
    }

    private async Task ExecuteAsync(Action action, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_delay, cancellationToken);
            action();
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void Dispose()
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
    }
}
