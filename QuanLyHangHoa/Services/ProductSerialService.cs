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

        public List<ProductSerial> SearchSerials(string serial, string product, string brand, string status, DateTime? fromDate = null, DateTime? toDate = null, string note = "")
        {
            using var db = _contextFactory();
            var query = db.ProductSerials
                .AsNoTracking()
                .Include(s => s.Product)
                    .ThenInclude(p => p!.Brand)
                .Include(s => s.CurrentWarehouse)
                .Include(s => s.LastStockInLine)
                    .ThenInclude(l => l!.StockIn)
                .Include(s => s.LastStockOutLine)
                    .ThenInclude(l => l!.StockOut)
                .AsQueryable();

            query = ApplySerialFilters(query, serial, product, brand, status, fromDate, toDate, note);

            return query
                .OrderByDescending(s => s.Id)
                .ThenBy(s => s.SerialNumber)
                .ToList();
        }

        public List<ProductSerial> SearchSerialsPaged(string serial, string product, string brand, string status, DateTime? fromDate, DateTime? toDate, string note, int skip, int take)
        {
            using var db = _contextFactory();
            var query = db.ProductSerials
                .AsNoTracking()
                .Include(s => s.Product)
                    .ThenInclude(p => p!.Brand)
                .Include(s => s.CurrentWarehouse)
                .Include(s => s.LastStockInLine)
                    .ThenInclude(l => l!.StockIn)
                .Include(s => s.LastStockOutLine)
                    .ThenInclude(l => l!.StockOut)
                .AsQueryable();

            query = ApplySerialFilters(query, serial, product, brand, status, fromDate, toDate, note);

            return query
                .OrderByDescending(s => s.Id)
                .ThenBy(s => s.SerialNumber)
                .Skip(skip)
                .Take(take)
                .ToList();
        }

        public int GetSerialsCount(string serial, string product, string brand, string status, DateTime? fromDate, DateTime? toDate, string note)
        {
            using var db = _contextFactory();
            var query = db.ProductSerials.AsNoTracking().AsQueryable();
            query = ApplySerialFilters(query, serial, product, brand, status, fromDate, toDate, note);
            return query.Count();
        }

        public void UpdateNote(int serialId, string? note, int userId)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();
            var serial = db.ProductSerials.SingleOrDefault(item => item.Id == serialId)
                ?? throw new InvalidOperationException("Không tìm thấy serial.");
            var beforeJson = System.Text.Json.JsonSerializer.Serialize(new { serial.Note });

            serial.Note = note;
            db.AuditLogs.Add(new AuditLog
            {
                EntityName = "ProductSerial",
                EntityId = serial.Id,
                ActionCode = "UPDATE",
                PerformedBy = userId,
                PerformedAt = DateTime.UtcNow,
                BeforeJson = beforeJson,
                AfterJson = System.Text.Json.JsonSerializer.Serialize(new { serial.Note })
            });

            try
            {
                db.SaveChanges();
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private IQueryable<ProductSerial> ApplySerialFilters(IQueryable<ProductSerial> query, string serial, string product, string brand, string status, DateTime? fromDate, DateTime? toDate, string note)
        {
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
                query = query.Where(s => s.Product != null && (s.Product.DisplayName.Contains(keyword) || s.Product.ProductCode.Contains(keyword)));
            }

            if (!string.IsNullOrWhiteSpace(brand))
            {
                var keyword = brand.Trim();
                query = query.Where(s => s.Product != null && s.Product.Brand != null && s.Product.Brand.DisplayName.Contains(keyword));
            }

            if (!string.IsNullOrWhiteSpace(note))
            {
                var keyword = note.Trim();
                query = query.Where(s => s.Note != null && s.Note.Contains(keyword));
            }

            if (fromDate.HasValue)
            {
                query = query.Where(s => s.LastStockInLine != null && s.LastStockInLine.StockIn != null && s.LastStockInLine.StockIn.CreatedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                var endOfDay = toDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(s => s.LastStockInLine != null && s.LastStockInLine.StockIn != null && s.LastStockInLine.StockIn.CreatedAt <= endOfDay);
            }

            return query;
        }
    }
}
