using QuanLyHangHoa.Models;
using QuanLyHangHoa.ViewModels;
using Xunit;

namespace QuanLyHangHoa.Tests.ViewModels;

public class ChangePasswordViewModelTests
{
    [Fact]
    public void ChangePasswordPassesCurrentUserAndFormValuesToService()
    {
        int? userId = null;
        string? currentPassword = null;
        string? newPassword = null;

        var viewModel = new ChangePasswordViewModel(
            new AppUser { Id = 42, Username = "tester" },
            (id, current, next) =>
            {
                userId = id;
                currentPassword = current;
                newPassword = next;
            },
            (_, _) => { });
        viewModel.CurrentPassword = "old-pass";
        viewModel.NewPassword = "new-pass";
        viewModel.ConfirmPassword = "new-pass";

        viewModel.ChangePasswordCommand.Execute(null);

        Assert.Equal(42, userId);
        Assert.Equal("old-pass", currentPassword);
        Assert.Equal("new-pass", newPassword);
        Assert.Equal("Đã đổi mật khẩu thành công.", viewModel.StatusMessage);
        Assert.Equal(string.Empty, viewModel.CurrentPassword);
        Assert.Equal(string.Empty, viewModel.NewPassword);
        Assert.Equal(string.Empty, viewModel.ConfirmPassword);
    }

    [Fact]
    public void ChangePasswordRejectsMismatchedConfirmation()
    {
        var called = false;
        var viewModel = new ChangePasswordViewModel(
            new AppUser { Username = "tester" },
            (_, _, _) => called = true,
            (_, _) => { });
        viewModel.CurrentPassword = "old-pass";
        viewModel.NewPassword = "new-pass";
        viewModel.ConfirmPassword = "different";

        viewModel.ChangePasswordCommand.Execute(null);

        Assert.False(called);
        Assert.Equal("Mật khẩu mới và xác nhận không khớp.", viewModel.StatusMessage);
    }
}
