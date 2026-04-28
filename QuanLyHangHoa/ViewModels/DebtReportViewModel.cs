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
        private readonly Func<IReadOnlyList<DebtSummary>> _customerDebtLoader;
        private readonly Func<IReadOnlyList<DebtSummary>> _supplierDebtLoader;

        [ObservableProperty] private bool _isCustomerMode = true;
        [ObservableProperty] private ObservableCollection<DebtSummary> _summaries = new();
        [ObservableProperty] private decimal _totalDebt;

        public DebtReportViewModel()
            : this(
                new DebtReportService().GetCustomerDebtSummary,
                new DebtReportService().GetSupplierDebtSummary)
        {
        }

        public DebtReportViewModel(
            Func<IReadOnlyList<DebtSummary>> customerDebtLoader,
            Func<IReadOnlyList<DebtSummary>> supplierDebtLoader)
        {
            _customerDebtLoader = customerDebtLoader;
            _supplierDebtLoader = supplierDebtLoader;
            LoadCurrentReport();
        }

        public string ReportTitle => IsCustomerMode ? "Cong no khach hang" : "Cong no nha cung cap";
        public string PartyColumnTitle => IsCustomerMode ? "Khach hang" : "Nha cung cap";

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
            var loaded = IsCustomerMode ? _customerDebtLoader() : _supplierDebtLoader();
            Summaries = new ObservableCollection<DebtSummary>(loaded);
            TotalDebt = loaded.Sum(summary => summary.DebtAmount);
        }
    }
}
