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

        // PasswordBox không hỗ trợ binding Password, nên View chuyển giá trị sang ViewModel tại sự kiện thay đổi
        private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (this.DataContext != null)
            {
                // DataContext của cửa sổ login là LoginViewModel
                var vm = (LoginViewModel)this.DataContext;
                
                // giá trị này chỉ dùng cho lần xác thực, không được đưa vào session
                vm.Password = txtPassword.Password;
            }
        }
    }
}
