using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class ChangePasswordViewModel : ObservableObject
    {
        private readonly AppUser _currentUser;
        private readonly Action<int, string, string> _changePassword;
        private readonly Action<string, string> _showMessage;

        [ObservableProperty] private string _currentPassword = string.Empty;
        [ObservableProperty] private string _newPassword = string.Empty;
        [ObservableProperty] private string _confirmPassword = string.Empty;
        [ObservableProperty] private string _statusMessage = string.Empty;

        public ChangePasswordViewModel(AppUser currentUser, Func<Data.AppDbContext> contextFactory)
            : this(
                currentUser,
                new AuthenticationService(contextFactory).ChangePassword,
                (message, title) => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information))
        {
        }

        public ChangePasswordViewModel(
            AppUser currentUser,
            Action<int, string, string> changePassword,
            Action<string, string> showMessage)
        {
            _currentUser = currentUser;
            _changePassword = changePassword;
            _showMessage = showMessage;
        }

        public string Username => _currentUser.Username;

        [RelayCommand]
        // mật khẩu hiện tại/mới chỉ giữ trong form đến khi service hoàn tất; thành công xóa cả ba field ngay
        private void ChangePassword()
        {
            if (string.IsNullOrWhiteSpace(CurrentPassword))
            {
                StatusMessage = "Vui lòng nhập mật khẩu hiện tại.";
                _showMessage(StatusMessage, "Cảnh báo");
                return;
            }

            if (string.IsNullOrWhiteSpace(NewPassword))
            {
                StatusMessage = "Vui lòng nhập mật khẩu mới.";
                _showMessage(StatusMessage, "Cảnh báo");
                return;
            }

            if (NewPassword != ConfirmPassword)
            {
                StatusMessage = "Mật khẩu mới và xác nhận không khớp.";
                _showMessage(StatusMessage, "Cảnh báo");
                return;
            }

            try
            {
                _changePassword(_currentUser.Id, CurrentPassword, NewPassword);
                StatusMessage = "Đã đổi mật khẩu thành công.";
                _showMessage(StatusMessage, "Thông báo");
                CurrentPassword = string.Empty;
                NewPassword = string.Empty;
                ConfirmPassword = string.Empty;
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = ex.Message;
                _showMessage(ex.Message, "Lỗi đổi mật khẩu");
            }
        }
    }
}
