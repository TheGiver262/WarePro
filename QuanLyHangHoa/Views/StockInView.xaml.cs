using System.Windows;
using System.Windows.Controls;

namespace QuanLyHangHoa.Views {
    public partial class StockInView : UserControl {
        public StockInView() { InitializeComponent(); }

        private void ProductComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                if (comboBox.SelectedItem == null && !string.IsNullOrWhiteSpace(comboBox.Text))
                {
                    MessageBox.Show("Sản phẩm này chưa có trong danh mục sản phẩm. Vui lòng cập nhật danh mục.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    comboBox.Text = string.Empty;
                }
            }
        }

        private void UnitComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                if (comboBox.SelectedItem == null && !string.IsNullOrWhiteSpace(comboBox.Text))
                {
                    MessageBox.Show("Đơn vị tính này chưa có trong danh mục đơn vị tính. Vui lòng cập nhật danh mục.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    comboBox.Text = string.Empty;
                }
            }
        }
    }
}
