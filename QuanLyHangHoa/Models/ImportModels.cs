using System.Collections.Generic;

namespace QuanLyHangHoa.Models
{
    public class ImportResult<T>
    {
        public int SuccessCount { get; set; }
        public List<RowError> Errors { get; set; } = new();
        public List<T> ImportedItems { get; set; } = new();
    }

    public class RowError
    {
        public int RowNumber { get; set; }
        public string Data { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
