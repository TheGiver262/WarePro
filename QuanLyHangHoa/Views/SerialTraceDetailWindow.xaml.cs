using System.Windows;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Views;

public partial class SerialTraceDetailWindow : Window
{
    public SerialTraceDetailWindow(SerialTraceReportItem item)
    {
        InitializeComponent();
        DataContext = item;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
