using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class StockOutService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public StockOutService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public List<StockOut> GetAll()
        {
            using var db = _contextFactory();
            return db.StockOuts
                .Include(s => s.Customer)
                .Include(s => s.Creator)
                .Include(s => s.Warehouse)
                .Include(s => s.Lines)
                    .ThenInclude(d => d.Product)
                .OrderByDescending(s => s.CreatedAt)
                .ToList();
        }

        public List<StockOut> GetStockOutPaged(
            string code,
            string customerName,
            DateTime? startDate,
            DateTime? endDate,
            int? warehouseId,
            string status,
            int skip,
            int take)
        {
            using var db = _contextFactory();
            var query = db.StockOuts.AsNoTracking()
                .Include(s => s.Customer)
                .Include(s => s.Creator)
                .Include(s => s.Warehouse)
                .Include(s => s.Lines)
                    .ThenInclude(d => d.Product)
                .AsQueryable();

            query = ApplyStockOutFilters(query, code, customerName, startDate, endDate, warehouseId, status);

            return query
                .OrderByDescending(s => s.ExportDate)
                .ThenByDescending(s => s.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToList();
        }

        public int GetStockOutCount(
            string code,
            string customerName,
            DateTime? startDate,
            DateTime? endDate,
            int? warehouseId,
            string status)
        {
            using var db = _contextFactory();
            var query = db.StockOuts.AsNoTracking().AsQueryable();
            query = ApplyStockOutFilters(query, code, customerName, startDate, endDate, warehouseId, status);
            return query.Count();
        }

        private IQueryable<StockOut> ApplyStockOutFilters(
            IQueryable<StockOut> query,
            string code,
            string customerName,
            DateTime? startDate,
            DateTime? endDate,
            int? warehouseId,
            string status)
        {
            if (!string.IsNullOrWhiteSpace(code))
            {
                var term = code.Trim();
                query = query.Where(s => s.DocumentCode.Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(customerName))
            {
                var term = customerName.Trim();
                query = query.Where(s => s.Customer != null && s.Customer.DisplayName.Contains(term));
            }

            if (startDate.HasValue)
            {
                query = query.Where(s => s.ExportDate >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(s => s.ExportDate <= endOfDay);
            }

            if (warehouseId.HasValue && warehouseId.Value > 0)
            {
                query = query.Where(s => s.WarehouseId == warehouseId.Value);
            }

            if (!string.IsNullOrEmpty(status) && status != "Tất cả")
            {
                string dbStatus = status == "Đã ghi sổ" ? DocumentStatus.Posted : DocumentStatus.Draft;
                query = query.Where(s => s.Status == dbStatus || (dbStatus == DocumentStatus.Draft && s.Status == "nháp") || (dbStatus == DocumentStatus.Posted && s.Status == "đã ghi sổ"));
            }

            return query;
        }

        public void Create(StockOut stockOut, List<StockOutLine> lines, int userId)
        {
            SaveDraft(stockOut, lines, userId);
            Post(stockOut.Id, userId);
        }

        public void SaveDraft(StockOut stockOut, List<StockOutLine> lines, int userId)
        {
            using var db = _contextFactory();
            
            StockOut? existing = null;
            if (stockOut.Id > 0)
            {
                existing = db.StockOuts
                    .Include(s => s.Lines)
                    .FirstOrDefault(s => s.Id == stockOut.Id);
            }

            // Extract serial numbers to DraftSerials string and calculate BaseQuantity
            var productIds = lines.Select(l => l.ProductId).Distinct().ToList();
            var unitMap = db.ProductUnits
                .Where(pu => productIds.Contains(pu.ProductId))
                .ToList();

            foreach (var line in lines)
            {
                var serials = line.ProductSerials?.Select(ps => ps.SerialNumber.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList() ?? new List<string>();
                if (serials.Any())
                {
                    line.DraftSerials = string.Join(",", serials);
                }
                else
                {
                    line.DraftSerials = null;
                }
                line.ProductSerials = new List<ProductSerial>(); // Prevent EF from modifying ProductSerials

                // Auto convert to base unit
                var pu = unitMap.FirstOrDefault(u => u.ProductId == line.ProductId && u.UnitId == line.UnitId);
                line.BaseQuantity = line.Quantity * (pu?.ConversionFactor ?? 1m);
            }

            if (existing != null)
            {
                if (existing.Status == DocumentStatus.Posted || existing.Status == "đã ghi sổ")
                    throw new Exception("Không thể cập nhật phiếu đã ghi sổ.");

                var beforeJson = Serialize(existing);

                existing.WarehouseId = stockOut.WarehouseId;
                existing.CustomerId = stockOut.CustomerId;
                existing.ExportDate = stockOut.ExportDate;
                existing.Notes = stockOut.Notes;
                existing.UpdatedAt = DateTime.Now;
                existing.UpdatedBy = userId;

                // Remove old lines
                db.StockOutLines.RemoveRange(existing.Lines);
                existing.Lines = lines;

                db.SaveChanges();
                stockOut.Id = existing.Id;
                stockOut.Status = existing.Status;

                var afterJson = Serialize(existing);
                AddAudit(db, "UPDATE", existing.Id, beforeJson, afterJson, userId);
            }
            else
            {
                stockOut.Lines = lines;
                stockOut.CreatedBy = userId;
                stockOut.CreatedAt = DateTime.Now;
                stockOut.Status = DocumentStatus.Draft;

                if (string.IsNullOrWhiteSpace(stockOut.DocumentCode))
                {
                    stockOut.DocumentCode = $"SO-{DateTime.Now:yyyyMMddHHmmss}";
                }

                db.StockOuts.Add(stockOut);
                db.SaveChanges();

                var afterJson = Serialize(stockOut);
                AddAudit(db, "CREATE", stockOut.Id, null, afterJson, userId);
            }
        }

        public void Post(int stockOutId, int userId)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();

            var stockOut = db.StockOuts
                .Include(s => s.Lines)
                .FirstOrDefault(s => s.Id == stockOutId);

            if (stockOut == null) throw new Exception("Không tìm thấy phiếu xuất kho.");
            if (stockOut.Status == DocumentStatus.Posted || stockOut.Status == "đã ghi sổ") 
                throw new Exception("Phiếu này đã được ghi sổ.");

            // Auto calculate BaseQuantity from ProductUnit in db
            var lineProductIds = stockOut.Lines.Select(l => l.ProductId).Distinct().ToList();
            var unitMap = db.ProductUnits
                .Where(pu => lineProductIds.Contains(pu.ProductId))
                .ToList();

            foreach (var line in stockOut.Lines)
            {
                var pu = unitMap.FirstOrDefault(u => u.ProductId == line.ProductId && u.UnitId == line.UnitId);
                line.BaseQuantity = line.Quantity * (pu?.ConversionFactor ?? 1m);
            }

            var beforeJson = Serialize(stockOut);

            // Load serials for validation and posting
            var lineSerialsMap = new Dictionary<int, List<string>>();
            var allDocumentSerials = new List<string>();
            foreach (var line in stockOut.Lines)
            {
                var serials = new List<string>();
                if (!string.IsNullOrWhiteSpace(line.DraftSerials))
                {
                    serials = line.DraftSerials.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                }
                else
                {
                    // Legacy fallback
                    serials = db.ProductSerials
                        .Where(ps => ps.LastStockOutLineId == line.Id)
                        .Select(ps => ps.SerialNumber)
                        .ToList();
                }
                lineSerialsMap[line.Id] = serials;
                allDocumentSerials.AddRange(serials);

                var product = db.Products.Find(line.ProductId);
                if (product != null && product.IsSerialTracked)
                {
                    if (serials.Count != (int)line.Quantity)
                    {
                        throw new Exception($"Sản phẩm {product.DisplayName} yêu cầu {(int)line.Quantity} serial, nhưng hiện có {serials.Count}.");
                    }

                    // Check that all serials are InStock, belong to this product, and are in the correct warehouse
                    var dbSerials = db.ProductSerials
                        .Where(ps => serials.Contains(ps.SerialNumber))
                        .ToList();

                    var invalidSerials = new List<string>();
                    foreach (var sn in serials)
                    {
                        var dbSerial = dbSerials.FirstOrDefault(ps => string.Equals(ps.SerialNumber, sn, StringComparison.OrdinalIgnoreCase));
                        if (dbSerial == null || 
                            dbSerial.ProductId != line.ProductId || 
                            dbSerial.CurrentWarehouseId != stockOut.WarehouseId || 
                            dbSerial.CurrentStatus != "InStock")
                        {
                            invalidSerials.Add(sn);
                        }
                    }

                    if (invalidSerials.Any())
                    {
                        throw new Exception($"Các số serial sau đã được xuất kho ở phiếu khác hoặc không còn tồn kho trong kho này: [{string.Join(", ", invalidSerials)}]. Vui lòng sửa lại phiếu nháp trước khi duyệt.");
                    }
                }
            }

            // Check duplicate serial numbers within the current document
            var duplicateDocumentSerials = allDocumentSerials
                .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicateDocumentSerials.Any())
            {
                throw new Exception($"Các số serial sau bị trùng lặp trong phiếu: [{string.Join(", ", duplicateDocumentSerials)}]. Vui lòng kiểm tra lại trước khi duyệt.");
            }

            stockOut.Status = DocumentStatus.Posted;
            stockOut.PostedBy = userId;
            stockOut.PostedAt = DateTime.Now;
            db.SaveChanges();

            var postingService = new InventoryPostingService(
                new EfInventoryUnitOfWork(db),
                new DbDefaultWarehouseProvider(db),
                new SystemClock());

            foreach (var line in stockOut.Lines.OrderBy(l => l.ProductId))
            {
                var serials = lineSerialsMap.TryGetValue(line.Id, out var sns) ? sns.ToArray() : Array.Empty<string>();
                
                postingService.PostStockOut(new PostStockOutCommand(
                    stockOut.Id,
                    stockOut.WarehouseId,
                    StockOutKind.Sale,
                    StockDocumentStatus.Posted,
                    line.ProductId,
                    line.BaseQuantity > 0 ? (int)line.BaseQuantity : (int)line.Quantity,
                    serials,
                    userId));
            }

            // Bind posted serials with LastStockOutLineId in database
            foreach (var line in stockOut.Lines)
            {
                var serials = lineSerialsMap.TryGetValue(line.Id, out var sns) ? sns : new List<string>();
                if (serials.Any())
                {
                    var dbSerials = db.ProductSerials.Where(ps => serials.Contains(ps.SerialNumber)).ToList();
                    foreach (var s in dbSerials)
                    {
                        s.LastStockOutLineId = line.Id;
                    }
                }
            }
            db.SaveChanges();

            var afterJson = Serialize(stockOut);
            AddAudit(db, "UPDATE", stockOut.Id, beforeJson, afterJson, userId);

            transaction.Commit();
        }

        public List<ProductSerial> GetInStockSerials(int productId, int warehouseId)
        {
            using var db = _contextFactory();
            return db.ProductSerials
                .Where(s => s.ProductId == productId && s.CurrentWarehouseId == warehouseId && s.CurrentStatus == "InStock")
                .ToList();
        }

        private sealed class DbDefaultWarehouseProvider : IDefaultWarehouseProvider
        {
            private readonly AppDbContext _context;
            public DbDefaultWarehouseProvider(AppDbContext context) => _context = context;

            public int GetDefaultWarehouseId()
            {
                return _context.Warehouses
                    .Where(w => w.IsDefault && w.IsActive)
                    .Select(w => w.Id)
                    .FirstOrDefault() switch { 0 => 1, var id => id };
            }
        }

        private sealed class SystemClock : IClock
        {
            public DateTime Now => DateTime.Now;
        }

        public virtual void Delete(int id, int userId)
        {
            using var db = _contextFactory();
            var stockOut = db.StockOuts
                .Include(s => s.Lines)
                .FirstOrDefault(s => s.Id == id);

            if (stockOut == null) throw new Exception("Không tìm thấy phiếu xuất kho.");
            if (stockOut.Status == DocumentStatus.Posted || stockOut.Status == "đã ghi sổ")
                throw new Exception("Không thể xóa phiếu đã ghi sổ.");

            var beforeJson = JsonSerializer.Serialize(new { stockOut.Id, stockOut.DocumentCode });

            db.StockOutLines.RemoveRange(stockOut.Lines);
            db.StockOuts.Remove(stockOut);
            db.SaveChanges();

            AddAudit(db, "DELETE", id, beforeJson, null, userId);
        }

        private string Serialize(StockOut s)
        {
            return JsonSerializer.Serialize(new
            {
                s.Id,
                s.DocumentCode,
                s.WarehouseId,
                s.CustomerId,
                s.ExportDate,
                s.Notes,
                s.Status,
                s.CreatedAt,
                s.CreatedBy,
                s.UpdatedAt,
                s.UpdatedBy,
                s.PostedAt,
                s.PostedBy,
                Lines = s.Lines?.Select(l => new
                {
                    l.Id,
                    l.ProductId,
                    l.Quantity,
                    l.UnitPrice,
                    DraftSerials = string.IsNullOrEmpty(l.DraftSerials) ? null :
                                   (l.DraftSerials.Length > 150 ? l.DraftSerials.Substring(0, 150) + "... (truncated)" : l.DraftSerials)
                }).ToList()
            });
        }

        private void AddAudit(AppDbContext db, string action, int entityId, string? before, string? after, int performedBy)
        {
            db.AuditLogs.Add(new AuditLog
            {
                EntityName = "StockOut",
                EntityId = entityId,
                ActionCode = action,
                BeforeJson = before,
                AfterJson = after,
                PerformedBy = performedBy,
                PerformedAt = DateTime.Now
            });
            db.SaveChanges();
        }
    }
}
