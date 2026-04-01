using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore; // Sửa lỗi Include
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    // Dịch vụ quản lý xử lý Hoá Đơn Bán Hàng
    public class InvoiceService
    {
        // Hàm lưu toàn bộ hoá đơn cùng chi tiết, NẾU thành công sẽ trừ số lượng tồn kho tự động
        public bool CreateInvoice(Invoice newInvoice, List<InvoiceDetail> details)
        {
            using (var db = new AppDbContext())
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        // 1. Lưu thông tin hoá đơn cha
                        newInvoice.InvoiceDate = DateTime.Now;
                        db.Invoices.Add(newInvoice);
                        db.SaveChanges(); // Lấy được newInvoice.Id sau khi lưu

                        // 2. Lưu từng dòng sản phẩm (Detail) và trừ số lượng trong kho
                        foreach (var item in details)
                        {
                            item.InvoiceId = newInvoice.Id; // Gắn ID cha
                            db.InvoiceDetails.Add(item);

                            // Lấy sản phẩm từ kho ra để rà soát
                            var productInDb = db.Products.FirstOrDefault(p => p.Id == item.ProductId);
                            if (productInDb != null)
                            {
                                // Kỉểm tra nếu kho không đủ hàng
                                if (productInDb.Quantity < item.Quantity)
                                {
                                    // Huỷ toàn bộ quá trình nếu 1 món không đủ hàng (Bảo toàn dữ liệu)
                                    transaction.Rollback();
                                    return false;
                                }

                                // TỰ ĐỘNG trừ lượng tồn kho
                                productInDb.Quantity -= item.Quantity;
                            }
                        }

                        // 3. Đẩy toàn bộ thay đổi lên Database 
                        db.SaveChanges();

                        // 4. Nếu mọi thứ suôn sẻ, chốt giao dịch vĩnh viễn
                        transaction.Commit();
                        return true;
                    }
                    catch
                    {
                        // Lỗi bất ngờ -> hoàn tác (Rollback) DB về trạng thái y hệt trước khi bấm Thanh toán
                        transaction.Rollback();
                        return false;
                    }
                }
            }
        }

        // Hàm hỗ trợ Tải lại danh sách hoá đơn (Dashboard / Thống kê dùng)
        public List<Invoice> GetAllInvoices()
        {
            using (var db = new AppDbContext())
            {
                // Lấy Invoice nhưng gộp luôn danh sách Details và Employee liên quan bằng .Include
                // Hàm Include thuộc thư viện EntityFrameworkCore
                return db.Invoices
                         .Include(i => i.Employee)
                         .Include(i => i.InvoiceDetails)
                         .OrderByDescending(i => i.InvoiceDate)
                         .ToList();
            }
        }
    }
}
