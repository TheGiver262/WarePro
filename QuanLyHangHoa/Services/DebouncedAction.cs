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

    // lần gọi mới hủy và dispose timer cũ; chỉ action cuối cùng sau khoảng chờ được chạy
    public void Schedule(Action action)
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = new CancellationTokenSource();
        _ = ExecuteAsync(action, _cancellation.Token);
    }

    // OperationCanceledException là kết quả bình thường khi người dùng tiếp tục gõ, không phải lỗi cần hiển thị
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

    // owner phải dispose khi ViewModel đóng để action đang chờ không chạy vào object đã hết vòng đời
    public void Dispose()
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
    }
}
