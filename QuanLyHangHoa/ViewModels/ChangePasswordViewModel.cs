using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class ChangePasswordViewModel : ObservableObject
    {
        private readonly AppUser _currentUser;
        private readonly Func<int, string, string, byte[], Guid, Task> _changePassword;
        private readonly Action<string, string> _showMessage;

        [ObservableProperty] private string _currentPassword = string.Empty;
        [ObservableProperty] private string _newPassword = string.Empty;
        [ObservableProperty] private string _confirmPassword = string.Empty;
        [ObservableProperty] private string _statusMessage = string.Empty;

        public ChangePasswordViewModel(AppUser currentUser, Func<AppDbContext> contextFactory)
        {
            _currentUser = currentUser;
            var service = new AuthenticationService(contextFactory);
            _changePassword = (id, current, next, rowVersion, operationId) =>
                service.ChangePasswordAsync(id, current, next, rowVersion, operationId);
            _showMessage = (message, title) =>
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public ChangePasswordViewModel(
            AppUser currentUser,
            Func<int, string, string, byte[], Guid, Task> changePassword,
            Action<string, string> showMessage)
        {
            _currentUser = currentUser;
            _changePassword = changePassword;
            _showMessage = showMessage;
        }

        public string Username => _currentUser.Username;

        [RelayCommand]
        // mật khẩu chỉ giữ trong form; service kiểm tra rowversion trước khi ghi để không đè thay đổi từ máy khác
        private async Task ChangePassword()
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
                var operationId = Guid.NewGuid();
                await _changePassword(
                    _currentUser.Id,
                    CurrentPassword,
                    NewPassword,
                    _currentUser.RowVersion.ToArray(),
                    operationId);
                StatusMessage = "Đã đổi mật khẩu thành công.";
                _showMessage(StatusMessage, "Thông báo");
                CurrentPassword = string.Empty;
                NewPassword = string.Empty;
                ConfirmPassword = string.Empty;
            }
            catch (DatabaseWriteConflictException)
            {
                StatusMessage = "Tài khoản đã thay đổi trên máy khác. Hãy đóng màn hình này và mở lại trước khi đổi mật khẩu.";
                _showMessage(StatusMessage, "Dữ liệu đã thay đổi");
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = ex.Message;
                _showMessage(ex.Message, "Lỗi đổi mật khẩu");
            }
        }
    }
}
