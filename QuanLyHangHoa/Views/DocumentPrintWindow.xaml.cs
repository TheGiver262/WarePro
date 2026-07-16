using System;
using System.Windows;
using System.Windows.Controls;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.Views;

public partial class DocumentPrintWindow : Window
{
    private readonly DocumentPrintModel _model;

    public DocumentPrintWindow(DocumentPrintModel model)
    {
        InitializeComponent();
        _model = model;
        DataContext = model;
        Title = model.Title;

        if (Application.Current?.MainWindow is { } owner && owner != this)
        {
            Owner = owner;
        }
    }

    // ẩn nút khỏi vùng PrintVisual và luôn hiện lại trong finally kể cả người dùng hủy hoặc in lỗi
    private void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ButtonPanel.Visibility = Visibility.Collapsed;
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                printDialog.PrintVisual(PrintArea, $"{_model.Title} {_model.Code}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi khi in: {ex.Message}", "Lỗi",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ButtonPanel.Visibility = Visibility.Visible;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
