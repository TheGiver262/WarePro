using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace QuanLyHangHoa.ViewModels
{
    public partial class ReportViewModel : ObservableObject
    {
        [ObservableProperty] private DateTime _fromDate = DateTime.Now.AddDays(-30);
        [ObservableProperty] private DateTime _toDate = DateTime.Now;
        [ObservableProperty] private decimal _totalRevenue = 0;
        [ObservableProperty] private decimal _totalProfit = 0;
        [ObservableProperty] private decimal _totalCost = 0;
        [ObservableProperty] private ObservableCollection<DailyReportItem> _dailyReports = new();

        public ReportViewModel()
        {
            Refresh();
        }

        [RelayCommand]
        private void Refresh()
        {
            // Placeholder for data calculation logic
            TotalRevenue = 0;
            TotalProfit = 0;
            TotalCost = 0;
            DailyReports.Clear();
        }
    }

    public class DailyReportItem
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal Profit => Revenue - Cost;
    }
}
