using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Views;

namespace QuanLyHangHoa.ViewModels
{
    // Kế thừa từ ObservableObject của MVVM Toolkit để hỗ trợ NotifyPropertyChanged
    // Cập nhật giao diện tự động khi dữ liệu thay đổi
    public partial class LoginViewModel : ObservableObject
    {
        // Thuộc tính lưu Username do người dùng nhập vào
        [ObservableProperty]
        private string _username = string.Empty;

        // Lưu Password do người dùng nhập vào
        [ObservableProperty]
        private string _password = string.Empty;

        // Thông báo lỗi nếu đăng nhập sai
        [ObservableProperty]
        private string _errorMessage = string.Empty;

        private readonly AuthenticationService _authService;

        public LoginViewModel()
        {
            _authService = new AuthenticationService(() => new Data.AppDbContext());
        }

        [RelayCommand]
        private void Login(Window currentWindow)
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Vui lòng nhập tên tài khoản và mật khẩu!";
                return;
            }

            var result = _authService.Authenticate(Username, Password);

            switch (result.Status)
            {
                case LoginStatus.Success:
                    if (result.User != null)
                    {
                        MainWindow main = new MainWindow(result.User, () => new Data.AppDbContext());
                        main.Show();

                        if (result.User.MustChangePassword)
                        {
                            MessageBox.Show("Đây là lần đầu bạn đăng nhập. Vui lòng đổi mật khẩu để tiếp tục!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                            // Sử dụng command OpenChangePassword trong ViewModel của MainWindow
                            if (main.DataContext is MainViewModel mainVm)
                            {
                                mainVm.OpenChangePasswordViewCommand.Execute(null);
                            }
                        }
                        
                        currentWindow?.Close();
                    }
                    break;

                case LoginStatus.LockedOut:
                    var localTime = result.LockoutUntil?.ToLocalTime().ToString("HH:mm:ss") ?? "n/a";
                    ErrorMessage = $"Tài khoản bị khóa tạm thời đến {localTime} do nhập sai nhiều lần.";
                    break;

                case LoginStatus.Inactive:
                    ErrorMessage = "Tài khoản bị vô hiệu hóa do vi phạm bảo mật. Vui lòng liên hệ Admin.";
                    break;

                case LoginStatus.InvalidCredentials:
                default:
                    ErrorMessage = "Tên tài khoản hoặc mật khẩu không đúng!";
                    break;
            }
        }
    }
}
