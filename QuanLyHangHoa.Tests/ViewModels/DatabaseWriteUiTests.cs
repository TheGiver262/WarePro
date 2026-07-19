using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Tests.ViewModels;

public sealed class DatabaseWriteUiTests
{
    [Fact]
    public async Task Conflict_reloads_before_showing_safe_message()
    {
        var events = new List<string>();
        var state = new TestState();

        var completed = await DatabaseWriteUi.ExecuteAsync(
            _ => Task.FromException(new DatabaseWriteConflictException(
                Guid.NewGuid(), new Exception("UPDATE StockBalances SET ..."))),
            () => state.IsWriting,
            value => state.IsWriting = value,
            value => state.Statuses.Add(value),
            () => events.Add("reload"),
            message => events.Add($"message:{message}"),
            CancellationToken.None);

        Assert.False(completed);
        Assert.Equal(new[]
        {
            "reload",
            $"message:{DatabaseWriteUi.ConflictMessage}"
        }, events);
        Assert.DoesNotContain("UPDATE", string.Join(" ", events), StringComparison.OrdinalIgnoreCase);
        Assert.False(state.IsWriting);
    }

    [Fact]
    public async Task Retry_exhaustion_reloads_before_allowing_a_new_attempt()
    {
        var events = new List<string>();
        var state = new TestState();

        var completed = await DatabaseWriteUi.ExecuteAsync(
            _ => Task.FromException(new DatabaseWriteRetryExhaustedException(
                Guid.NewGuid(), new Exception("SqlException 1205"))),
            () => state.IsWriting,
            value => state.IsWriting = value,
            value => state.Statuses.Add(value),
            () => events.Add("reload"),
            message => events.Add($"message:{message}"),
            CancellationToken.None);

        Assert.False(completed);
        Assert.Equal(new[]
        {
            "reload",
            $"message:{DatabaseWriteUi.RetryExhaustedMessage}"
        }, events);
        Assert.DoesNotContain("1205", string.Join(" ", events), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cancellation_is_silent()
    {
        var messages = new List<string>();
        var state = new TestState();

        var completed = await DatabaseWriteUi.ExecuteAsync(
            _ => Task.FromCanceled(new CancellationToken(canceled: true)),
            () => state.IsWriting,
            value => state.IsWriting = value,
            value => state.Statuses.Add(value),
            () => { },
            messages.Add,
            CancellationToken.None);

        Assert.False(completed);
        Assert.Empty(messages);
        Assert.False(state.IsWriting);
    }

    [Fact]
    public async Task Domain_error_keeps_safe_business_message()
    {
        var messages = new List<string>();
        var state = new TestState();

        await DatabaseWriteUi.ExecuteAsync(
            _ => Task.FromException(new InventoryDomainException("Sản phẩm không đủ tồn kho.")),
            () => state.IsWriting,
            value => state.IsWriting = value,
            value => state.Statuses.Add(value),
            () => { },
            messages.Add,
            CancellationToken.None);

        Assert.Equal(new[] { "Sản phẩm không đủ tồn kho." }, messages);
    }
    [Fact]
    public async Task Technical_error_hides_provider_details()
    {
        var messages = new List<string>();
        var state = new TestState();

        await DatabaseWriteUi.ExecuteAsync(
            _ => Task.FromException(new Exception("Server=secret; SQL syntax near password")),
            () => state.IsWriting,
            value => state.IsWriting = value,
            value => state.Statuses.Add(value),
            () => { },
            messages.Add,
            CancellationToken.None);

        Assert.Equal(new[] { DatabaseWriteUi.TechnicalErrorMessage }, messages);
        Assert.DoesNotContain("secret", messages[0], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SQL", messages[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Long_write_shows_retry_status_but_short_write_does_not()
    {
        var longState = new TestState();
        var shortState = new TestState();

        await DatabaseWriteUi.ExecuteAsync(
            async token => await Task.Delay(20, token),
            () => longState.IsWriting,
            value => longState.IsWriting = value,
            value => longState.Statuses.Add(value),
            () => { },
            _ => { },
            CancellationToken.None,
            TimeSpan.Zero);
        await DatabaseWriteUi.ExecuteAsync(
            _ => Task.CompletedTask,
            () => shortState.IsWriting,
            value => shortState.IsWriting = value,
            value => shortState.Statuses.Add(value),
            () => { },
            _ => { },
            CancellationToken.None,
            TimeSpan.FromSeconds(1));

        Assert.Contains(DatabaseWriteUi.RetryStatus, longState.Statuses);
        Assert.DoesNotContain(DatabaseWriteUi.RetryStatus, shortState.Statuses);
    }

    [Fact]
    public async Task Reentry_is_ignored_while_a_write_is_running()
    {
        var invoked = false;
        var state = new TestState { IsWriting = true };

        var completed = await DatabaseWriteUi.ExecuteAsync(
            _ =>
            {
                invoked = true;
                return Task.CompletedTask;
            },
            () => state.IsWriting,
            value => state.IsWriting = value,
            value => state.Statuses.Add(value),
            () => { },
            _ => { },
            CancellationToken.None);

        Assert.False(completed);
        Assert.False(invoked);
        Assert.True(state.IsWriting);
    }

    private sealed class TestState
    {
        public bool IsWriting { get; set; }

        public List<string> Statuses { get; } = new();
    }
}
