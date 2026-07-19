using System;
using System.Threading;
using System.Threading.Tasks;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;

namespace QuanLyHangHoa.ViewModels;

public static class DatabaseWriteUi
{
    public const string ConflictMessage =
        "Dữ liệu đã thay đổi trên máy khác. Hãy tải lại rồi thao tác lại.";
    public const string RetryExhaustedMessage =
        "Kết nối đang bận. Dữ liệu sẽ được tải lại trước khi thử lại.";
    public const string RetryStatus = "Đang kết nối lại…";
    public const string TechnicalErrorMessage =
        "Không thể lưu dữ liệu. Vui lòng thử lại hoặc liên hệ quản trị viên.";

    private static readonly TimeSpan DefaultRetryStatusDelay = TimeSpan.FromMilliseconds(600);

    public static async Task<bool> ExecuteAsync(
        Func<CancellationToken, Task> write,
        Func<bool> isWriting,
        Action<bool> setIsWriting,
        Action<string> setWriteStatus,
        Action reload,
        Action<string> showError,
        CancellationToken cancellationToken,
        TimeSpan? retryStatusDelay = null)
    {
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(isWriting);
        ArgumentNullException.ThrowIfNull(setIsWriting);
        ArgumentNullException.ThrowIfNull(setWriteStatus);
        ArgumentNullException.ThrowIfNull(reload);
        ArgumentNullException.ThrowIfNull(showError);

        if (isWriting())
        {
            return false;
        }

        setIsWriting(true);
        setWriteStatus(string.Empty);

        using var statusCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var statusTask = ShowRetryStatusAsync(
            setWriteStatus,
            retryStatusDelay ?? DefaultRetryStatusDelay,
            statusCancellation.Token);

        try
        {
            await write(cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (DatabaseWriteConflictException)
        {
            reload();
            showError(ConflictMessage);
            return false;
        }
        catch (DatabaseWriteRetryExhaustedException)
        {
            reload();
            showError(RetryExhaustedMessage);
            return false;
        }
        catch (InventoryDomainException ex)
        {
            showError(ex.Message);
            return false;
        }
        catch (Exception)
        {
            showError(TechnicalErrorMessage);
            return false;
        }
        finally
        {
            statusCancellation.Cancel();
            try
            {
                await statusTask;
            }
            catch (OperationCanceledException)
            {
                // tác vụ chỉ dùng để trì hoãn dòng trạng thái
            }

            setWriteStatus(string.Empty);
            setIsWriting(false);
        }
    }

    private static async Task ShowRetryStatusAsync(
        Action<string> setWriteStatus,
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        await Task.Delay(delay, cancellationToken);
        setWriteStatus(RetryStatus);
    }
}
