using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Services.DataImport;
using QuanLyHangHoa.Views;
using ClosedXML.Excel;
using System.Windows;

namespace QuanLyHangHoa.ViewModels
{
    public partial class CustomerViewModel : ObservableObject
    {
        private readonly ReferenceDataService _service;
        [ObservableProperty] private ObservableCollection<Customer> _customers = new();
        [ObservableProperty] private Customer? _selectedCustomer;
        [ObservableProperty] private string _searchCode = string.Empty;
        [ObservableProperty] private string _searchName = string.Empty;
        [ObservableProperty] private string _searchPhone = string.Empty;

        [ObservableProperty]
        private string _displayName = string.Empty;

        [ObservableProperty]
        private string _customerCode = string.Empty;

        [ObservableProperty]
        private string _address = string.Empty;

        [ObservableProperty]
        private string _phone = string.Empty;

        [ObservableProperty]
        private string _email = string.Empty;

        public CustomerViewModel()
        {
            _service = new ReferenceDataService();
            LoadData();
        }

        private void LoadData()
        {
            var data = _service.GetAllCustomers();
            
            if (!string.IsNullOrWhiteSpace(SearchCode))
            {
                var lower = SearchCode.ToLower().Trim();
                data = data.Where(x => x.CustomerCode?.ToLower().Contains(lower) ?? false).ToList();
            }

            if (!string.IsNullOrWhiteSpace(SearchName))
            {
                var lower = SearchName.ToLower().Trim();
                data = data.Where(x => x.DisplayName?.ToLower().Contains(lower) ?? false).ToList();
            }

            if (!string.IsNullOrWhiteSpace(SearchPhone))
            {
                var lower = SearchPhone.ToLower().Trim();
                data = data.Where(x => x.Phone?.ToLower().Contains(lower) ?? false).ToList();
            }

            Customers = new ObservableCollection<Customer>(data);
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

                        // Headers
                        worksheet.Cell(1, 1).Value = "Mã Khách Hàng";
                        worksheet.Cell(1, 2).Value = "Tên Khách Hàng";
                        worksheet.Cell(1, 3).Value = "Số Điện Thoại";
                        worksheet.Cell(1, 4).Value = "Email";
                        worksheet.Cell(1, 5).Value = "Địa Chỉ";

                        var headerRange = worksheet.Range(1, 1, 1, 5);
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                        // Data
                        for (int i = 0; i < Customers.Count; i++)
                        {
                            worksheet.Cell(i + 2, 1).Value = Customers[i].CustomerCode;
                            worksheet.Cell(i + 2, 2).Value = Customers[i].DisplayName;
                            worksheet.Cell(i + 2, 3).Value = Customers[i].Phone;
                            worksheet.Cell(i + 2, 4).Value = Customers[i].Email;
                            worksheet.Cell(i + 2, 5).Value = Customers[i].Address;
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

        partial void OnSearchCodeChanged(string value) => LoadData();
        partial void OnSearchNameChanged(string value) => LoadData();
        partial void OnSearchPhoneChanged(string value) => LoadData();

        [RelayCommand]
        private void Save()
        {
            if (string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrWhiteSpace(CustomerCode)) return;

            if (SelectedCustomer == null)
            {
                _service.AddCustomer(new Customer 
                { 
                    DisplayName = DisplayName, 
                    CustomerCode = CustomerCode,
                    Address = Address,
                    Phone = Phone,
                    Email = Email
                });
            }
            else
            {
                SelectedCustomer.DisplayName = DisplayName;
                SelectedCustomer.CustomerCode = CustomerCode;
                SelectedCustomer.Address = Address;
                SelectedCustomer.Phone = Phone;
                SelectedCustomer.Email = Email;
                _service.UpdateCustomer(SelectedCustomer);
            }
            LoadData();
            Clear();
        }

        [RelayCommand]
        private void Delete()
        {
            if (SelectedCustomer != null)
            {
                _service.DeactivateCustomer(SelectedCustomer.Id);
                LoadData();
                Clear();
            }
        }


        [RelayCommand]
        private void Clear()
        {
            SelectedCustomer = null;
            DisplayName = string.Empty;
            CustomerCode = string.Empty;
            Address = string.Empty;
            Phone = string.Empty;
            Email = string.Empty;
        }

        partial void OnSelectedCustomerChanged(Customer? value)
        {
            if (value != null)
            {
                DisplayName = value.DisplayName;
                CustomerCode = value.CustomerCode;
                Address = value.Address ?? string.Empty;
                Phone = value.Phone ?? string.Empty;
                Email = value.Email ?? string.Empty;
            }
        }
    }
}
