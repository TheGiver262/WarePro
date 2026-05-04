using System;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class AddUserViewModel : ObservableObject
    {
        private readonly AppUserService _userService;
        private readonly int _currentUserId;

        [ObservableProperty] private string _fullName = string.Empty;
        [ObservableProperty] private string _username = string.Empty;
        [ObservableProperty] private string _roleCode = "Staff";
        
        public ObservableCollection<string> Roles { get; } = ["Admin", "Manager", "Staff"];

        public AddUserViewModel(int currentUserId, AppUserService userService)
        {
            _currentUserId = currentUserId;
            _userService = userService;
        }

        [RelayCommand]
        private void Confirm(Window window)
        {
            if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Username))
            {
                MessageBox.Show("Vui lòng điền đầy đủ thông tin!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var newUser = new AppUser
                {
                    FullName = FullName,
                    Username = Username,
                    RoleCode = RoleCode,
                    IsActive = true,
                    MustChangePassword = true // Bắt buộc đổi mật khẩu lần đầu
                };

                _userService.AddUser(newUser, _currentUserId);
                
                window.DialogResult = true;
                window.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Cancel(Window window)
        {
            window.DialogResult = false;
            window.Close();
        }
    }
}
