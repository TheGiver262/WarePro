using System;
using System.Collections.ObjectModel;
using System.Linq;
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
        private ObservableCollection<AppUser> _users = [];

        [ObservableProperty]
        private AppUser? _selectedUser;

        [ObservableProperty] private AppUser _currentInputUser = new();
        
        [ObservableProperty] private string _searchFullName = string.Empty;
        [ObservableProperty] private string _searchUsername = string.Empty;
        [ObservableProperty] private string _searchRole = "Tất cả";
        [ObservableProperty] private DateTime? _searchDate;
        [ObservableProperty] private bool _isEditPanelOpen;

        public ObservableCollection<string> Roles { get; } = ["Tất cả", "Admin", "Manager", "Staff"];

        public AppUserViewModel()
            : this(null, new Data.AppDbContext())
        {
        }

        public bool IsAdmin => _currentUser?.RoleCode == "Admin";

        public AppUserViewModel(AppUser? currentUser, Data.AppDbContext dbContext)
        {
            _currentUser = currentUser;
            _userService = new AppUserService(dbContext);
            
            LoadData();
        }

        [RelayCommand]
        private void OpenAddUserDialog()
        {
            if (_currentUser == null) return;

            var vm = new AddUserViewModel(_currentUser.Id, _userService);
            var window = new AddUserWindow { DataContext = vm };
            
            if (window.ShowDialog() == true)
            {
                LoadData();
            }
        }

        private void LoadData()
        {
            var list = _userService.GetAllUsers();
            
            // Apply Filters
            if (!string.IsNullOrWhiteSpace(SearchFullName))
            {
                var term = SearchFullName.ToLower();
                list = list.Where(u => u.FullName != null && u.FullName.ToLower().Contains(term)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(SearchUsername))
            {
                var term = SearchUsername.ToLower();
                list = list.Where(u => u.Username != null && u.Username.ToLower().Contains(term)).ToList();
            }

            if (SearchRole != "Tất cả")
            {
                list = list.Where(u => string.Equals(u.RoleCode, SearchRole, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (SearchDate.HasValue)
            {
                list = list.Where(u => u.CreatedAt.Date == SearchDate.Value.Date).ToList();
            }

            Users = new ObservableCollection<AppUser>(list);
        }

        [RelayCommand]
        public void Search()
        {
            LoadData();
        }

        [RelayCommand]
        private void ClearInput()
        {
            CurrentInputUser = new AppUser();
            SelectedUser = null;
            IsEditPanelOpen = false;
        }

        [RelayCommand]
        private void EditUser(AppUser? user)
        {
            if (user == null) return;
            
            SelectedUser = user;
            CurrentInputUser = new AppUser 
            {
                Id = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                RoleCode = user.RoleCode,
                IsActive = user.IsActive,
                PasswordHash = "" 
            };
            IsEditPanelOpen = true;
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
                IsEditPanelOpen = false;
                SelectedUser = null;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void DeleteUser(AppUser? user)
        {
            if (user == null || _currentUser == null) return;

            var result = System.Windows.MessageBox.Show($"Bạn có chắc chắn muốn xoá người dùng '{user.Username}' khỏi hệ thống không?\nThao tác này không thể hoàn tác.", 
                "Xác nhận xoá", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                try
                {
                    _userService.DeleteUser(user.Id, _currentUser.Id);
                    LoadData();
                    if (SelectedUser?.Id == user.Id) ClearInput();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(ex.Message, "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void ToggleStatus(AppUser? user)
        {
            if (user == null || _currentUser == null) return;

            try
            {
                _userService.ToggleActiveStatus(user.Id, _currentUser.Id);
                LoadData();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        partial void OnSelectedUserChanged(AppUser? value)
        {
            // Do nothing automatically to avoid popups when just clicking rows
        }
        partial void OnSearchFullNameChanged(string value) => LoadData();
        partial void OnSearchUsernameChanged(string value) => LoadData();
        partial void OnSearchRoleChanged(string value) => LoadData();
        partial void OnSearchDateChanged(DateTime? value) => LoadData();
    }
}
