using System.Windows.Controls;

namespace QuanLyHangHoa.Views
{
    public partial class PurchaseInvoiceView : UserControl
    {
        public PurchaseInvoiceView()
        {
            InitializeComponent();
        }

        private void CreateNew_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ViewModels.PurchaseInvoiceViewModel vm)
            {
                vm.SelectedTabIndex = 1;
            }
        }

        private void BackToList_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ViewModels.PurchaseInvoiceViewModel vm)
            {
                vm.SelectedTabIndex = 0;
            }
        }
    }
}
