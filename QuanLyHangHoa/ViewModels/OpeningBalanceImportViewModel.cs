using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services.DataImport;
using System.Text.Json;

namespace QuanLyHangHoa.ViewModels
{
    public class ImportTypeItem
    {
        public ImportFileType Value { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }

    public partial class ColumnMappingItem : ObservableObject
    {
        public string DbFieldKey { get; set; } = null!;
        public string DbFieldName { get; set; } = null!;
        public bool IsRequired { get; set; }
        public string DataType { get; set; } = "string";

        [ObservableProperty]
        private string _excelHeader = string.Empty;
    }

    public partial class OpeningBalanceImportViewModel : ObservableObject
    {
        private readonly int _postedByUserId;
        private readonly Func<Data.AppDbContext> _contextFactory;
        private readonly FileClassificationService _classifier = new();
        private readonly DynamicImportService _importService;
        private readonly Action<string, string> _showMessage;

        private List<string> _rawHeaders = new();
        private List<Dictionary<string, string>> _rawRows = new();

        [ObservableProperty] private string _filePath = string.Empty;
        [ObservableProperty] private int _activeStep = 1; // 1: File, 2: Map, 3: Preview, 4: Result
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private int _successCount = 0;
        
        [ObservableProperty] private ObservableCollection<RowError> _errors = new();
        [ObservableProperty] private ObservableCollection<ColumnMappingItem> _columnMappings = new();
        [ObservableProperty] private ObservableCollection<string> _excelHeaders = new();
        [ObservableProperty] private DataTable? _previewData;

        // ComboBox helpers
        public List<ImportTypeItem> ImportTypes { get; } = Enum.GetValues(typeof(ImportFileType))
            .Cast<ImportFileType>()
            .Where(t => t != ImportFileType.Unknown)
            .Select(t => new ImportTypeItem { Value = t, DisplayName = FileClassificationService.GetTypeDisplayName(t) })
            .ToList();

        [ObservableProperty]
        private ImportTypeItem? _selectedImportTypeItem;

        partial void OnSelectedImportTypeItemChanged(ImportTypeItem? value)
        {
            if (value != null)
            {
                InitializeMappings(value.Value);
            }
        }

        public OpeningBalanceImportViewModel(int postedByUserId, Func<Data.AppDbContext> contextFactory)
            : this(
                postedByUserId,
                contextFactory,
                (message, title) => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information))
        {
        }

        public OpeningBalanceImportViewModel(
            int postedByUserId,
            Func<Data.AppDbContext> contextFactory,
            Action<string, string> showMessage)
        {
            _postedByUserId = postedByUserId;
            _contextFactory = contextFactory;
            _importService = new DynamicImportService(contextFactory);
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
                ProcessSelectedFile();
            }
        }

        private void ProcessSelectedFile()
        {
            if (string.IsNullOrWhiteSpace(FilePath)) return;

            try
            {
                var (headers, rows) = DynamicImportService.ReadFile(FilePath);
                _rawHeaders = headers;
                _rawRows = rows;

                ExcelHeaders = new ObservableCollection<string>(headers);
                ExcelHeaders.Insert(0, string.Empty); // Option to leave unmapped

                // Classify file type
                var predictedType = _classifier.Classify(headers);
                var targetType = predictedType != ImportFileType.Unknown ? predictedType : ImportFileType.Product;
                SelectedImportTypeItem = ImportTypes.FirstOrDefault(t => t.Value == targetType);

                if (predictedType != ImportFileType.Unknown)
                {
                    StatusMessage = $"Đã nhận diện file: {FileClassificationService.GetTypeDisplayName(predictedType)}";
                }
                else
                {
                    StatusMessage = "Không thể nhận diện tự động loại file. Vui lòng tự chọn danh mục bên dưới.";
                }

                InitializeMappings(targetType);
                ActiveStep = 2;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Lỗi đọc file: {ex.Message}";
                _showMessage(StatusMessage, "Lỗi");
            }
        }

        private void InitializeMappings(ImportFileType type)
        {
            var fields = DynamicImportService.GetFieldDefinitions(type);
            var mappings = new List<ColumnMappingItem>();

            foreach (var f in fields)
            {
                var bestMatch = FindBestHeaderMatch(f.DisplayName, f.Key, _rawHeaders);
                mappings.Add(new ColumnMappingItem
                {
                    DbFieldKey = f.Key,
                    DbFieldName = f.DisplayName,
                    IsRequired = f.IsRequired,
                    DataType = f.DataType,
                    ExcelHeader = bestMatch
                });
            }

            ColumnMappings = new ObservableCollection<ColumnMappingItem>(mappings);
            GeneratePreview();
        }

        private string FindBestHeaderMatch(string dbName, string dbKey, List<string> excelHeaders)
        {
            var normalizedDbName = dbName.ToLowerInvariant().Replace(" ", "").Replace("/", "").Replace("-", "");
            var normalizedDbKey = dbKey.ToLowerInvariant();

            // 1. Exact match
            foreach (var h in excelHeaders)
            {
                var normH = h.ToLowerInvariant().Replace(" ", "").Replace("/", "").Replace("-", "").Replace("_", "");
                if (normH == normalizedDbName || normH == normalizedDbKey) return h;
            }

            // 2. Fuzzy contains match
            foreach (var h in excelHeaders)
            {
                var normH = h.ToLowerInvariant().Replace(" ", "").Replace("/", "").Replace("-", "").Replace("_", "");
                if (normH.Contains(normalizedDbName) || normalizedDbName.Contains(normH) ||
                    normH.Contains(normalizedDbKey) || normalizedDbKey.Contains(normH))
                {
                    return h;
                }
            }

            return string.Empty;
        }

        [RelayCommand]
        private void NextToPreview()
        {
            // Validate that required fields are mapped
            var unmappedRequired = ColumnMappings.Where(m => m.IsRequired && string.IsNullOrWhiteSpace(m.ExcelHeader)).ToList();
            if (unmappedRequired.Any())
            {
                string names = string.Join(", ", unmappedRequired.Select(m => m.DbFieldName));
                _showMessage($"Vui lòng ánh xạ các cột bắt buộc: {names}", "Cảnh báo");
                return;
            }

            GeneratePreview();
            ActiveStep = 3;
        }

        [RelayCommand]
        private void BackToMapping()
        {
            ActiveStep = 2;
        }

        [RelayCommand]
        private void BackToFileSelect()
        {
            ActiveStep = 1;
        }

        private void GeneratePreview()
        {
            var dt = new DataTable();
            foreach (var mapping in ColumnMappings)
            {
                dt.Columns.Add(mapping.DbFieldName);
            }

            // Take first 5 rows for preview
            foreach (var rawRow in _rawRows.Take(5))
            {
                var dr = dt.NewRow();
                foreach (var mapping in ColumnMappings)
                {
                    if (!string.IsNullOrEmpty(mapping.ExcelHeader) && rawRow.TryGetValue(mapping.ExcelHeader, out string? val))
                    {
                        dr[mapping.DbFieldName] = val;
                    }
                    else
                    {
                        dr[mapping.DbFieldName] = DBNull.Value;
                    }
                }
                dt.Rows.Add(dr);
            }

            PreviewData = dt;
        }

        [ObservableProperty] private bool _autoCreateReferences = true;

        [RelayCommand]
        private void ConfirmImport()
        {
            if (!_rawRows.Any())
            {
                _showMessage("Không có dữ liệu để import.", "Cảnh báo");
                return;
            }

            try
            {
                // Create mapping dict
                var mappingsDict = ColumnMappings.ToDictionary(m => m.DbFieldKey, m => m.ExcelHeader);

                // Use the logged-in user ID
                var importType = SelectedImportTypeItem?.Value ?? ImportFileType.Product;
                var result = _importService.ExecuteImport(_rawRows, importType, mappingsDict, _postedByUserId, AutoCreateReferences);

                SuccessCount = result.SuccessCount;
                Errors = new ObservableCollection<RowError>(result.Errors);
                
                StatusMessage = $"Import thành công {SuccessCount} dòng. Thất bại {Errors.Count} dòng.";

                // Ghi audit log cho nhập tồn đầu kỳ
                if (SuccessCount > 0)
                {
                    try
                    {
                        using var db = _contextFactory();
                        var importTypeName = SelectedImportTypeItem?.DisplayName ?? "Sản phẩm";
                        var fileName = System.IO.Path.GetFileName(FilePath);
                        var detailLog = new
                        {
                            ImportType = importType.ToString(),
                            ImportTypeDisplayName = importTypeName,
                            FileName = fileName,
                            SuccessCount = SuccessCount,
                            ErrorCount = Errors.Count,
                            AutoCreateReferences = AutoCreateReferences
                        };
                        
                        db.AuditLogs.Add(new AuditLog
                        {
                            EntityName = "OpeningBalanceImport",
                            EntityId = 0,
                            ActionCode = "IMPORT",
                            PerformedBy = _postedByUserId,
                            PerformedAt = DateTime.Now,
                            BeforeJson = null,
                            AfterJson = JsonSerializer.Serialize(detailLog)
                        });
                        db.SaveChanges();
                    }
                    catch (Exception auditEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to write audit log: {auditEx.Message}");
                    }
                }

                ActiveStep = 4;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Lỗi import: {ex.Message}";
                _showMessage(StatusMessage, "Lỗi");
            }
        }

        [RelayCommand]
        private void ShowInstruction()
        {
            var window = new Views.ImportInstructionWindow();
            window.ShowDialog();
        }

        [RelayCommand]
        private void ResetWizard()
        {
            FilePath = string.Empty;
            _rawHeaders.Clear();
            _rawRows.Clear();
            ExcelHeaders.Clear();
            ColumnMappings.Clear();
            PreviewData = null;
            Errors.Clear();
            SuccessCount = 0;
            StatusMessage = string.Empty;
            ActiveStep = 1;
        }
    }
}
