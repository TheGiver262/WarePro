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
        public ImportResult<T> Import<T>(string filePath) where T : new()
        {
            var result = new ImportResult<T>();
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                PrepareHeaderForMatch = args => args.Header?.ToLowerInvariant() ?? string.Empty,
                HeaderValidated = null,
                MissingFieldFound = null,
            };

            try
            {
                using var reader = new StreamReader(filePath);
                using var csv = new CsvReader(reader, config);
                
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
