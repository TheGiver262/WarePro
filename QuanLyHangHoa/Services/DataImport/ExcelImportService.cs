using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ClosedXML.Excel;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services.DataImport
{
    public class ExcelImportService
    {
        // cột được ghép với property không phân biệt hoa thường; reflection giúp một bộ đọc dùng cho nhiều model
        public ImportResult<T> Import<T>(string filePath) where T : new()
        {
            var result = new ImportResult<T>();
            try
            {
                using var workbook = new XLWorkbook(filePath);
                var worksheet = workbook.Worksheet(1);
                var usedRange = worksheet.RangeUsed();
                if (usedRange == null)
                {
                    return result;
                }

                // bỏ dòng tiêu đề, chỉ tạo object từ các dòng dữ liệu
                var rows = usedRange.RowsUsed().Skip(1); // Skip header row
                var headers = worksheet.Row(1).CellsUsed().ToDictionary(c => c.Address.ColumnNumber, c => c.Value.ToString().Trim());

                var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

                int rowNum = 1;
                foreach (var row in rows)
                {
                    rowNum++;
                    try
                    {
                        var item = new T();
                        foreach (var cell in row.CellsUsed())
                        {
                            if (headers.TryGetValue(cell.Address.ColumnNumber, out string? header))
                            {
                                var prop = properties.FirstOrDefault(p => string.Equals(p.Name, header, StringComparison.OrdinalIgnoreCase));
                                if (prop != null && prop.CanWrite)
                                {
                                    try 
                                    {
                                        var value = ConvertValue(cell.Value, prop.PropertyType);
                                        prop.SetValue(item, value);
                                    }
                                    catch (Exception ex)
                                    {
                                        result.Errors.Add(new RowError { RowNumber = rowNum, Data = cell.Value.ToString(), ErrorMessage = $"Lỗi định dạng cột '{header}': {ex.Message}" });
                                    }
                                }
                            }
                        }
                        result.ImportedItems.Add(item);
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add(new RowError { RowNumber = rowNum, Data = "Dòng dữ liệu", ErrorMessage = ex.Message });
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Không thể đọc file Excel: {ex.Message}");
            }

            return result;
        }

        // ClosedXML giữ kiểu ô gốc; hàm này đổi sang kiểu property, kể cả Nullable<T>, trước khi reflection gán giá trị
        private object? ConvertValue(XLCellValue value, Type targetType)
        {
            if (value.IsBlank) return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            var strValue = value.ToString();
            
            // lấy kiểu lõi để int? và int đi chung một nhánh chuyển đổi
            Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (underlyingType == typeof(string)) return strValue;
            if (underlyingType == typeof(int)) return (int)value.GetNumber();
            if (underlyingType == typeof(decimal)) return (decimal)value.GetNumber();
            if (underlyingType == typeof(double)) return value.GetNumber();
            if (underlyingType == typeof(bool)) return value.GetBoolean();
            if (underlyingType == typeof(DateTime)) return value.GetDateTime();

            return Convert.ChangeType(strValue, underlyingType);
        }
    }
}
