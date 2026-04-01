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

        // Constructor khởi tạo AuthenticationService
        public LoginViewModel()
        {
            _authService = new AuthenticationService();
        }

        // Lệnh (Command) Đăng nhập được gọi khi người dùng ấn nút Login
        [RelayCommand]
        private void Login(Window currentWindow)
        {
            // Kiểm tra rỗng
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Vui lòng nhập tên tài khoản và mật khẩu!";
                return;
            }

            // Gọi logic xác thực tới DB
            var currentUser = _authService.Authenticate(Username, Password);

            if (currentUser != null)
            {
                // Đăng nhập thành công, mở cửa sổ MainWindow
                // Cửa sổ mới sẽ được cấp kèm theo currentUser để biết ai đang login
                MainWindow main = new MainWindow(currentUser);
                main.Show();

                // Đóng cửa sổ Login hiện tại
                currentWindow?.Close();
            }
            else
            {
                // Thất bại
                ErrorMessage = "Tên tài khoản hoặc mật khẩu không đúng!";
            }
        }
    }
}
