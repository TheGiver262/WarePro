using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class ProductSerialService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public ProductSerialService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public List<ProductSerial> SearchSerials(string searchText, string status)
        {
            using var db = _contextFactory();
            var query = db.ProductSerials
                .Include(serial => serial.Product)
                .Include(serial => serial.CurrentWarehouse)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && status != "All")
            {
                query = query.Where(serial => serial.CurrentStatus == status);
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var keyword = searchText.Trim();
                query = query.Where(serial =>
                    serial.SerialNumber.Contains(keyword) ||
                    serial.CurrentStatus.Contains(keyword) ||
                    (serial.Product != null && serial.Product.DisplayName.Contains(keyword)));
            }

            return query
                .OrderBy(serial => serial.SerialNumber)
                .ToList();
        }
    }
}
