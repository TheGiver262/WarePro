using System.Windows.Controls;
namespace QuanLyHangHoa.Views {
    public partial class SupplierView : UserControl {
        public SupplierView() { InitializeComponent(); DataContext = new ViewModels.SupplierViewModel(); }
    }
}
