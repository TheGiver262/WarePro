using QuanLyHangHoa.Models;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Tests.ViewModels;

public class ChangePasswordViewModelTests
{
    [Fact]
    public void ChangePasswordPassesCurrentUserAndFormValuesToService()
    {
        string? username = null;
        string? currentPassword = null;
        string? newPassword = null;

        var viewModel = new ChangePasswordViewModel(
            new Employee { Username = "tester" },
            (user, current, next) =>
            {
                username = user;
                currentPassword = current;
                newPassword = next;
            },
            (_, _) => { });
        viewModel.CurrentPassword = "old-pass";
        viewModel.NewPassword = "new-pass";
        viewModel.ConfirmPassword = "new-pass";

        viewModel.ChangePasswordCommand.Execute(null);

        Assert.Equal("tester", username);
        Assert.Equal("old-pass", currentPassword);
        Assert.Equal("new-pass", newPassword);
        Assert.Equal("Da doi mat khau.", viewModel.StatusMessage);
        Assert.Equal(string.Empty, viewModel.CurrentPassword);
        Assert.Equal(string.Empty, viewModel.NewPassword);
        Assert.Equal(string.Empty, viewModel.ConfirmPassword);
    }

    [Fact]
    public void ChangePasswordRejectsMismatchedConfirmation()
    {
        var called = false;
        var viewModel = new ChangePasswordViewModel(
            new Employee { Username = "tester" },
            (_, _, _) => called = true,
            (_, _) => { });
        viewModel.CurrentPassword = "old-pass";
        viewModel.NewPassword = "new-pass";
        viewModel.ConfirmPassword = "different";

        viewModel.ChangePasswordCommand.Execute(null);

        Assert.False(called);
        Assert.Equal("Mat khau moi va xac nhan khong khop.", viewModel.StatusMessage);
    }
}
