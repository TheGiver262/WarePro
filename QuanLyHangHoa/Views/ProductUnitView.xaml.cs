using System.Windows.Controls;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Views
{
    public partial class ProductUnitView : UserControl
    {
        public ProductUnitView()
        {
            InitializeComponent();
            DataContext = new ProductUnitViewModel();
        }
    }
}
