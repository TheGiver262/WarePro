using QuanLyHangHoa.Data;

namespace QuanLyHangHoa.Tests.Data;

public sealed class DatabaseWriteDiagnosticsTests
{
    [Fact]
    public void Default_executors_share_the_app_owned_diagnostics_observer()
    {
        var first = new DatabaseWriteExecutor(() => throw new InvalidOperationException());
        var second = new DatabaseWriteExecutor(() => throw new InvalidOperationException());

        var field = typeof(DatabaseWriteExecutor).GetField("_diagnostics", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        Assert.Same(field.GetValue(first), field.GetValue(second));
    }

    [Fact]
    public void Custom_diagnostics_owns_a_disposable_listener_subscription()
    {
        var diagnostics = new DatabaseWriteDiagnostics(_ => { });

        Assert.IsAssignableFrom<IDisposable>(diagnostics).Dispose();
    }
}