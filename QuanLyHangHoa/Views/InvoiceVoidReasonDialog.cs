using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace QuanLyHangHoa.Views;

public sealed class InvoiceVoidReasonDialog : Window
{
    private readonly TextBox _reasonBox;
    private readonly TextBlock _validationText;

    public InvoiceVoidReasonDialog(string invoiceCode)
    {
        Title = "Hủy hóa đơn";
        Width = 500;
        Height = 300;
        MinWidth = 500;
        MinHeight = 300;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Background = Brushes.White;

        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var title = new TextBlock
        {
            Text = "XÁC NHẬN HỦY HÓA ĐƠN",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(32, 42, 56))
        };
        Grid.SetRow(title, 0);
        root.Children.Add(title);

        var description = new TextBlock
        {
            Text = $"Hóa đơn {invoiceCode} sẽ được giữ lại và đánh dấu Đã hủy. Vui lòng nhập lý do:",
            Margin = new Thickness(0, 12, 0, 8),
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105))
        };
        Grid.SetRow(description, 1);
        root.Children.Add(description);

        var inputPanel = new Grid();
        inputPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        inputPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _reasonBox = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(10),
            BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            BorderThickness = new Thickness(1)
        };
        inputPanel.Children.Add(_reasonBox);
        _validationText = new TextBlock
        {
            Text = string.Empty,
            Margin = new Thickness(0, 6, 0, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40))
        };
        Grid.SetRow(_validationText, 1);
        inputPanel.Children.Add(_validationText);
        Grid.SetRow(inputPanel, 2);
        root.Children.Add(inputPanel);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        var cancel = new Button { Content = "Đóng", MinWidth = 90, Height = 36, Margin = new Thickness(0, 0, 10, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var confirm = new Button
        {
            Content = "Hủy hóa đơn",
            MinWidth = 120,
            Height = 36,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromRgb(198, 40, 40)),
            BorderThickness = new Thickness(0)
        };
        confirm.Click += Confirm;
        actions.Children.Add(cancel);
        actions.Children.Add(confirm);
        Grid.SetRow(actions, 3);
        root.Children.Add(actions);

        Content = root;
        Loaded += (_, _) => _reasonBox.Focus();
    }

    public string Reason { get; private set; } = string.Empty;

    private void Confirm(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_reasonBox.Text))
        {
            _validationText.Text = "Lý do hủy không được để trống.";
            _reasonBox.Focus();
            return;
        }

        Reason = _reasonBox.Text.Trim();
        DialogResult = true;
    }
}
