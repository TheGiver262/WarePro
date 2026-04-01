using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    // Dịch vụ Quản lý Phiếu Bảo Hành Khách hàng
    public class WarrantyService
    {
        // Đường dẫn thư mục cục bộ lưu ảnh bảo hành
        private string _imageFolderPath;

        public WarrantyService()
        {
            // Thiết lập đường dẫn thư mục lưu ảnh: Tạm để AppData hoặc cạnh file EXE
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _imageFolderPath = Path.Combine(appDataPath, "QuanLyHangHoa", "WarrantyImages");
            
            // Tự động tạo thư mục nếu chưa có
            if (!Directory.Exists(_imageFolderPath))
            {
                Directory.CreateDirectory(_imageFolderPath);
            }
        }

        // Tạo 1 hoặc NHIỀU phiếu bảo hành nếu khách mang tới nhiều sản phẩm nhưng hạn bảo hành khác nhau
        // Lưu ý: Requirement 4 --> "Nếu thời gian BH khác nhau -> Tách nhiều phiếu. Ảnh tuỳ chọn hợp lý."
        public bool CreateWarrantyTickets(int invoiceId, string customerName, string condition, string sourceImageFilePath, List<WarrantyTicketDetail> brokenItems)
        {
            using (var db = new AppDbContext())
            {
                using (var transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        // Xử lý Ảnh: Copy ảnh từ máy người dùng vào thư mục nội bộ của App
                        string savedImagePath = "";
                        if (!string.IsNullOrEmpty(sourceImageFilePath) && File.Exists(sourceImageFilePath))
                        {
                            string fileExt = Path.GetExtension(sourceImageFilePath);
                            // Tạo tên file ngẫu nhiên bằng Guid để chống trùng lặp
                            string newFileName = Guid.NewGuid().ToString() + fileExt; 
                            savedImagePath = Path.Combine(_imageFolderPath, newFileName);
                            
                            // Thực hiện copy file
                            File.Copy(sourceImageFilePath, savedImagePath);
                        }

                        // Nhóm các sản phẩm lỗi theo "Số tháng bảo hành" (đã lấy sẵn từ DB về lúc bán)
                        // Grouping: Key = WarrantyMonths
                        var groupedItems = brokenItems.GroupBy(b => b.Product.WarrantyMonths).ToList();

                        foreach (var group in groupedItems)
                        {
                            // Tính toán thời gian hết hạn bảo hành của nhóm này
                            // Dựa theo ngày lập Hoá Đơn Mua lúc khách Mua Hàng
                            var invoiceData = db.Invoices.FirstOrDefault(iv => iv.Id == invoiceId);
                            DateTime baseDate = invoiceData != null ? invoiceData.InvoiceDate : DateTime.Now;

                            int months = group.Key;
                            DateTime endDate = baseDate.AddMonths(months);

                            // Tạo chung MỘT vé WarrantyTicket cho các sản phẩm có CÙNG THỜI HẠN
                            var ticket = new WarrantyTicket()
                            {
                                InvoiceId = invoiceId,
                                CustomerName = customerName,
                                DateCreated = DateTime.Now,
                                WarrantyEndDate = endDate,
                                ConditionReceived = condition,
                                ImagePath = savedImagePath, // Lưu đường dẫn (Path) vào CSDL giúp App cực nhẹ
                                Status = "Chờ xử lý"
                            };

                            db.WarrantyTickets.Add(ticket);
                            db.SaveChanges(); // Lấy ID ticket mới

                            // add Details vào sau lưng nó
                            foreach (var brokenProd in group)
                            {
                                brokenProd.WarrantyTicketId = ticket.Id;
                                // Xoá product ảo ra để EF không bị nhầm lẫn tracking
                                brokenProd.Product = null;
                                db.WarrantyTicketDetails.Add(brokenProd);
                            }
                        }

                        db.SaveChanges();
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
