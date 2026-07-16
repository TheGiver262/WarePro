using System.Collections.Generic;

namespace QuanLyHangHoa.Models
{
    // ImportedItems là dữ liệu parse được; SuccessCount chỉ tăng sau khi bước lưu database thành công
    public class ImportResult<T>
    {
        public int SuccessCount { get; set; }
        public List<RowError> Errors { get; set; } = new();
        public List<T> ImportedItems { get; set; } = new();
    }

    // RowNumber dùng số dòng người dùng thấy trong file; 0/-1 dành cho lỗi không gắn một dòng cụ thể
    public class RowError
    {
        public int RowNumber { get; set; }
        public string Data { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
