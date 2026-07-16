using System;
using System.Collections.Generic;
using System.Linq;

namespace QuanLyHangHoa.Services.DataImport
{
    public enum ImportFileType
    {
        Unknown,
        Product,
        Category,
        ProductSerial,
        StockIn,
        StockOut,
        PurchaseInvoice,
        SalesInvoice
    }

    public class FileClassificationService
    {
        // mỗi loại file có các tên cột tiếng việt và tên kỹ thuật thường gặp; danh sách rộng giúp nhận cả mẫu cũ
        private static readonly Dictionary<ImportFileType, List<string>> TypeKeywords = new()
        {
            {
                ImportFileType.Product, new() 
                { 
                    "mã sản phẩm", "mã sp", "tên sản phẩm", "tên sp", "giá vốn", "giá bán", 
                    "productcode", "displayname", "costprice", "defaultprice", "nước sản xuất", 
                    "nước sx", "xuất xứ", "bảo hành", "thời hạn bảo hành", "quy cách" 
                }
            },
            {
                ImportFileType.Category, new() 
                { 
                    "mã danh mục", "mã loại", "tên danh mục", "tên loại", "nhóm sản phẩm", 
                    "nhóm sp", "categorycode", "categoryname" 
                }
            },
            {
                ImportFileType.ProductSerial, new() 
                { 
                    "số serial", "serial", "mã serial", "serialnumber", "serials" 
                }
            },
            {
                ImportFileType.StockIn, new() 
                { 
                    "mã phiếu nhập", "số phiếu nhập", "ngày nhập", "kho nhập", 
                    "stockindate", "documentcode", "isopeningbalance", "tồn đầu kỳ", "lý do nhập"
                }
            },
            {
                ImportFileType.StockOut, new() 
                { 
                    "mã phiếu xuất", "số phiếu xuất", "ngày xuất", "kho xuất", 
                    "stockoutdate", "lý do xuất"
                }
            },
            {
                ImportFileType.PurchaseInvoice, new() 
                { 
                    "mã hóa đơn mua", "số hóa đơn mua", "ngày hóa đơn mua", "nhà cung cấp", 
                    "tiền thuế", "tiền giảm giá", "invoicecode", "supplierid", "suppliername"
                }
            },
            {
                ImportFileType.SalesInvoice, new() 
                { 
                    "mã hóa đơn bán", "số hóa đơn bán", "ngày hóa đơn bán", "khách hàng", 
                    "taxamount", "discountamount", "customerid", "customername"
                }
            }
        };

        public static string GetTypeDisplayName(ImportFileType type)
        {
            return type switch
            {
                ImportFileType.Product => "Danh mục Sản phẩm",
                ImportFileType.Category => "Danh mục Nhóm sản phẩm",
                ImportFileType.ProductSerial => "Danh sách số Serial",
                ImportFileType.StockIn => "Phiếu Nhập kho",
                ImportFileType.StockOut => "Phiếu Xuất kho",
                ImportFileType.PurchaseInvoice => "Hóa đơn Mua hàng",
                ImportFileType.SalesInvoice => "Hóa đơn Bán hàng",
                _ => "Không xác định"
            };
        }

        // chấm điểm toàn bộ header rồi chọn loại cao nhất; thứ tự dictionary chỉ quyết định khi hai loại bằng điểm
        public ImportFileType Classify(IEnumerable<string> headers)
        {
            if (headers == null || !headers.Any())
                return ImportFileType.Unknown;

            // chuẩn hóa một lần để các phép so phía dưới không phụ thuộc hoa thường hoặc khoảng trắng ngoài
            var normalizedHeaders = headers
                .Select(h => h.Trim().ToLowerInvariant())
                .ToList();

            var bestType = ImportFileType.Unknown;
            int maxScore = 0;

            foreach (var kvp in TypeKeywords)
            {
                int score = 0;
                foreach (var keyword in kvp.Value)
                {
                    if (normalizedHeaders.Any(h => h.Contains(keyword) || keyword.Contains(h)))
                    {
                        // khớp chứa hai chiều để nhận cả tiêu đề đầy đủ và tên rút gọn
                        score += 2; // Direct/Fuzzy match
                    }
                }

                // cột đặc trưng được cộng thêm điểm để không bị các từ chung như mã hoặc ngày lấn át
                if (kvp.Key == ImportFileType.ProductSerial && normalizedHeaders.Any(h => h == "serial" || h == "số serial" || h == "serialnumber"))
                {
                    score += 5;
                }
                if (kvp.Key == ImportFileType.StockIn && normalizedHeaders.Any(h => h == "mã phiếu nhập" || h == "phiếu nhập"))
                {
                    score += 5;
                }
                if (kvp.Key == ImportFileType.StockOut && normalizedHeaders.Any(h => h == "mã phiếu xuất" || h == "phiếu xuất"))
                {
                    score += 5;
                }
                if (kvp.Key == ImportFileType.PurchaseInvoice && normalizedHeaders.Any(h => h == "mã hóa đơn mua" || h == "hóa đơn mua"))
                {
                    score += 5;
                }
                if (kvp.Key == ImportFileType.SalesInvoice && normalizedHeaders.Any(h => h == "mã hóa đơn bán" || h == "hóa đơn bán"))
                {
                    score += 5;
                }

                if (score > maxScore)
                {
                    maxScore = score;
                    bestType = kvp.Key;
                }
            }

            // file có header nhưng không khớp mẫu cũ được đưa về Product để người dùng vẫn có thể tự ánh xạ cột
            if (bestType == ImportFileType.Unknown && normalizedHeaders.Any())
            {
                bestType = ImportFileType.Product;
            }

            return bestType;
        }
    }
}
