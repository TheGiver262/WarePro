using System.Collections.Generic;
using System.Linq;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public static class StockInDetailSerialFactory
    {
        public static List<ProductSerial> CreateSerials(Product product, string serialInput)
        {
            return StockInService.ParseSerialRange(serialInput)
                .Select(serialNumber => new ProductSerial
                {
                    SerialNumber = serialNumber,
                    ProductId = product.Id,
                    Status = "InStock"
                })
                .ToList();
        }
    }
}
