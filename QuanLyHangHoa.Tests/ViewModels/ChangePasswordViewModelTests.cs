using QuanLyHangHoa.Models;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Tests.ViewModels;

public class ChangePasswordViewModelTests
{
    [Fact]
    public async Task ChangePasswordPassesCurrentUserAndFormValuesToService()
    {
        int? userId = null;
        string? currentPassword = null;
        string? newPassword = null;
        byte[]? rowVersion = null;
        Guid? operationId = null;
        var expectedRowVersion = new byte[] { 1, 2, 3 };

        var viewModel = new ChangePasswordViewModel(
            new AppUser
            {
                Id = 42,
                Username = "tester",
                RowVersion = expectedRowVersion
            },
            (id, current, next, expectedVersion, operation) =>
            {
                userId = id;
                currentPassword = current;
                newPassword = next;
                rowVersion = expectedVersion;
                operationId = operation;
                return Task.CompletedTask;
            },
            (_, _) => { });
        viewModel.CurrentPassword = "old-pass";
        viewModel.NewPassword = "new-pass";
        viewModel.ConfirmPassword = "new-pass";

        await viewModel.ChangePasswordCommand.ExecuteAsync(null);

        Assert.Equal(42, userId);
        Assert.Equal("old-pass", currentPassword);
        Assert.Equal("new-pass", newPassword);
        Assert.Equal(expectedRowVersion, rowVersion);
        Assert.NotEqual(Guid.Empty, operationId);
        Assert.Equal("Đã đổi mật khẩu thành công.", viewModel.StatusMessage);
        Assert.Equal(string.Empty, viewModel.CurrentPassword);
        Assert.Equal(string.Empty, viewModel.NewPassword);
        Assert.Equal(string.Empty, viewModel.ConfirmPassword);
    }

    [Fact]
    public async Task ChangePasswordRejectsMismatchedConfirmation()
    {
        var called = false;
        var viewModel = new ChangePasswordViewModel(
            new AppUser { Username = "tester" },
            (_, _, _, _, _) =>
            {
                called = true;
                return Task.CompletedTask;
            },
            (_, _) => { });
        viewModel.CurrentPassword = "old-pass";
        viewModel.NewPassword = "new-pass";
        viewModel.ConfirmPassword = "different";

        await viewModel.ChangePasswordCommand.ExecuteAsync(null);

        Assert.False(called);
        Assert.Equal("Mật khẩu mới và xác nhận không khớp.", viewModel.StatusMessage);
    }
}
