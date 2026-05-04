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
        [ObservableProperty] private string _roleCode = "Nhân viên bán hàng";
        [ObservableProperty] private bool _isActive = true;
        
        public ObservableCollection<string> Roles { get; } = ["Quản trị viên", "Quản lý", "Nhân viên bảo hành", "Nhân viên bán hàng", "Nhân viên kho"];

        public AddUserViewModel(int currentUserId, AppUserService userService)
        {
            _currentUserId = currentUserId;
            _userService = userService;
        }

        [RelayCommand]
        private void Confirm(Window? window)
        {
            if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Username))
            {
                MessageBox.Show("Vui lòng điền đầy đủ Tên và Tên tài khoản!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var newUser = new AppUser
                {
                    FullName = FullName.Trim(),
                    Username = Username.Trim(),
                    RoleCode = RoleCode,
                    IsActive = IsActive,
                    MustChangePassword = true 
                };

                _userService.AddUser(newUser, _currentUserId);
                
                if (window != null)
                {
                    window.DialogResult = true;
                    window.Close();
                }
            }
            catch (Exception ex)
            {
                var fullMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    fullMessage += $"\n\nInner Exception: {ex.InnerException.Message}";
                    if (ex.InnerException.InnerException != null)
                    {
                        fullMessage += $"\n\nDeep Inner Exception: {ex.InnerException.InnerException.Message}";
                    }
                }
                
                MessageBox.Show($"Không thể tạo người dùng:\n{fullMessage}", "Lỗi hệ thống", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Cancel(Window? window)
        {
            if (window != null)
            {
                window.DialogResult = false;
                window.Close();
            }
        }
    }
}
