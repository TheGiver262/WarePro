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

        public List<ProductSerial> SearchSerials(string serial, string product, string brand, string status)
        {
            using var db = _contextFactory();
            var query = db.ProductSerials
                .Include(s => s.Product)
                    .ThenInclude(p => p.Brand)
                .Include(s => s.CurrentWarehouse)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && status != "All")
            {
                query = query.Where(s => s.CurrentStatus == status);
            }

            if (!string.IsNullOrWhiteSpace(serial))
            {
                var keyword = serial.Trim();
                query = query.Where(s => s.SerialNumber.Contains(keyword));
            }

            if (!string.IsNullOrWhiteSpace(product))
            {
                var keyword = product.Trim();
                query = query.Where(s => s.Product.DisplayName.Contains(keyword) || s.Product.ProductCode.Contains(keyword));
            }

            if (!string.IsNullOrWhiteSpace(brand))
            {
                var keyword = brand.Trim();
                query = query.Where(s => s.Product.Brand.DisplayName.Contains(keyword));
            }

            return query
                .OrderBy(s => s.SerialNumber)
                .ToList();
        }
    }
}
