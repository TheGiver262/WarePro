using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.Views
{
    public partial class SerialInputWindow : Window
    {
        // ── Dependency properties ──────────────────────────────────────────────
        public static readonly DependencyProperty SerialInputProperty =
            DependencyProperty.Register(nameof(SerialInput), typeof(string), typeof(SerialInputWindow),
                new PropertyMetadata(string.Empty, (d, e) => ((SerialInputWindow)d).UpdatePreview()));

        public string SerialInput
        {
            get => (string)GetValue(SerialInputProperty);
            set => SetValue(SerialInputProperty, value);
        }

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(SerialInputWindow),
                new PropertyMetadata(false));

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        public List<string> AvailableSerials { get; } = new();
        public bool HasAvailableSerials => AvailableSerials.Count > 0;

        public bool ShowAvailableSerials => HasAvailableSerials && !IsReadOnly;
        public string CancelButtonText => IsReadOnly ? "ĐÓNG" : "HỦY BỎ";
        public bool ShowConfirmButton => !IsReadOnly;
        public HorizontalAlignment CancelButtonAlignment => IsReadOnly ? HorizontalAlignment.Right : HorizontalAlignment.Left;

        // ── Converters via code ────────────────────────────────────────────────
        public SerialInputWindow(string existingInput = "", IEnumerable<ProductSerial>? available = null, bool isReadOnly = false)
        {
            IsReadOnly = isReadOnly;
            InitializeComponent();
            SerialInput = existingInput;
            if (available != null)
                AvailableSerials.AddRange(available.Select(s => s.SerialNumber));
            DataContext = this;
            UpdatePreview();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                DragMove();
        }

        private void UpdatePreview()
        {
            if (PreviewLabel == null) return;
            var parsed = StockInService.ParseSerialRange(SerialInput);
            if (IsReadOnly)
            {
                PreviewLabel.Text = parsed.Count > 0
                    ? $"→ Có {parsed.Count} serial number."
                    : "Không có serial number.";
            }
            else
            {
                PreviewLabel.Text = parsed.Count > 0
                    ? $"→ Sẽ tạo {parsed.Count} serial number."
                    : "Nhập serial để xem trước.";
            }
        }

        private void AvailableListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AvailableListBox.SelectedItem is string serial)
            {
                // Append to text box
                var lines = SerialInput.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
                if (!lines.Contains(serial))
                {
                    lines.Add(serial);
                    SerialInput = string.Join("\n", lines);
                }
                AvailableListBox.SelectedItem = null;
            }
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
