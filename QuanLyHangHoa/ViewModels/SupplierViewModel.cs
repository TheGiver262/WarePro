using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Services.DataImport;
using QuanLyHangHoa.Views;

namespace QuanLyHangHoa.ViewModels
{
    public partial class SupplierViewModel : ObservableObject
    {
        private readonly ReferenceDataService _svc = new();
        private readonly DataImportManager _importManager = new();

        [ObservableProperty] private ObservableCollection<Supplier> _suppliers = new();
        [ObservableProperty] private Supplier? _selectedSupplier;
        [ObservableProperty] private string _editName    = string.Empty;
        [ObservableProperty] private string _editAddress = string.Empty;
        [ObservableProperty] private string _editPhone   = string.Empty;
        [ObservableProperty] private string _statusMessage = string.Empty;

        public SupplierViewModel() => LoadData();
        private void LoadData() => Suppliers = new ObservableCollection<Supplier>(_svc.GetAllSuppliers());

        [RelayCommand]
        private void Add()
        {
            if (string.IsNullOrWhiteSpace(EditName)) { StatusMessage = "Tên không được trống!"; return; }
            _svc.AddSupplier(new Supplier { Name = EditName.Trim(), Address = EditAddress, Phone = EditPhone });
            ClearInputs(); LoadData(); StatusMessage = "Thêm thành công.";
        }
        [RelayCommand]
        private void SaveEdit()
        {
            if (SelectedSupplier == null) { StatusMessage = "Chưa chọn mục!"; return; }
            SelectedSupplier.Name = EditName.Trim(); SelectedSupplier.Address = EditAddress; SelectedSupplier.Phone = EditPhone;
            _svc.UpdateSupplier(SelectedSupplier); LoadData(); StatusMessage = "Cập nhật thành công.";
        }
        [RelayCommand]
        private void Delete()
        {
            if (SelectedSupplier == null) { StatusMessage = "Chưa chọn mục!"; return; }
            _svc.DeleteSupplier(SelectedSupplier.Id); LoadData(); StatusMessage = "Đã xoá.";
        }

        [RelayCommand]
        private void ImportData()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel Files|*.xlsx;*.xls|CSV Files|*.csv|All Files|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var result = _importManager.ProcessFile<Supplier>(dialog.FileName);
                    LoadData();
                    var reportWin = new ImportResultWindow(result.SuccessCount, result.Errors);
                    reportWin.ShowDialog();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(ex.Message, "Lỗi Import", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void ClearInputs() { EditName = string.Empty; EditAddress = string.Empty; EditPhone = string.Empty; }

        partial void OnSelectedSupplierChanged(Supplier? value)
        {
            EditName    = value?.Name    ?? string.Empty;
            EditAddress = value?.Address ?? string.Empty;
            EditPhone   = value?.Phone   ?? string.Empty;
        }
    }
}
