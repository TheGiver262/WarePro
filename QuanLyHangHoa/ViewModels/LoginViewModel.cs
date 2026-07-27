using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Views;

namespace QuanLyHangHoa.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        [ObservableProperty] private string _username = string.Empty;
        [ObservableProperty] private string _password = string.Empty;
        [ObservableProperty] private string _errorMessage = string.Empty;
        [ObservableProperty] private string _startupMessage = "Đang kết nối cơ sở dữ liệu...";
        [ObservableProperty] private bool _isDatabaseReady;

        private readonly AuthenticationService _authService;

        public LoginViewModel()
        {
            _authService = new AuthenticationService(() => new Data.AppDbContext());
            _ = ObserveDatabaseReadyAsync();
        }

        private bool CanLogin() => IsDatabaseReady;

        partial void OnIsDatabaseReadyChanged(bool value)
        {
            LoginCommand.NotifyCanExecuteChanged();
        }

        // khóa nút đăng nhập đến khi startup hoàn tất kết nối/migration, tránh lỗi giả do database chưa sẵn sàng
        private async Task ObserveDatabaseReadyAsync()
        {
            try
            {
                await App.DatabaseReady;
                IsDatabaseReady = true;
                StartupMessage = string.Empty;
            }
            catch
            {
                StartupMessage = string.Empty;
                ErrorMessage = "Không thể kết nối cơ sở dữ liệu. Vui lòng kiểm tra SQL Server và mở lại ứng dụng.";
            }
        }

        [RelayCommand(CanExecute = nameof(CanLogin))]
        // password chỉ đi vào AuthenticationService; session mới chỉ giữ AppUser từ LoginResult và cửa sổ login đóng khi thành công
        private async Task Login(Window currentWindow)
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Vui lòng nhập tên tài khoản và mật khẩu!";
                return;
            }

            // BCrypt chạy ở worker để không khóa UI; mọi lỗi đăng nhập dùng thông báo chung, không lộ tài khoản tồn tại
            LoginResult result;
            try
            {
                result = await Task.Run(() =>
                    _authService.AuthenticateAsync(Username, Password, Guid.NewGuid()));
            }
            catch (DatabaseWriteConflictException)
            {
                ErrorMessage = "Tài khoản vừa thay đổi trên máy khác. Vui lòng thử đăng nhập lại.";
                return;
            }
            catch (DatabaseWriteRetryExhaustedException)
            {
                ErrorMessage = DatabaseWriteUi.RetryExhaustedMessage;
                return;
            }
            switch (result.Status)
            {
                case LoginStatus.Success:
                    if (result.User != null)
                    {
                        var main = new MainWindow(result.User, () => new Data.AppDbContext());
                        Application.Current.MainWindow = main;
                        main.Show();

                        if (result.User.MustChangePassword)
                        {
                            MessageBox.Show("Đây là lần đầu bạn đăng nhập. Vui lòng đổi mật khẩu để tiếp tục!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                            if (main.DataContext is MainViewModel mainVm)
                            {
                                mainVm.OpenChangePasswordViewCommand.Execute(null);
                            }
                        }

                        currentWindow?.Close();
                    }
                    break;

                case LoginStatus.LockedOut:
                    ErrorMessage = "Tên tài khoản hoặc mật khẩu không đúng hoặc tài khoản đang tạm khóa!";
                    break;
                case LoginStatus.Inactive:
                    ErrorMessage = "Tên tài khoản hoặc mật khẩu không đúng!";
                    break;
                case LoginStatus.InvalidCredentials:
                default:
                    ErrorMessage = result.FailedLoginCount >= 3 && result.FailedLoginCount < 5
                        ? "Tên tài khoản hoặc mật khẩu không đúng!\n(Nhập sai tên đăng nhập/mật khẩu liên tiếp sẽ bị khóa tài khoản tạm thời)"
                        : "Tên tài khoản hoặc mật khẩu không đúng!";
                    break;
            }
        }
    }
}
