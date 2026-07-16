using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class ProductSerialEditViewModel : ObservableObject
    {
        private readonly ProductSerialService _serialService;
        private readonly ProductSerial _originalSerial;
        private readonly int _userId;

        [ObservableProperty] private string? _serialNumber;
        [ObservableProperty] private string? _productName;
        [ObservableProperty] private string? _note;
        public string StatusDisplay { get; }

        public bool IsSaved { get; private set; }

        public ProductSerialEditViewModel(Func<AppDbContext> contextFactory, ProductSerial serial, int userId)
        {
            _serialService = new ProductSerialService(contextFactory);
            _originalSerial = serial;
            _userId = userId;

            SerialNumber = serial.SerialNumber;
            ProductName = serial.Product?.DisplayName ?? "N/A";
            Note = serial.Note;
            StatusDisplay = GetStatusDisplay(serial.CurrentStatus);
        }

        private static string GetStatusDisplay(string? status)
        {
            if (status == null) return "N/A";
            if (string.Equals(status, "ReturnedToManufacturer", StringComparison.OrdinalIgnoreCase))
                return "Trả lại NCC";

            return status switch
            {
                "InStock" => "Trong kho",
                "Sold" => "Đã bán",
                "Reserved" => "Đã đặt",
                "InWarrantyProcess" => "Đang bảo hành",
                "WarrantyDefective" => "Lỗi bảo hành",
                "Returned" => "Đã trả hàng",
                "ReturnedToManufacturer" => "Trả lại NCC",
                "Scrapped" => "Đã thanh lý",
                "Replaced" => "Đã đổi mới",
                "Inactive" => "Dừng",
                _ => status
            };
        }

        [RelayCommand]
        // màn hình này chỉ sửa ghi chú; trạng thái và số serial không được thay ngoài nghiệp vụ kho/bảo hành
        private void Save()
        {
            try
            {
                _serialService.UpdateNote(_originalSerial.Id, Note, _userId);
                IsSaved = true;
                _originalSerial.Note = Note;
                CloseWindow();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lưu dữ liệu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            CloseWindow();
        }

        // DialogResult báo cho màn hình cha có cần reload danh sách hay không
        private void CloseWindow()
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window.DataContext == this)
                {
                    window.DialogResult = IsSaved;
                    window.Close();
                    break;
                }
            }
        }
    }
}
