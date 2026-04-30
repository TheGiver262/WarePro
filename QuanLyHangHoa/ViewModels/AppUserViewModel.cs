using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Services.DataImport;
using QuanLyHangHoa.Views;

namespace QuanLyHangHoa.ViewModels
{
    public partial class AppUserViewModel : ObservableObject
    {
        private readonly AppUserService _userService;
        private readonly DataImportManager _importManager = new();
        private readonly AppUser? _currentUser;

        [ObservableProperty]
        private ObservableCollection<AppUser> _users = new();

        [ObservableProperty]
        private AppUser? _selectedUser;

        [ObservableProperty]
        private AppUser _currentInputUser;

        public AppUserViewModel()
            : this(null, new Data.AppDbContext())
        {
        }

        public AppUserViewModel(AppUser? currentUser, Data.AppDbContext dbContext)
        {
            _currentUser = currentUser;
            _userService = new AppUserService(dbContext);
            CurrentInputUser = new AppUser();
            LoadData();
        }

        private void LoadData()
        {
            var list = _userService.GetAllUsers();
            Users = new ObservableCollection<AppUser>(list);
        }

        [RelayCommand]
        private void ClearInput()
        {
            CurrentInputUser = new AppUser();
            SelectedUser = null;
        }

        [RelayCommand]
        private void SaveUser()
        {
            if (string.IsNullOrWhiteSpace(CurrentInputUser.FullName) || string.IsNullOrWhiteSpace(CurrentInputUser.Username))
            {
                System.Windows.MessageBox.Show("Vui lòng điền đủ Tên Đầy đủ và Tên tài khoản!", "Cảnh báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (_currentUser == null)
            {
                System.Windows.MessageBox.Show("Bạn không có quyền thực hiện thao tác này.", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            try
            {
                if (CurrentInputUser.Id == 0)
                {
                    _userService.AddUser(CurrentInputUser, _currentUser.Id);
                }
                else
                {
                    _userService.UpdateUser(CurrentInputUser, _currentUser.Id);
                }

                LoadData();
                ClearInput();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void DeactivateUser()
        {
            if (SelectedUser != null && SelectedUser.Id > 0 && _currentUser != null)
            {
                try
                {
                    _userService.DeleteUser(SelectedUser.Id, _currentUser.Id);
                    LoadData();
                    ClearInput();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(ex.Message, "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        partial void OnSelectedUserChanged(AppUser? value)
        {
            if (value != null)
            {
                CurrentInputUser = new AppUser 
                {
                    Id = value.Id,
                    Username = value.Username,
                    FullName = value.FullName,
                    RoleCode = value.RoleCode,
                    IsActive = value.IsActive,
                    PasswordHash = "" // Clear for UI safety, though service handles hash detection
                };
            }
        }
    }
}
