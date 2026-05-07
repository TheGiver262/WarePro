using System;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class OpeningBalanceImportViewModel : ObservableObject
    {
        private readonly int _postedByUserId;
        private readonly Func<string, int, ImportResult<OpeningBalanceImportRow>> _importer;
        private readonly Action<string, string> _showMessage;

        [ObservableProperty] private string _filePath = string.Empty;
        [ObservableProperty] private ObservableCollection<RowError> _errors = new();
        [ObservableProperty] private string _statusMessage = string.Empty;

        public OpeningBalanceImportViewModel(int postedByUserId, Func<Data.AppDbContext> contextFactory)
            : this(
                postedByUserId,
                new OpeningBalanceImportService(contextFactory).ImportFile,
                (message, title) => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information))
        {
        }

        public OpeningBalanceImportViewModel(
            int postedByUserId,
            Func<string, int, ImportResult<OpeningBalanceImportRow>> importer,
            Action<string, string> showMessage)
        {
            _postedByUserId = postedByUserId;
            _importer = importer;
            _showMessage = showMessage;
        }

        [RelayCommand]
        private void BrowseFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Excel Files|*.xlsx;*.xls|CSV Files|*.csv|All Files|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                FilePath = dialog.FileName;
            }
        }

        [RelayCommand]
        private void ImportOpeningBalance()
        {
            if (string.IsNullOrWhiteSpace(FilePath))
            {
                StatusMessage = "Chua chon file import.";
                _showMessage(StatusMessage, "Canh bao");
                return;
            }

            try
            {
                var result = _importer(FilePath, _postedByUserId);
                Errors = new ObservableCollection<RowError>(result.Errors);
                StatusMessage = $"Da import {result.SuccessCount} dong ton dau ky.";
                _showMessage(StatusMessage, "Thong bao");
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
                _showMessage(ex.Message, "Loi import");
            }
        }
    }
}
