using System;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.ViewModels
{
    public static class StockInDetailSerialFactory
    {
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
