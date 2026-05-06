using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Views;
using QuanLyHangHoa.Data;
using System;
using System.Windows;
using ClosedXML.Excel;
using System.Text.Json;

namespace QuanLyHangHoa.ViewModels
{
    public partial class CustomerViewModel : ObservableObject
    {
        private readonly AppDbContext _db;
        private readonly AppUser _currentUser;

        [ObservableProperty] private ObservableCollection<Customer> _customers = new();
        [ObservableProperty] private Customer? _selectedCustomer;

        // Search Filters
        [ObservableProperty] private string _searchCode = string.Empty;
        [ObservableProperty] private string _searchName = string.Empty;
        [ObservableProperty] private string _searchEmail = string.Empty;
        [ObservableProperty] private string _searchPhone = string.Empty;
        [ObservableProperty] private string? _searchStatus = "Tất cả";
        public ObservableCollection<string> StatusOptions { get; } = ["Tất cả", "Hoạt động", "Ngưng"];

        public CustomerViewModel(AppDbContext db, AppUser currentUser)
        {
            _db = db;
            _currentUser = currentUser;
            LoadData();
        }

        [RelayCommand]
        public void LoadData()
        {
            var query = _db.Customers.AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchCode))
                query = query.Where(c => c.CustomerCode.Contains(SearchCode));

            if (!string.IsNullOrWhiteSpace(SearchName))
                query = query.Where(c => c.DisplayName.Contains(SearchName));

            if (!string.IsNullOrWhiteSpace(SearchEmail))
                query = query.Where(c => c.Email != null && c.Email.Contains(SearchEmail));

            if (!string.IsNullOrWhiteSpace(SearchPhone))
                query = query.Where(c => c.Phone != null && c.Phone.Contains(SearchPhone));

            if (SearchStatus == "Hoạt động")
                query = query.Where(c => c.IsActive);
            else if (SearchStatus == "Ngưng")
                query = query.Where(c => !c.IsActive);

            var list = query.OrderBy(c => c.CustomerCode).ToList();
            Customers = new ObservableCollection<Customer>(list);
        }

        partial void OnSearchCodeChanged(string value) => LoadData();
        partial void OnSearchNameChanged(string value) => LoadData();
        partial void OnSearchEmailChanged(string value) => LoadData();
        partial void OnSearchPhoneChanged(string value) => LoadData();
        partial void OnSearchStatusChanged(string? value) => LoadData();

        [RelayCommand]
        private void OpenAddCustomerDialog()
        {
            var vm = new CustomerEditViewModel();
            var window = new CustomerEditWindow { DataContext = vm };
            if (window.ShowDialog() == true)
            {
                if (_db.Customers.Any(c => c.CustomerCode == vm.CustomerCode))
                {
                    MessageBox.Show($"Mã khách hàng '{vm.CustomerCode}' đã tồn tại.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var newCust = new Customer();
                vm.ApplyTo(newCust);
                _db.Customers.Add(newCust);
                _db.SaveChanges();

                LogAction("CREATE", newCust.Id, null, Serialize(newCust));
                LoadData();
            }
        }

        [RelayCommand]
        private void EditCustomer(Customer customer)
        {
            var beforeJson = Serialize(customer);
            var vm = new CustomerEditViewModel(customer);
            var window = new CustomerEditWindow { DataContext = vm };
            if (window.ShowDialog() == true)
            {
                vm.ApplyTo(customer);
                _db.SaveChanges();

                LogAction("UPDATE", customer.Id, beforeJson, Serialize(customer));
                LoadData();
            }
        }

        [RelayCommand]
        private void DeleteCustomer(Customer customer)
        {
            var result = MessageBox.Show($"Bạn có chắc chắn muốn xoá khách hàng '{customer.DisplayName}'?", "Xác nhận xoá", 
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                try 
                {
                    // Check for dependencies
                    bool isUsed = _db.SalesInvoices.Any(si => si.CustomerId == customer.Id) ||
                                 _db.StockOuts.Any(so => so.CustomerId == customer.Id) ||
                                 _db.WarrantyCoverages.Any(wc => wc.CustomerId == customer.Id);

                    if (isUsed)
                    {
                        MessageBox.Show("Không thể xoá khách hàng này vì đang có dữ liệu liên quan (Hóa đơn bán, Phiếu xuất kho hoặc Bảo hành).", 
                            "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var beforeJson = Serialize(customer);
                    int entityId = customer.Id;

                    _db.Customers.Remove(customer);
                    _db.SaveChanges();

                    LogAction("DELETE", entityId, beforeJson, null);
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi xoá khách hàng: {ex.Message}", 
                        "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void ExportToExcel()
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                    FileName = $"DanhSachKhachHang_{DateTime.Now:yyyyMMdd_HHmm}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("Customers");
                        worksheet.Cell(1, 1).Value = "Mã Khách Hàng";
                        worksheet.Cell(1, 2).Value = "Tên Khách Hàng";
                        worksheet.Cell(1, 3).Value = "Số Điện Thoại";
                        worksheet.Cell(1, 4).Value = "Email";
                        worksheet.Cell(1, 5).Value = "Địa Chỉ";
                        worksheet.Cell(1, 6).Value = "Trạng Thái";

                        var headerRange = worksheet.Range(1, 1, 1, 6);
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                        for (int i = 0; i < Customers.Count; i++)
                        {
                            worksheet.Cell(i + 2, 1).Value = Customers[i].CustomerCode;
                            worksheet.Cell(i + 2, 2).Value = Customers[i].DisplayName;
                            worksheet.Cell(i + 2, 3).Value = Customers[i].Phone;
                            worksheet.Cell(i + 2, 4).Value = Customers[i].Email;
                            worksheet.Cell(i + 2, 5).Value = Customers[i].Address;
                            worksheet.Cell(i + 2, 6).Value = Customers[i].IsActive ? "Hoạt động" : "Ngưng";
                        }

                        worksheet.Columns().AdjustToContents();
                        workbook.SaveAs(saveFileDialog.FileName);
                    }
                    MessageBox.Show("Xuất file Excel thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xuất Excel: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string Serialize(Customer c)
        {
            return JsonSerializer.Serialize(new { c.Id, c.CustomerCode, c.DisplayName, c.Phone, c.Email, c.Address, c.IsActive });
        }

        private void LogAction(string action, int entityId, string? before = null, string? after = null)
        {
            try
            {
                _db.AuditLogs.Add(new AuditLog
                {
                    EntityName = "Customer",
                    EntityId = entityId,
                    ActionCode = action,
                    BeforeJson = before,
                    AfterJson = after,
                    PerformedBy = _currentUser.Id,
                    PerformedAt = DateTime.Now
                });
                _db.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to write audit log: {ex.Message}");
            }
        }
    }
}
