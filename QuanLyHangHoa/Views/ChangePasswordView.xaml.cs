using System.Windows.Controls;

namespace QuanLyHangHoa.Views
{
    public partial class ChangePasswordView : UserControl
    {
        public ChangePasswordView()
        {
            InitializeComponent();
        }

        private void PasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (this.DataContext is ViewModels.ChangePasswordViewModel vm)
            {
                vm.CurrentPassword = CurrentPasswordBox.Password;
                vm.NewPassword = NewPasswordBox.Password;
                vm.ConfirmPassword = ConfirmPasswordBox.Password;
            }
        }
    }
}
