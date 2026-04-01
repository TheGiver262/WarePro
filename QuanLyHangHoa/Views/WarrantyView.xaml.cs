using System.Windows.Controls;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Views
{
    public partial class WarrantyView : UserControl
    {
        public WarrantyView()
        {
            InitializeComponent();
            this.DataContext = new WarrantyViewModel();
        }
    }
}
