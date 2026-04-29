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
        private readonly Employee _currentUser;
        private readonly Action<string, string, string> _changePassword;
        private readonly Action<string, string> _showMessage;

        [ObservableProperty] private string _currentPassword = string.Empty;
        [ObservableProperty] private string _newPassword = string.Empty;
        [ObservableProperty] private string _confirmPassword = string.Empty;
        [ObservableProperty] private string _statusMessage = string.Empty;

        public ChangePasswordViewModel(Employee currentUser)
            : this(
                currentUser,
                new AuthenticationService().ChangePassword,
                (message, title) => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information))
        {
        }

        public ChangePasswordViewModel(
            Employee currentUser,
            Action<string, string, string> changePassword,
            Action<string, string> showMessage)
        {
            _currentUser = currentUser;
            _changePassword = changePassword;
            _showMessage = showMessage;
        }

        public string Username => _currentUser.Username;

        [RelayCommand]
        private void ChangePassword()
        {
            if (string.IsNullOrWhiteSpace(CurrentPassword))
            {
                StatusMessage = "Vui long nhap mat khau hien tai.";
                _showMessage(StatusMessage, "Canh bao");
                return;
            }

            if (string.IsNullOrWhiteSpace(NewPassword))
            {
                StatusMessage = "Vui long nhap mat khau moi.";
                _showMessage(StatusMessage, "Canh bao");
                return;
            }

            if (NewPassword != ConfirmPassword)
            {
                StatusMessage = "Mat khau moi va xac nhan khong khop.";
                _showMessage(StatusMessage, "Canh bao");
                return;
            }

            try
            {
                _changePassword(_currentUser.Username, CurrentPassword, NewPassword);
                StatusMessage = "Da doi mat khau.";
                _showMessage(StatusMessage, "Thong bao");
                CurrentPassword = string.Empty;
                NewPassword = string.Empty;
                ConfirmPassword = string.Empty;
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = ex.Message;
                _showMessage(ex.Message, "Loi doi mat khau");
            }
        }
    }
}
