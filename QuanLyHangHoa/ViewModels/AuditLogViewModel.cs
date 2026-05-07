using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using QuanLyHangHoa.Data;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class AuditLogViewModel : ObservableObject
    {
        private readonly AuditQueryService _auditService = null!;
        private readonly AppUserService _userService = null!;

        [ObservableProperty] private ObservableCollection<AuditLog> _logs = new();
        [ObservableProperty] private AuditLog? _selectedLog;
        [ObservableProperty] private string _diffResult = string.Empty;

        // Filters
        [ObservableProperty] private string? _selectedEntity;
        [ObservableProperty] private string? _selectedAction;
        [ObservableProperty] private AppUser? _selectedUser;
        [ObservableProperty] private DateTime? _fromDate;
        [ObservableProperty] private DateTime? _toDate;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private int _totalLogs;

        // Archive Dialog
        [ObservableProperty] private bool _isArchiveDialogOpen;
        [ObservableProperty] private DateTime _archiveFromDate = new DateTime(DateTime.Now.Year - 1, 1, 1);
        [ObservableProperty] private DateTime _archiveToDate = new DateTime(DateTime.Now.Year - 1, 12, 31);
        [ObservableProperty] private string _archiveValidationMessage = string.Empty;

        public ObservableCollection<string> EntityNames { get; } = new();
        public ObservableCollection<string> ActionCodes { get; } = new();
        public ObservableCollection<AppUser> Users { get; } = new();

        private readonly Func<AppDbContext> _contextFactory;

        public AuditLogViewModel(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            _auditService = new AuditQueryService(contextFactory);
            _userService = new AppUserService(contextFactory);
            LoadMetadata();
            LoadLogs();
        }

        private void LoadMetadata()
        {
            using var db = _contextFactory();
            var entities = db.AuditLogs.Select(a => a.EntityName).Distinct().ToList();
            EntityNames.Add("Tất cả");
            foreach (var e in entities) if (e != null) EntityNames.Add(e);

            var actions = db.AuditLogs.Select(a => a.ActionCode).Distinct().ToList();
            ActionCodes.Add("Tất cả");
            foreach (var a in actions) if (a != null) ActionCodes.Add(a);

            var users = _userService.GetAllUsers();
            foreach (var u in users) Users.Add(u);
        }

        [RelayCommand]
        private void LoadLogs()
        {
            var results = _auditService.GetAllAuditLogs(
                SelectedEntity == "Tất cả" ? null : SelectedEntity,
                SelectedAction == "Tất cả" ? null : SelectedAction,
                SelectedUser?.Id,
                FromDate,
                ToDate,
                SearchText);

            Logs.Clear();
            foreach (var log in results) Logs.Add(log);
            TotalLogs = results.Count;
        }

        [RelayCommand]
        private void ResetFilters()
        {
            SelectedEntity = "Tất cả";
            SelectedAction = "Tất cả";
            SelectedUser = null;
            FromDate = null;
            ToDate = null;
            SearchText = string.Empty;
            LoadLogs();
        }

        [RelayCommand]
        private void ExportLogs()
        {
            if (Logs == null || !Logs.Any())
            {
                MessageBox.Show("Không có dữ liệu để xuất Excel.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = $"NhatKyHeThong_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                Title = "Xuất nhật ký hệ thống"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    ExportLogsToExcel(Logs, saveFileDialog.FileName);
                    MessageBox.Show("Xuất dữ liệu thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi xuất Excel: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void OpenArchiveDialog()
        {
            ArchiveFromDate = new DateTime(DateTime.Now.Year - 1, 1, 1);
            ArchiveToDate = new DateTime(DateTime.Now.Year - 1, 12, 31);
            ArchiveValidationMessage = string.Empty;
            IsArchiveDialogOpen = true;
        }

        [RelayCommand]
        private void CloseArchiveDialog() => IsArchiveDialogOpen = false;

        [RelayCommand]
        private void ConfirmArchive()
        {
            ArchiveValidationMessage = string.Empty;

            if (ArchiveFromDate.Year >= DateTime.Now.Year || ArchiveToDate.Year >= DateTime.Now.Year)
            {
                ArchiveValidationMessage = "Chỉ lưu trữ được dữ liệu trên 1 năm";
                return;
            }

            if (ArchiveFromDate > ArchiveToDate)
            {
                ArchiveValidationMessage = "Ngày bắt đầu phải nhỏ hơn ngày kết thúc";
                return;
            }

            var oldLogs = _auditService.GetLogsBetween(ArchiveFromDate, ArchiveToDate);
            if (!oldLogs.Any())
            {
                MessageBox.Show($"Không có nhật ký nào từ {ArchiveFromDate:dd/MM/yyyy} đến {ArchiveToDate:dd/MM/yyyy} để lưu trữ.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show($"Tìm thấy {oldLogs.Count} bản ghi. Bạn có muốn xuất ra Excel sau đó xóa chúng khỏi hệ thống không?", 
                "Xác nhận lưu trữ", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            // Step 1: Export to Excel
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = $"AuditLog_Archive_{ArchiveFromDate:yyyyMMdd}_{ArchiveToDate:yyyyMMdd}.xlsx",
                Title = "Lưu tệp nhật ký lưu trữ"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    ExportLogsToExcel(oldLogs, saveFileDialog.FileName);

                    // Step 2: Delete from DB
                    int count = _auditService.DeleteLogs(oldLogs.Select(l => l.Id));
                    MessageBox.Show($"Đã xuất tệp thành công và xóa {count} bản ghi nhật ký khỏi hệ thống.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    IsArchiveDialogOpen = false;
                    LoadLogs();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi trong quá trình lưu trữ: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportLogsToExcel(IEnumerable<AuditLog> logs, string filePath)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("AuditLogs");

            // Headers
            worksheet.Cell(1, 1).Value = "ID";
            worksheet.Cell(1, 2).Value = "Thời gian";
            worksheet.Cell(1, 3).Value = "Nhân viên";
            worksheet.Cell(1, 4).Value = "Đối tượng";
            worksheet.Cell(1, 5).Value = "ID Đối tượng";
            worksheet.Cell(1, 6).Value = "Hành động";
            worksheet.Cell(1, 7).Value = "Dữ liệu trước";
            worksheet.Cell(1, 8).Value = "Dữ liệu sau";

            var headerRow = worksheet.Row(1);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.LightBlue;

            // Data
            int row = 2;
            foreach (var log in logs)
            {
                worksheet.Cell(row, 1).Value = log.Id;
                worksheet.Cell(row, 2).Value = log.PerformedAt.ToString("yyyy-MM-dd HH:mm:ss");
                worksheet.Cell(row, 3).Value = log.Performer?.FullName ?? "N/A";
                worksheet.Cell(row, 4).Value = log.EntityName;
                worksheet.Cell(row, 5).Value = log.EntityId;
                worksheet.Cell(row, 6).Value = log.ActionCode;
                worksheet.Cell(row, 7).Value = log.BeforeJson;
                worksheet.Cell(row, 8).Value = log.AfterJson;
                row++;
            }

            worksheet.Columns().AdjustToContents();
            workbook.SaveAs(filePath);
        }

        partial void OnSelectedEntityChanged(string? value) => LoadLogs();
        partial void OnSelectedActionChanged(string? value) => LoadLogs();
        partial void OnSelectedUserChanged(AppUser? value) => LoadLogs();
        partial void OnFromDateChanged(DateTime? value) => LoadLogs();
        partial void OnToDateChanged(DateTime? value) => LoadLogs();
        partial void OnSearchTextChanged(string value) => LoadLogs();

        partial void OnSelectedLogChanged(AuditLog? value)
        {
            if (value == null)
            {
                DiffResult = "Chọn một bản ghi để xem chi tiết thay đổi.";
                return;
            }

            DiffResult = GenerateDetailedResult(value);
        }

        private string GenerateDetailedResult(AuditLog? log)
        {
            if (log == null) return "Chọn một bản ghi để xem chi tiết.";

            try
            {
                var performerName = log.Performer?.FullName ?? "Hệ thống";
                var actionCode = (log.ActionCode ?? "UNKNOWN").ToUpperInvariant();
                
                string actionText = actionCode switch
                {
                    "CREATE" => "đã thêm mới",
                    "UPDATE" => "đã chỉnh sửa",
                    "DELETE" => "đã xoá",
                    "DEACTIVATE" => "đã dừng hoạt động",
                    "ACTIVATE" => "đã cho phép hoạt động",
                    "LOGIN" => "đã đăng nhập",
                    "LOGOUT" => "đã đăng xuất",
                    _ => $"đã thực hiện thao tác {actionCode} trên"
                };

                string entityNameText = log.EntityName switch
                {
                    "AppUser" => "tài khoản",
                    "Product" => "sản phẩm",
                    "Category" => "danh mục",
                    "Customer" => "khách hàng",
                    "Supplier" => "nhà cung cấp",
                    "Brand" => "thương hiệu",
                    "Warehouse" => "kho",
                    "Unit" => "đơn vị tính",
                    "PurchaseInvoice" => "đơn nhập hàng",
                    "SalesInvoice" => "đơn bán hàng",
                    "StockAdjustment" => "phiếu kiểm kho",
                    _ => log.EntityName ?? "đối tượng"
                };

                // Try to find a descriptive name for the target entity
                string targetName = "không xác định";
                if (actionCode == "CREATE" || actionCode == "UPDATE" || actionCode == "ACTIVATE" || actionCode == "DEACTIVATE")
                {
                    targetName = ExtractNameFromJson(log.AfterJson);
                    if (targetName == "không xác định") targetName = ExtractNameFromJson(log.BeforeJson);
                }
                else if (actionCode == "DELETE")
                {
                    targetName = ExtractNameFromJson(log.BeforeJson);
                }

                var sb = new StringBuilder();
                sb.AppendLine($"{performerName} {actionText} {entityNameText}: {targetName}");
                sb.AppendLine();
                sb.AppendLine("--- CHI TIẾT ---");
                sb.Append(GenerateDiff(log.BeforeJson, log.AfterJson));

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Lỗi khi hiển thị chi tiết: {ex.Message}";
            }
        }

        private string ExtractNameFromJson(string? json)
        {
            if (string.IsNullOrEmpty(json)) return "không xác định";
            try
            {
                using var doc = JsonDocument.Parse(json);
                // List of common name properties in order of preference
                string[] possibleKeys = { "Username", "DisplayName", "FullName", "CategoryName", "CustomerName", "SupplierName", "BrandName", "WarehouseName", "ProductCode", "Code" };
                
                foreach (var key in possibleKeys)
                {
                    if (doc.RootElement.TryGetProperty(key, out var prop))
                    {
                        var val = prop.GetString();
                        if (!string.IsNullOrWhiteSpace(val)) return val;
                    }
                }
            }
            catch { }
            return "không xác định";
        }

        private string GenerateDiff(string? before, string? after)
        {
            if (string.IsNullOrEmpty(before) && string.IsNullOrEmpty(after)) return "Không có dữ liệu chi tiết.";
            if (string.IsNullOrEmpty(before)) return $"[THÊM MỚI]\n{FormatJson(after)}";
            if (string.IsNullOrEmpty(after)) return $"[XÓA BỎ]\n{FormatJson(before)}";

            try
            {
                var beforeDict = JsonSerializer.Deserialize<Dictionary<string, object>>(before!);
                var afterDict = JsonSerializer.Deserialize<Dictionary<string, object>>(after!);

                if (beforeDict == null || afterDict == null) return "Lỗi phân tích dữ liệu JSON.";

                var changes = new List<string>();
                var allKeys = beforeDict.Keys.Union(afterDict.Keys);

                foreach (var key in allKeys)
                {
                    beforeDict.TryGetValue(key, out var oldVal);
                    afterDict.TryGetValue(key, out var newVal);

                    if (!Equals(oldVal?.ToString(), newVal?.ToString()))
                    {
                        changes.Add($"{key}: {oldVal ?? "NULL"} -> {newVal ?? "NULL"}");
                    }
                }

                return changes.Count > 0 ? string.Join("\n", changes) : "Không có thay đổi dữ liệu giữa hai phiên bản.";
            }
            catch
            {
                return "Lỗi khi so sánh dữ liệu JSON.";
            }
        }

        private string FormatJson(string? json)
        {
            if (string.IsNullOrEmpty(json)) return string.Empty;
            try
            {
                var obj = JsonSerializer.Deserialize<object>(json);
                return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
            }
            catch { return json; }
        }
    }
}
