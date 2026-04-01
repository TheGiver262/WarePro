using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models
{
    // Bảng Phiếu Bảo Hành (Mỗi vé áp dụng chung 1 thời hạn kết thúc bảo hành)
    public class WarrantyTicket
    {
        public int Id { get; set; }
        
        // Thuộc hoá đơn mua hàng nào
        public int InvoiceId { get; set; }
        public virtual Invoice? Invoice { get; set; }

        public DateTime DateCreated { get; set; }
        
        // Ngày hết hạn bảo hành
        public DateTime WarrantyEndDate { get; set; }

        public string CustomerName { get; set; } = string.Empty;
        
        // Tình trạng nhận (VD: Trầy xước nhẹ, mất nguồn, v.v.)
        public string ConditionReceived { get; set; } = string.Empty;

        // Trạng thái (Đang chờ xử lý, Đã sửa xong, Đang trả khách...)
        public string Status { get; set; } = "Chờ xử lý";

        // Đường dẫn ảnh lúc nhận (lưu cục bộ)
        public string ImagePath { get; set; } = string.Empty;

        public virtual ICollection<WarrantyTicketDetail> WarrantyTicketDetails { get; set; } = new List<WarrantyTicketDetail>();
    }

    // Sản phẩm trong phiếu bảo hành
    public class WarrantyTicketDetail
    {
        public int Id { get; set; }
        public int WarrantyTicketId { get; set; }
        public virtual WarrantyTicket? WarrantyTicket { get; set; }

        public int ProductId { get; set; }
        public virtual Product? Product { get; set; }

        // Giải quyết lỗi ntn (Thay thế linh kiện, vệ sinh máy, ...)
        public string Resolution { get; set; } = string.Empty;
    }
}
