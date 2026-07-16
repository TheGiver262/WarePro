using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services.DataImport
{
    public class CsvImportService
    {
        // CsvHelper ánh xạ trực tiếp header vào thuộc tính; lỗi một dòng được ghi lại rồi bỏ qua để đọc tiếp file
        public ImportResult<T> Import<T>(string filePath) where T : new()
        {
            var result = new ImportResult<T>();
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                PrepareHeaderForMatch = args => args.Header?.ToLowerInvariant() ?? string.Empty,
                HeaderValidated = null,
                MissingFieldFound = null,
                ReadingExceptionOccurred = args =>
                {
                    result.Errors.Add(new RowError 
                    { 
                        RowNumber = args.Exception?.Context?.Parser?.Row ?? 0, 
                        Data = args.Exception?.Context?.Parser?.RawRecord ?? string.Empty, 
                        ErrorMessage = $"Lỗi phân tích: {args.Exception?.Message ?? "Không rõ lỗi"}" 
                    });
                    // false yêu cầu CsvHelper bỏ qua lỗi hiện tại và tiếp tục dòng kế tiếp
                    return false; // Return false to ignore the exception and continue reading
                }
            };

            try
            {
                using var reader = new StreamReader(filePath);
                using var csv = new CsvReader(reader, config);
                
                // bắt đầu từ 1 vì dòng đầu là header; số báo lỗi vì vậy khớp số dòng người dùng thấy trong file
                int rowNum = 1;
                while (csv.Read())
                {
                    rowNum++;
                    try
                    {
                        var record = csv.GetRecord<T>();
                        if (record != null)
                        {
                            result.ImportedItems.Add(record);
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add(new RowError 
                        { 
                            RowNumber = rowNum, 
                            Data = csv.Context.Parser?.RawRecord ?? string.Empty, 
                            ErrorMessage = $"Lỗi phân tích: {ex.Message}" 
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Không thể đọc file CSV: {ex.Message}");
            }

            return result;
        }
    }
}
