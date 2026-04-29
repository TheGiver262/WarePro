using System.Windows.Controls;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Views
{
    public partial class OpeningBalanceImportView : UserControl
    {
        public OpeningBalanceImportView(int postedByUserId)
        {
            InitializeComponent();
            DataContext = new OpeningBalanceImportViewModel(postedByUserId);
        }
    }
}
