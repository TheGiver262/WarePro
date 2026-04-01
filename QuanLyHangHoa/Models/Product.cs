namespace QuanLyHangHoa.Models
{
    // Bảng Hàng hoá
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        
        // Số lượng tồn kho hiện tại
        public int Quantity { get; set; }
        
        // Đơn giá bán
        public decimal UnitPrice { get; set; }
        
        public string Origin { get; set; } = string.Empty;
        
        // Thời gian bảo hành (tính theo tháng)
        public int WarrantyMonths { get; set; }
        
        public string Notes { get; set; } = string.Empty;
    }
}
