using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services.DataImport
{
    public class DataImportManager
    {
        private readonly ExcelImportService _excelService = new();
        private readonly CsvImportService _csvService = new();

        // bước đọc chỉ tạo model và lỗi dòng; có ít nhất một model hợp lệ mới mở context để upsert
        public ImportResult<T> ProcessFile<T>(string filePath) where T : class, new()
        {
            string extension = Path.GetExtension(filePath).ToLower();
            ImportResult<T> result;

            if (extension == ".xlsx" || extension == ".xls")
            {
                result = _excelService.Import<T>(filePath);
            }
            else if (extension == ".csv")
            {
                result = _csvService.Import<T>(filePath);
            }
            else
            {
                throw new NotSupportedException("Định dạng file không được hỗ trợ. Vui lòng chọn file Excel hoặc CSV.");
            }

            if (result.ImportedItems.Any())
            {
                UpsertToDatabase(result);
            }

            return result;
        }

        // upsert tổng quát dựng biểu thức LINQ từ các property đánh dấu ImportKey, không cần viết query riêng cho từng model
        private void UpsertToDatabase<T>(ImportResult<T> result) where T : class
        {
            using var db = new AppDbContext();
            var dbSet = db.Set<T>();
            var keyProps = typeof(T).GetProperties().Where(p => p.GetCustomAttribute<ImportKeyAttribute>() != null).ToList();

            // model không khai báo ImportKey thì dùng Id làm khóa cuối cùng
            if (!keyProps.Any())
            {
                var idProp = typeof(T).GetProperty("Id");
                if (idProp != null) keyProps.Add(idProp);
            }

            if (!keyProps.Any())
            {
                throw new Exception($"Không thể thực hiện Upsert cho {typeof(T).Name} vì không tìm thấy thuộc tính định danh (Id hoặc [ImportKey]).");
            }

            foreach (var item in result.ImportedItems)
            {
                try
                {
                    // predicate ghép nhiều khóa bằng AND, ví dụ cùng mã sản phẩm và mã kho
                    var parameter = Expression.Parameter(typeof(T), "x");
                    Expression? predicate = null;

                    foreach (var prop in keyProps)
                    {
                        var value = prop.GetValue(item);
                        if (value == null) continue;

                        var left = Expression.Property(parameter, prop);
                        var right = Expression.Constant(value, prop.PropertyType);
                        var equal = Expression.Equal(left, right);

                        predicate = predicate == null ? equal : Expression.AndAlso(predicate, equal);
                    }

                    T? existing = null;
                    if (predicate != null)
                    {
                        var lambda = Expression.Lambda<Func<T, bool>>(predicate, parameter);
                        existing = dbSet.FirstOrDefault(lambda);
                    }

                    if (existing != null)
                    {
                        // giữ nguyên id và khóa import, chỉ chép các trường dữ liệu có thể ghi
                        foreach (var prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                        {
                            if (prop.Name == "Id" || keyProps.Contains(prop) || !prop.CanWrite) continue;
                            
                            // bỏ navigation và collection để không vô tình thay cả đồ thị quan hệ EF
                            if (prop.PropertyType.IsClass && prop.PropertyType != typeof(string)) continue;

                            var newValue = prop.GetValue(item);
                            prop.SetValue(existing, newValue);
                        }
                    }
                    else
                    {
                        // không tìm thấy khóa tương ứng thì thêm bản ghi mới
                        dbSet.Add(item);
                    }
                    
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new RowError { RowNumber = -1, Data = item.ToString() ?? "", ErrorMessage = $"Lỗi cập nhật DB: {ex.Message}" });
                }
            }

            // lưu một lần sau toàn bộ vòng lặp; lỗi gán từng item vẫn được giữ trong result trước khi lưu
            db.SaveChanges();
        }
    }
}
