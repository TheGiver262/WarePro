using System.Windows.Controls;
namespace QuanLyHangHoa.Views {
    public partial class CustomerView : UserControl {
        public CustomerView() { InitializeComponent(); DataContext = new ViewModels.CustomerViewModel(); }
    }
}
