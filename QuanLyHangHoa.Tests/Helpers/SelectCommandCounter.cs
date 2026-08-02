using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace QuanLyHangHoa.Tests.Helpers;

internal sealed class SelectCommandCounter : DbCommandInterceptor
{
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public void Reset() => Interlocked.Exchange(ref _count, 0);

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        CountSelect(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        CountSelect(command);
        return ValueTask.FromResult(result);
    }

    private void CountSelect(DbCommand command)
    {
        if (command.CommandText.AsSpan().TrimStart().StartsWith(
                "SELECT",
                StringComparison.OrdinalIgnoreCase))
        {
            Interlocked.Increment(ref _count);
        }
    }
}
