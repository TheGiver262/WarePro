using System.Collections.Generic;
using System.Windows;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Views
{
    public partial class ImportResultWindow : Window
    {
        public List<RowError> Errors { get; set; }

        public ImportResultWindow(int successCount, List<RowError> errors)
        {
            InitializeComponent();
            Errors = errors;
            TxtSuccess.Text = $"Thành công: {successCount}";
            TxtFailed.Text = $"Thất bại: {errors.Count}";
            DgErrors.ItemsSource = Errors;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
