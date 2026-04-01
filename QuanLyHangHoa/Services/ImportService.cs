using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    // Dịch vụ Quản lý Nhập Kho Hàng Hoá
    public class ImportService
    {
        // Hàm lưu Phiếu nhập kho, NẾU thành công sẽ CỘNG số lượng tồn kho tự động
        public bool CreateImportReceipt(ImportReceipt newReceipt, List<ImportReceiptDetail> details)
        {
            using (var db = new AppDbContext())
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        // 1. Lưu thông tin phiếu nhập
                        newReceipt.ImportDate = DateTime.Now;
                        db.ImportReceipts.Add(newReceipt);
                        db.SaveChanges(); // Lấy ID vừa tạo

                        // 2. Lưu chi tiết phiếu nhập & Tăng tồn kho
                        foreach (var item in details)
                        {
                            item.ImportReceiptId = newReceipt.Id;
                            db.ImportReceiptDetails.Add(item);

                            // Lấy sản phẩm tương ứng từ kho
                            var productInDb = db.Products.FirstOrDefault(p => p.Id == item.ProductId);
                            if (productInDb != null)
                            {
                                // TỰ ĐỘNG CỘNG THÊM LƯỢNG TỒN KHO
                                productInDb.Quantity += item.Quantity;
                                // Có thể tự động update luôn Giá Hiện Tại (UnitPrice) tại đây nếu thiết kế yêu cầu tính trung bình giá.
                            }
                        }

                        // 3. Đẩy lên DB
                        db.SaveChanges();

                        // 4. Chốt giao dịch
                        transaction.Commit();
                        return true;
                    }
                    catch
                    {
                        transaction.Rollback();
                        return false;
                    }
                }
            }
        }
    }
}
