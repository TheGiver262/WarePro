using System.Windows;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Views
{
    public partial class LoginView : Window
    {
        public LoginView()
        {
            InitializeComponent();
        }

        // Bắt sự kiện Click nút đóng X để thoát ứng dụng
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // Vì WPF không hỗ trợ Binding trực tiếp vào Password (vì lý do bảo mật)
        // Nên ta sẽ bắt sự kiện TextChange tại View và gán vào ViewModel thủ công
        private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (this.DataContext != null)
            {
                // Ép kiểu DataContext hiện tại (đang được liên kết với LoginViewModel)
                var vm = (LoginViewModel)this.DataContext;
                
                // Set chuỗi mật khẩu từ PasswordBox sang _password trong ViewModel
                vm.Password = txtPassword.Password;
            }
        }
    }
}
