namespace QuanLyHangHoa.Models
{
    // quantity là đơn vị nhập từ file; service sẽ quy đổi và đối soát serial trước khi post tồn đầu kỳ
    public class OpeningBalanceImportRow
    {
        public int RowNumber { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public string SerialNumbers { get; set; } = string.Empty;
    }
}
