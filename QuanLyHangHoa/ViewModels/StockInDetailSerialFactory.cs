using System;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.ViewModels
{
    public static class StockInDetailSerialFactory
    {
        // serial chi tiết nhập kho bắt đầu InStock và giữ liên kết dòng nhập làm nguồn truy vết
        public static ProductSerial Create(int productId, string serialNumber, int warehouseId, int stockInLineId)
        {
            return new ProductSerial
            {
                ProductId = productId,
                SerialNumber = serialNumber,
                CurrentWarehouseId = warehouseId,
                LastStockInLineId = stockInLineId,
                CurrentStatus = "InStock"
            };
        }
    }
}
