using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models
{
    // Bảng Hoá đơn Gốc
    public class Invoice
    {
        public int Id { get; set; }
        public DateTime InvoiceDate { get; set; }
        
        // Tổng tiền hoá đơn
        public decimal TotalAmount { get; set; }
        
        // Tên Khách hàng (có thể trống nếu khách lẻ)
        public string CustomerName { get; set; } = string.Empty;

        // Nhân viên bán hàng
        public int EmployeeId { get; set; }
        public virtual Employee? Employee { get; set; }

        public virtual ICollection<InvoiceDetail> InvoiceDetails { get; set; } = new List<InvoiceDetail>();
        public virtual ICollection<WarrantyTicket> WarrantyTickets { get; set; } = new List<WarrantyTicket>();
    }

    // Bảng chi tiết Hoá đơn
    public class InvoiceDetail
    {
        public int Id { get; set; }
        public int InvoiceId { get; set; }
        public virtual Invoice? Invoice { get; set; }

        public int ProductId { get; set; }
        public virtual Product? Product { get; set; }

        public int Quantity { get; set; }
        
        // Giá bán thực tế (có thể khác giá niêm yết do chiết khấu)
        public decimal UnitPrice { get; set; }
    }
}
