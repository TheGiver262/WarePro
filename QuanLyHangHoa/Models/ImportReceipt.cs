using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models
{
    // Bảng Phiếu Nhập Kho
    public class ImportReceipt
    {
        public int Id { get; set; }
        public DateTime ImportDate { get; set; }
        
        // Tổng tiền nhập kho
        public decimal TotalAmount { get; set; }
        
        // Nhân viên thực hiện nhập
        public int EmployeeId { get; set; }
        public virtual Employee? Employee { get; set; }

        public virtual ICollection<ImportReceiptDetail> ImportReceiptDetails { get; set; } = new List<ImportReceiptDetail>();
    }

    // Bảng Chi tiết phiếu nhập
    public class ImportReceiptDetail
    {
        public int Id { get; set; }
        public int ImportReceiptId { get; set; }
        public virtual ImportReceipt? ImportReceipt { get; set; }

        public int ProductId { get; set; }
        public virtual Product? Product { get; set; }

        public int Quantity { get; set; }
        // Giá nhập vào
        public decimal ImportPrice { get; set; }
        
        // Tình trạng hàng khi nhập
        public string Status { get; set; } = "Mới";
    }
}
