using System.Windows.Controls;
namespace QuanLyHangHoa.Views {
    public partial class CategoryView : UserControl {
        public CategoryView() { InitializeComponent(); DataContext = new ViewModels.CategoryViewModel(); }
    }
}
