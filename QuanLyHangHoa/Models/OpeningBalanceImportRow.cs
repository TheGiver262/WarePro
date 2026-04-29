namespace QuanLyHangHoa.Models
{
    public class OpeningBalanceImportRow
    {
        public int RowNumber { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public string SerialNumbers { get; set; } = string.Empty;
    }
}
