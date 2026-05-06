using System.Windows;

namespace QuanLyHangHoa.Views
{
    public partial class CustomerEditWindow : Window
    {
        public CustomerEditWindow()
        {
            InitializeComponent();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
