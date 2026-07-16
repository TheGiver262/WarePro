using System.Windows.Controls;
using System.Windows.Input;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Views
{
    public partial class StockAdjustmentView : UserControl
    {
        public StockAdjustmentView()
        {
            InitializeComponent();
        }

        // chặn ComboBox tự mở danh sách; click được chuyển sang dialog chọn serial có kiểm tra nghiệp vụ
        private void SerialSelector_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ComboBox { DataContext: StockAdjustmentLineEditor line } ||
                DataContext is not StockAdjustmentViewModel viewModel ||
                !viewModel.IsEditMode)
            {
                return;
            }

            viewModel.OpenSerialWindowCommand.Execute(line);
            e.Handled = true;
        }
    }
}
