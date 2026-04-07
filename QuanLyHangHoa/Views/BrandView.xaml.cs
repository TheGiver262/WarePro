using System.Windows.Controls;
namespace QuanLyHangHoa.Views {
    public partial class BrandView : UserControl {
        public BrandView() { InitializeComponent(); DataContext = new ViewModels.BrandViewModel(); }
    }
}
