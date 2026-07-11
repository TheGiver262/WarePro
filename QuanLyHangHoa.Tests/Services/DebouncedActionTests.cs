using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.Tests.Services;

public class DebouncedActionTests
{
    [Fact]
    public async Task Schedule_runs_only_the_latest_action()
    {
        using var debouncer = new DebouncedAction(delayMilliseconds: 20);
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var firstRan = false;

        debouncer.Schedule(() => firstRan = true);
        debouncer.Schedule(() => completion.SetResult());

        await completion.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.False(firstRan);
    }
}
