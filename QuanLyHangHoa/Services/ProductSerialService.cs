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

        public ProductSerialService()
            : this(() => new AppDbContext())
        {
        }

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
                .Where(serial => !serial.IsDeleted);

            if (!string.IsNullOrWhiteSpace(status) && status != "All")
            {
                query = query.Where(serial => serial.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var keyword = searchText.Trim();
                query = query.Where(serial =>
                    serial.SerialNumber.Contains(keyword) ||
                    serial.Status.Contains(keyword) ||
                    (serial.Product != null && serial.Product.Name.Contains(keyword)));
            }

            return query
                .OrderBy(serial => serial.SerialNumber)
                .ToList();
        }
    }
}
