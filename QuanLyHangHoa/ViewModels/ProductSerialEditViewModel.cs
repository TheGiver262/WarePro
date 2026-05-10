using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.ViewModels
{
    public partial class ProductSerialEditViewModel : ObservableObject
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly ProductSerial _originalSerial;
        private readonly int _userId;

        [ObservableProperty] private string? _serialNumber;
        [ObservableProperty] private string? _productName;
        [ObservableProperty] private string? _selectedStatus;
        [ObservableProperty] private string? _note;
        [ObservableProperty] private ObservableCollection<string> _statuses;

        public bool IsSaved { get; private set; }

        public ProductSerialEditViewModel(Func<AppDbContext> contextFactory, ProductSerial serial, int userId)
        {
            _contextFactory = contextFactory;
            _originalSerial = serial;
            _userId = userId;

            SerialNumber = serial.SerialNumber;
            ProductName = serial.Product?.DisplayName ?? "N/A";
            Note = serial.Note;

            Statuses = new ObservableCollection<string>
            {
                "Trong kho",
                "Đã bán",
                "Đã đặt",
                "Đang bảo hành",
                "Lỗi bảo hành",
                "Đã trả hàng",
                "Đã đổi mới",
                "Đã thanh lý",
                "Dừng"
            };

            SelectedStatus = GetStatusDisplay(serial.CurrentStatus);
        }

        private string GetStatusDisplay(string? status)
        {
            if (status == null) return "N/A";
            return status switch
            {
                "InStock" => "Trong kho",
                "Sold" => "Đã bán",
                "Reserved" => "Đã đặt",
                "InWarrantyProcess" => "Đang bảo hành",
                "WarrantyDefective" => "Lỗi bảo hành",
                "Returned" => "Đã trả hàng",
                "Scrapped" => "Đã thanh lý",
                "Replaced" => "Đã đổi mới",
                "Inactive" => "Dừng",
                _ => status
            };
        }

        private string GetStatusKey(string? display)
        {
            if (display == null) return "InStock";
            return display switch
            {
                "Trong kho" => "InStock",
                "Đã bán" => "Sold",
                "Đã đặt" => "Reserved",
                "Đang bảo hành" => "InWarrantyProcess",
                "Lỗi bảo hành" => "WarrantyDefective",
                "Đã trả hàng" => "Returned",
                "Đã thanh lý" => "Scrapped",
                "Đã đổi mới" => "Replaced",
                "Dừng" => "Inactive",
                _ => display
            };
        }

        [RelayCommand]
        private void Save()
        {
            try
            {
                using var db = _contextFactory();
                var serial = db.ProductSerials.FirstOrDefault(s => s.Id == _originalSerial.Id);
                if (serial != null)
                {
                    // Lưu lại trạng thái cũ để ghi log
                    var oldState = new { Status = serial.CurrentStatus, Note = serial.Note };

                    serial.Note = Note;
                    serial.CurrentStatus = GetStatusKey(SelectedStatus);
                    db.SaveChanges();

                    // Ghi log nhật ký hệ thống
                    var newState = new { Status = serial.CurrentStatus, Note = serial.Note };
                    var log = new AuditLog
                    {
                        EntityName = "ProductSerial",
                        EntityId = serial.Id,
                        ActionCode = "UPDATE",
                        PerformedBy = _userId,
                        PerformedAt = DateTime.Now,
                        BeforeJson = System.Text.Json.JsonSerializer.Serialize(oldState),
                        AfterJson = System.Text.Json.JsonSerializer.Serialize(newState)
                    };
                    db.AuditLogs.Add(log);
                    db.SaveChanges();

                    IsSaved = true;
                    
                    // Cập nhật lại đối tượng gốc để hiển thị ngoài danh sách mà không cần load lại nếu cần
                    _originalSerial.Note = Note;
                    _originalSerial.CurrentStatus = serial.CurrentStatus;
                    
                    CloseWindow();
                }
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
