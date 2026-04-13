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
            TxtSummary.Text = $"Thành công: {successCount} dòng. Thất bại: {errors.Count} dòng.";
            DgErrors.ItemsSource = Errors;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
