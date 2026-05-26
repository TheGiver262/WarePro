using System.Collections.ObjectModel;
using System.Collections.Generic;
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
using QuanLyHangHoa.Services;
using Microsoft.EntityFrameworkCore;

namespace QuanLyHangHoa.ViewModels
{
    public partial class CustomerViewModel : ObservableObject
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly CustomerService _service;
        private readonly AppUser _currentUser;


        [ObservableProperty] private bool _canManage;
        [ObservableProperty] private ObservableCollection<Customer> _customers = new();
        [ObservableProperty] private Customer? _selectedCustomer;

        // Footer counts
        [ObservableProperty] private int _totalCount;
        [ObservableProperty] private int _activeCount;
        [ObservableProperty] private int _inactiveCount;

        // Search Filters
        [ObservableProperty] private string _searchCode = string.Empty;
        [ObservableProperty] private string _searchName = string.Empty;
        [ObservableProperty] private string _searchEmail = string.Empty;
        [ObservableProperty] private string _searchPhone = string.Empty;
        [ObservableProperty] private string? _searchStatus = "Tất cả";
        public ObservableCollection<string> StatusOptions { get; } = ["Tất cả", "Hoạt động", "Dừng"];

        public CustomerViewModel(Func<AppDbContext> contextFactory, AppUser currentUser)
        {
            _contextFactory = contextFactory;
            _service = new CustomerService(_contextFactory);
            _currentUser = currentUser;
            CanManage = AuthorizationService.CanPerform(_currentUser, PermissionAction.ManageMasterData);
            LoadData();
        }

        private List<Customer> _allCustomers = new();

        [RelayCommand]
        public void LoadData()
        {
            using var db = _contextFactory();
            _allCustomers = db.Customers.AsNoTracking().Where(c => c.CustomerCode != "CUS-ADJ").ToList();

            // Calculate counts in memory (instant)
            TotalCount = _allCustomers.Count;
            ActiveCount = _allCustomers.Count(c => c.IsActive);
            InactiveCount = _allCustomers.Count(c => !c.IsActive);

            ApplyFilters();
        }

        private void ApplyFilters()
        {
            var query = _allCustomers.AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchCode))
                query = query.Where(c => c.CustomerCode.Contains(SearchCode, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(SearchName))
                query = query.Where(c => c.DisplayName.Contains(SearchName, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(SearchEmail))
                query = query.Where(c => c.Email != null && c.Email.Contains(SearchEmail, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(SearchPhone))
                query = query.Where(c => c.Phone != null && c.Phone.Contains(SearchPhone, StringComparison.OrdinalIgnoreCase));

            if (SearchStatus == "Hoạt động")
                query = query.Where(c => c.IsActive);
            else if (SearchStatus == "Dừng")
                query = query.Where(c => !c.IsActive);

            var list = query.OrderBy(c => c.CustomerCode).ToList();
            Customers = new ObservableCollection<Customer>(list);
        }

        [RelayCommand]
        private void Refresh()
        {
            SearchCode = string.Empty;
            SearchName = string.Empty;
            SearchEmail = string.Empty;
            SearchPhone = string.Empty;
            SearchStatus = "Tất cả";
            LoadData();
        }

        partial void OnSearchCodeChanged(string value) => ApplyFilters();
        partial void OnSearchNameChanged(string value) => ApplyFilters();
        partial void OnSearchEmailChanged(string value) => ApplyFilters();
        partial void OnSearchPhoneChanged(string value) => ApplyFilters();
        partial void OnSearchStatusChanged(string? value) => ApplyFilters();

        [RelayCommand(CanExecute = nameof(CanManage))]
        private void OpenAddCustomerDialog()
        {
            var vm = new CustomerEditViewModel();
            var window = new CustomerEditWindow { DataContext = vm };
            if (window.ShowDialog() == true)
            {
                var newCust = new Customer();
                vm.ApplyTo(newCust);
                _service.Add(newCust, _currentUser.Id);
                LoadData();
            }
        }

        [RelayCommand(CanExecute = nameof(CanManage))]
        private void EditCustomer(Customer customer)
        {
            var beforeJson = Serialize(customer);
            var vm = new CustomerEditViewModel(customer);
            var window = new CustomerEditWindow { DataContext = vm };
            if (window.ShowDialog() == true)
            {
                vm.ApplyTo(customer);
                _service.Update(customer, beforeJson, _currentUser.Id);
                LoadData();
            }
        }

        [RelayCommand(CanExecute = nameof(CanManage))]
        private void DeleteCustomer(Customer customer)
        {
            using var db = _contextFactory();
            // 1. Kiểm tra phát sinh dữ liệu
            bool isUsed = db.SalesInvoices.Any(si => si.CustomerId == customer.Id) ||
                         db.StockOuts.Any(so => so.CustomerId == customer.Id) ||
                         db.WarrantyCoverages.Any(wc => wc.CustomerId == customer.Id);

            if (isUsed)
            {
                MessageBox.Show($"Không thể xoá khách hàng '{customer.DisplayName}' vì đang có dữ liệu liên quan (Hóa đơn bán, Phiếu xuất kho hoặc Bảo hành).\n\nVui lòng chuyển trạng thái khách hàng sang 'Dừng' nếu không còn sử dụng.", 
                    "Không thể xoá", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }

            // 2. Xác nhận xoá (nếu không có ràng buộc)
            var result = MessageBox.Show($"Khách hàng '{customer.DisplayName}' chưa có dữ liệu liên quan. Bạn có chắc chắn muốn xoá vĩnh viễn khách hàng này?", 
                "Xác nhận xoá", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                try 
                {
                    _service.Delete(customer.Id, _currentUser.Id);
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi xoá khách hàng: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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
                            worksheet.Cell(i + 2, 6).Value = Customers[i].IsActive ? "Hoạt động" : "Dừng";
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
            return System.Text.Json.JsonSerializer.Serialize(new { c.Id, c.CustomerCode, c.DisplayName, c.Phone, c.Email, c.Address, c.IsActive });
        }
    }
}
