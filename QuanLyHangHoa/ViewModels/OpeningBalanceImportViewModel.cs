using System;
using System.Collections.Generic;
using System.Globalization;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Services.DataImport;
using System.Threading.Tasks;

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
        private readonly OpeningBalanceImportService _openingBalanceImportService;
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
            _openingBalanceImportService = new OpeningBalanceImportService(contextFactory);
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

        // đọc file một lần và giữ raw header/row xuyên suốt các bước mapping, preview và confirm
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

        // tạo một dòng ánh xạ cho mỗi field đích và thử ghép header tốt nhất để người dùng chỉ sửa trường hợp sai
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

        // ưu tiên khớp chuẩn hóa chính xác, chỉ dùng contains khi không có kết quả rõ ràng
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
        // không cho sang preview nếu bất kỳ field bắt buộc nào chưa có cột nguồn
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

        // chỉ dựng 5 dòng đầu để xem nhanh; dữ liệu import thật vẫn dùng toàn bộ _rawRows
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
        // StockIn đi qua OpeningBalanceImportService để áp invariant tồn đầu kỳ; loại khác dùng pipeline dynamic
        private async Task ConfirmImport()
        {
            if (!_rawRows.Any())
            {
                _showMessage("Không có dữ liệu để import.", "Cảnh báo");
                return;
            }

            try
            {
                var mappingsDict = ColumnMappings.ToDictionary(mapping => mapping.DbFieldKey, mapping => mapping.ExcelHeader);
                var importType = SelectedImportTypeItem?.Value ?? ImportFileType.Product;
                var operationId = Guid.NewGuid();

                if (importType == ImportFileType.StockIn)
                {
                    var openingRows = BuildOpeningBalanceRows(mappingsDict);
                    var openingResult = await _openingBalanceImportService.ImportRowsAsync(openingRows, _postedByUserId, operationId);
                    SuccessCount = openingResult.SuccessCount;
                    Errors = new ObservableCollection<RowError>(openingResult.Errors);
                }
                else
                {
                    var dynamicResult = await _importService.ExecuteImportAsync(
                        _rawRows,
                        importType,
                        mappingsDict,
                        _postedByUserId,
                        AutoCreateReferences,
                        operationId);
                    SuccessCount = dynamicResult.SuccessCount;
                    Errors = new ObservableCollection<RowError>(dynamicResult.Errors);
                }

                StatusMessage = $"Import thành công {SuccessCount} dòng. Thất bại {Errors.Count} dòng.";

                ActiveStep = 4;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Lỗi import: {ex.Message}";
                _showMessage(StatusMessage, "Lỗi");
            }
        }

        // resolve mã sản phẩm và parse số lượng trước khi gửi cả batch cho service atomic
        private List<OpeningBalanceImportRow> BuildOpeningBalanceRows(
            IReadOnlyDictionary<string, string> mappings)
        {
            using var db = _contextFactory();
            var result = new List<OpeningBalanceImportRow>();
            var rowNumber = 1;

            foreach (var rawRow in _rawRows)
            {
                rowNumber++;
                var productCode = GetMappedValue(rawRow, mappings, "ProductCode", required: true);
                var product = db.Products.SingleOrDefault(item =>
                    item.ProductCode == productCode && item.IsActive)
                    ?? throw new InvalidOperationException($"Không tìm thấy sản phẩm '{productCode}'.");
                var quantityText = GetMappedValue(rawRow, mappings, "Quantity", required: true);
                if (!decimal.TryParse(
                        quantityText,
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out var quantity) &&
                    !decimal.TryParse(quantityText, out quantity))
                {
                    throw new InvalidOperationException($"Số lượng '{quantityText}' không hợp lệ ở dòng {rowNumber}.");
                }

                result.Add(new OpeningBalanceImportRow
                {
                    RowNumber = rowNumber,
                    ProductId = product.Id,
                    Quantity = quantity,
                    SerialNumbers = GetMappedValue(rawRow, mappings, "SerialNumbers", required: false)
                });
            }

            return result;
        }

        // một helper duy nhất xử lý cột không map, ô trống và trường bắt buộc để báo lỗi nhất quán
        private static string GetMappedValue(
            IReadOnlyDictionary<string, string> rawRow,
            IReadOnlyDictionary<string, string> mappings,
            string field,
            bool required)
        {
            var value = mappings.TryGetValue(field, out var header) &&
                        !string.IsNullOrWhiteSpace(header) &&
                        rawRow.TryGetValue(header, out var mappedValue)
                ? mappedValue?.Trim() ?? string.Empty
                : string.Empty;
            if (required && string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Thiếu dữ liệu bắt buộc: {field}.");
            }

            return value;
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
