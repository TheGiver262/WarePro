using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class DebtReportViewModel : ObservableObject
    {
        private readonly DebtReportService _service;

        [ObservableProperty] private bool _isCustomerMode = true;
        [ObservableProperty] private ObservableCollection<DebtReportEntry> _summaries = new();
        [ObservableProperty] private decimal _totalDebt;

        public DebtReportViewModel()
        {
            _service = new DebtReportService();
            LoadCurrentReport();
        }

        public string ReportTitle => IsCustomerMode ? "Công nợ khách hàng" : "Công nợ nhà cung cấp";
        public string PartyColumnTitle => IsCustomerMode ? "Đối tác" : "Đối tác";

        [RelayCommand]
        private void ShowCustomers()
        {
            IsCustomerMode = true;
            LoadCurrentReport();
        }

        [RelayCommand]
        private void ShowSuppliers()
        {
            IsCustomerMode = false;
            LoadCurrentReport();
        }

        [RelayCommand]
        private void Refresh() => LoadCurrentReport();

        partial void OnIsCustomerModeChanged(bool value)
        {
            OnPropertyChanged(nameof(ReportTitle));
            OnPropertyChanged(nameof(PartyColumnTitle));
        }

        private void LoadCurrentReport()
        {
            var loaded = IsCustomerMode ? _service.GetCustomerDebtReport() : _service.GetSupplierDebtReport();
            Summaries = new ObservableCollection<DebtReportEntry>(loaded);
            TotalDebt = loaded.Sum(summary => summary.Balance);
        }
    }
}
