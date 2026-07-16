using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.Views
{
    public class AvailableSerialItem : ObservableObject
    {
        private string _serialNumber = string.Empty;
        public string SerialNumber
        {
            get => _serialNumber;
            set => SetProperty(ref _serialNumber, value);
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }

    public partial class SerialInputWindow : Window
    {
        // ── Dependency properties ──────────────────────────────────────────────
        public static readonly DependencyProperty SerialInputProperty =
            DependencyProperty.Register(nameof(SerialInput), typeof(string), typeof(SerialInputWindow),
                new PropertyMetadata(string.Empty));

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

        public ObservableCollection<AvailableSerialItem> AvailableSerials { get; } = new();
        // chặn TextChanged và CheckBox tự gọi qua lại khi đồng bộ hai cách nhập serial
        private bool _isUpdating;
        private bool _hasAvailableSource;
        private readonly bool _requireNonEmptySerials;

        public bool HasAvailableSerials => AvailableSerials.Count > 0;
        public bool ShowAvailableSerials => HasAvailableSerials && !IsReadOnly;
        public bool ShowAvailableColumn => _hasAvailableSource;
        public string CancelButtonText => IsReadOnly ? "ĐÓNG" : "HỦY BỎ";
        public bool ShowConfirmButton => !IsReadOnly;
        public HorizontalAlignment CancelButtonAlignment => IsReadOnly ? HorizontalAlignment.Right : HorizontalAlignment.Left;

        public SerialInputWindow(string existingInput = "", IEnumerable<ProductSerial>? available = null, bool isReadOnly = false, bool requireNonEmptySerials = false)
        {
            IsReadOnly = isReadOnly;
            _hasAvailableSource = available != null;
            _requireNonEmptySerials = requireNonEmptySerials;
            InitializeComponent();

            _isUpdating = true;
            try
            {
                if (available != null)
                {
                    var existingSerials = new HashSet<string>(
                        StockInService.ParseSerialRange(existingInput), 
                        StringComparer.OrdinalIgnoreCase
                    );

                    foreach (var s in available)
                    {
                        var item = new AvailableSerialItem
                        {
                            SerialNumber = s.SerialNumber,
                            IsSelected = existingSerials.Contains(s.SerialNumber)
                        };
                        AvailableSerials.Add(item);
                    }
                }

                SerialInput = existingInput;
                DataContext = this;
                
                // Set the TextBox text directly to trigger the TextChanged handler once initially
                SerialTextBox.Text = existingInput;
            }
            finally
            {
                _isUpdating = false;
            }
            UpdatePreview();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                DragMove();
        }

        // dùng cùng parser với service để số lượng xem trước khớp dữ liệu sẽ lưu
        private void UpdatePreview()
        {
            if (PreviewLabel == null) return;
            var parsed = StockInService.ParseSerialRange(SerialTextBox.Text);
            if (IsReadOnly)
            {
                PreviewLabel.Text = parsed.Count > 0
                    ? $"→ Có {parsed.Count} serial number."
                    : "Không có serial number.";
            }
            else
            {
                PreviewLabel.Text = parsed.Count > 0
                    ? $"→ Sẽ chọn {parsed.Count} serial number."
                    : "Nhập serial để xem trước.";
            }
        }
        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdating) return;
            if (sender is CheckBox cb && cb.DataContext is AvailableSerialItem item)
            {
                _isUpdating = true;
                try
                {
                    item.IsSelected = cb.IsChecked ?? false;
                    UpdateTextBoxFromCheckboxes();
                }
                finally
                {
                    _isUpdating = false;
                }
            }
        }

        private void UpdateTextBoxFromCheckboxes()
        {
            var selected = AvailableSerials
                .Where(x => x.IsSelected)
                .Select(x => x.SerialNumber)
                .ToList();
            SerialTextBox.Text = string.Join(Environment.NewLine, selected);
            SerialInput = SerialTextBox.Text;
        }

        private void SerialTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreview();

            if (_isUpdating) return;

            _isUpdating = true;
            try
            {
                var parsed = new HashSet<string>(
                    StockInService.ParseSerialRange(SerialTextBox.Text),
                    StringComparer.OrdinalIgnoreCase
                );
                foreach (var item in AvailableSerials)
                {
                    item.IsSelected = parsed.Contains(item.SerialNumber);
                }
                SerialInput = SerialTextBox.Text;
            }
            finally
            {
                _isUpdating = false;
            }
        }

        // một số nghiệp vụ bắt buộc ít nhất một serial; DialogResult=true chỉ đặt sau khi qua kiểm tra
        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            var serials = StockInService.ParseSerialRange(SerialTextBox.Text);
            if (_requireNonEmptySerials && serials.Count == 0)
            {
                MessageBox.Show(
                    "Vui l\u00f2ng ch\u1ecdn ho\u1eb7c nh\u1eadp \u00edt nh\u1ea5t m\u1ed9t serial.",
                    "Thi\u1ebfu Serial",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            SerialInput = SerialTextBox.Text;
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
