using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Services.DataImport;
using QuanLyHangHoa.Views;
using ClosedXML.Excel;
using System.Windows;
using System.Linq;

namespace QuanLyHangHoa.ViewModels
{
    public partial class SupplierViewModel : ObservableObject
    {
        private readonly ReferenceDataService _svc = new();

        [ObservableProperty] private ObservableCollection<Supplier> _suppliers = new();
        [ObservableProperty] private Supplier? _selectedSupplier;
        [ObservableProperty] private string _editCode    = string.Empty;
        [ObservableProperty] private string _editName    = string.Empty;
        [ObservableProperty] private string _editAddress = string.Empty;
        [ObservableProperty] private string _editPhone   = string.Empty;
        [ObservableProperty] private string _editEmail   = string.Empty;
        [ObservableProperty] private string _searchCode = string.Empty;
        [ObservableProperty] private string _searchName = string.Empty;
        [ObservableProperty] private string _searchPhone = string.Empty;
        [ObservableProperty] private string _statusMessage = string.Empty;

        public SupplierViewModel() => LoadData();

        [RelayCommand]
        private void LoadData()
        {
            var data = _svc.GetAllSuppliers();
            
            if (!string.IsNullOrWhiteSpace(SearchCode))
            {
                var lower = SearchCode.ToLower().Trim();
                data = data.Where(x => x.SupplierCode?.ToLower().Contains(lower) ?? false).ToList();
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

            Suppliers = new ObservableCollection<Supplier>(data);
        }

        [RelayCommand]
        private void ExportToExcel()
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                    FileName = $"DanhSachNhaCungCap_{DateTime.Now:yyyyMMdd_HHmm}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("Suppliers");

                        // Headers
                        worksheet.Cell(1, 1).Value = "Mã Nhà Cung Cấp";
                        worksheet.Cell(1, 2).Value = "Tên Nhà Cung Cấp";
                        worksheet.Cell(1, 3).Value = "Số Điện Thoại";
                        worksheet.Cell(1, 4).Value = "Email";
                        worksheet.Cell(1, 5).Value = "Địa Chỉ";

                        var headerRange = worksheet.Range(1, 1, 1, 5);
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                        // Data
                        for (int i = 0; i < Suppliers.Count; i++)
                        {
                            worksheet.Cell(i + 2, 1).Value = Suppliers[i].SupplierCode;
                            worksheet.Cell(i + 2, 2).Value = Suppliers[i].DisplayName;
                            worksheet.Cell(i + 2, 3).Value = Suppliers[i].Phone;
                            worksheet.Cell(i + 2, 4).Value = Suppliers[i].Email;
                            worksheet.Cell(i + 2, 5).Value = Suppliers[i].Address;
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
        private void Add()
        {
            if (string.IsNullOrWhiteSpace(EditName)) { StatusMessage = "Tên không được trống!"; return; }
            if (string.IsNullOrWhiteSpace(EditCode)) { StatusMessage = "Mã không được trống!"; return; }
            _svc.AddSupplier(new Supplier { SupplierCode = EditCode.Trim(), DisplayName = EditName.Trim(), Address = EditAddress, Phone = EditPhone, Email = EditEmail });
            ClearInputs(); LoadData(); StatusMessage = "Thêm thành công.";
        }
        [RelayCommand]
        private void SaveEdit()
        {
            if (SelectedSupplier == null) { StatusMessage = "Chưa chọn mục!"; return; }
            SelectedSupplier.SupplierCode = EditCode.Trim();
            SelectedSupplier.DisplayName = EditName.Trim(); 
            SelectedSupplier.Address = EditAddress; 
            SelectedSupplier.Phone = EditPhone;
            SelectedSupplier.Email = EditEmail;
            _svc.UpdateSupplier(SelectedSupplier); LoadData(); StatusMessage = "Cập nhật thành công.";
        }
        [RelayCommand]
        private void Delete()
        {
            if (SelectedSupplier == null) { StatusMessage = "Chưa chọn mục!"; return; }
            _svc.DeactivateSupplier(SelectedSupplier.Id); LoadData(); StatusMessage = "Đã xoá.";
        }


        [RelayCommand]
        private void ClearInput() { EditCode = string.Empty; EditName = string.Empty; EditAddress = string.Empty; EditPhone = string.Empty; EditEmail = string.Empty; SelectedSupplier = null; }

        private void ClearInputs() { EditCode = string.Empty; EditName = string.Empty; EditAddress = string.Empty; EditPhone = string.Empty; EditEmail = string.Empty; }

        partial void OnSelectedSupplierChanged(Supplier? value)
        {
            EditCode    = value?.SupplierCode ?? string.Empty;
            EditName    = value?.DisplayName    ?? string.Empty;
            EditAddress = value?.Address ?? string.Empty;
            EditPhone   = value?.Phone   ?? string.Empty;
            EditEmail   = value?.Email   ?? string.Empty;
        }
    }
}
