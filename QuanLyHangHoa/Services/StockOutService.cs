using System.Collections.Generic;
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class StockOutService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public StockOutService()
            : this(() => new AppDbContext())
        {
        }

        public StockOutService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public List<StockOut> GetAll()
        {
            using var db = _contextFactory();
            return db.StockOuts
                .Where(s => !s.IsDeleted)
                .Include(s => s.Customer)
                .Include(s => s.Employee)
                .Include(s => s.StockOutDetails)
                    .ThenInclude(d => d.Product)
                .Include(s => s.StockOutDetails)
                    .ThenInclude(d => d.ProductSerials)
                .OrderByDescending(s => s.ExportDate)
                .ToList();
        }

        public void Create(StockOut stockOut)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();

            stockOut.TotalAmount = stockOut.StockOutDetails.Sum(d => d.Quantity * d.ExportPrice);

            var serialsByDetail = stockOut.StockOutDetails.ToDictionary(
                detail => detail,
                detail => detail.ProductSerials
                    .Select(serial => serial.SerialNumber)
                    .Where(serialNumber => !string.IsNullOrWhiteSpace(serialNumber))
                    .ToArray());

            foreach (var detail in stockOut.StockOutDetails)
            {
                detail.ProductSerials.Clear();
            }

            db.StockOuts.Add(stockOut);
            db.SaveChanges();

            var postingService = new InventoryPostingService(
                new EfInventoryUnitOfWork(db),
                new DbDefaultWarehouseProvider(db),
                new SystemClock());

            foreach (var detail in stockOut.StockOutDetails)
            {
                postingService.PostStockOut(new PostStockOutCommand(
                    Guid.NewGuid(),
                    StockOutKind.Sale,
                    StockDocumentStatus.Approved,
                    detail.ProductId,
                    detail.Quantity,
                    serialsByDetail[detail],
                    stockOut.EmployeeId));
            }

            transaction.Commit();
        }

        public void SoftDelete(int id)
        {
            using var db = _contextFactory();
            var s = db.StockOuts.Find(id);
            if (s == null) return;
            s.IsDeleted = true;
            db.SaveChanges();
        }

        /// <summary>Get InStock serials for a given productId (for selection when creating StockOut)</summary>
        public List<ProductSerial> GetInStockSerials(int productId)
        {
            using var db = _contextFactory();
            return db.ProductSerials
                .Where(ps => ps.ProductId == productId && ps.Status == "InStock" && !ps.IsDeleted)
                .ToList();
        }

        private sealed class DbDefaultWarehouseProvider : IDefaultWarehouseProvider
        {
            private readonly AppDbContext _context;

            public DbDefaultWarehouseProvider(AppDbContext context)
            {
                _context = context;
            }

            public int GetDefaultWarehouseId()
            {
                var warehouseId = _context.Warehouses
                    .Where(warehouse => warehouse.IsDefault && warehouse.IsActive)
                    .Select(warehouse => warehouse.Id)
                    .FirstOrDefault();

                return warehouseId == 0 ? 1 : warehouseId;
            }
        }

        private sealed class SystemClock : IClock
        {
            public DateTime Now => DateTime.Now;
        }
    }
}
