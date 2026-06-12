using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class StockInService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public StockInService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public virtual List<StockIn> GetAll()
        {
            using var db = _contextFactory();
            return db.StockIns.AsNoTracking()
                .Include(s => s.Supplier)
                .Include(s => s.Creator)
                .Include(s => s.Warehouse)
                .Include(s => s.Lines)
                    .ThenInclude(d => d.Product)
                .Include(s => s.Lines)
                    .ThenInclude(d => d.ProductSerials)
                .OrderByDescending(s => s.CreatedAt)
                .ToList();
        }

        public virtual List<StockIn> GetStockInPaged(
            string code,
            string supplierName,
            DateTime? startDate,
            DateTime? endDate,
            int? warehouseId,
            string status,
            int skip,
            int take)
        {
            using var db = _contextFactory();
            var query = db.StockIns.AsNoTracking()
                .Include(s => s.Supplier)
                .Include(s => s.Creator)
                .Include(s => s.Warehouse)
                .Include(s => s.Lines)
                    .ThenInclude(d => d.Product)
                .AsQueryable();

            query = ApplyStockInFilters(query, code, supplierName, startDate, endDate, warehouseId, status);

            return query
                .OrderByDescending(s => s.ImportDate)
                .ThenByDescending(s => s.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToList();
        }

        public virtual int GetStockInCount(
            string code,
            string supplierName,
            DateTime? startDate,
            DateTime? endDate,
            int? warehouseId,
            string status)
        {
            using var db = _contextFactory();
            var query = db.StockIns.AsNoTracking().AsQueryable();
            query = ApplyStockInFilters(query, code, supplierName, startDate, endDate, warehouseId, status);
            return query.Count();
        }

        private IQueryable<StockIn> ApplyStockInFilters(
            IQueryable<StockIn> query,
            string code,
            string supplierName,
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

            if (!string.IsNullOrWhiteSpace(supplierName))
            {
                var term = supplierName.Trim();
                query = query.Where(s => s.Supplier != null && s.Supplier.DisplayName.Contains(term));
            }

            if (startDate.HasValue)
            {
                query = query.Where(s => s.ImportDate >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(s => s.ImportDate <= endOfDay);
            }

            if (warehouseId.HasValue && warehouseId.Value > 0)
            {
                query = query.Where(s => s.WarehouseId == warehouseId.Value);
            }

            if (!string.IsNullOrEmpty(status) && status != "Tất cả")
            {
                string targetStatus = status == "Đã ghi sổ" ? DocumentStatus.Posted : DocumentStatus.Draft;
                query = query.Where(s => s.Status == targetStatus || (targetStatus == DocumentStatus.Draft && s.Status == "nháp") || (targetStatus == DocumentStatus.Posted && s.Status == "đã ghi sổ"));
            }

            return query;
        }

        public virtual StockIn? GetById(int id)
        {
            using var db = _contextFactory();
            return db.StockIns.AsNoTracking()
                .Include(s => s.Supplier)
                .Include(s => s.Creator)
                .Include(s => s.Lines)
                    .ThenInclude(d => d.Product)
                .Include(s => s.Lines)
                    .ThenInclude(d => d.ProductSerials)
                .FirstOrDefault(s => s.Id == id);
        }

        public virtual void SaveDraft(StockIn stockIn, List<StockInLine> lines, int userId)
        {
            using var db = _contextFactory();
            
            StockIn? existing = null;
            if (stockIn.Id > 0)
            {
                existing = db.StockIns
                    .Include(s => s.Lines)
                        .ThenInclude(l => l.ProductSerials)
                    .FirstOrDefault(s => s.Id == stockIn.Id);
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

                // Update properties
                existing.WarehouseId = stockIn.WarehouseId;
                existing.SupplierId = stockIn.SupplierId;
                existing.ImportDate = stockIn.ImportDate;
                existing.Notes = stockIn.Notes;
                existing.UpdatedAt = DateTime.Now;
                existing.UpdatedBy = userId;

                // Simple strategy: Remove old lines and add new ones
                var oldSerials = existing.Lines.SelectMany(l => l.ProductSerials).ToList();
                if (oldSerials.Any())
                {
                    db.ProductSerials.RemoveRange(oldSerials);
                }

                db.StockInLines.RemoveRange(existing.Lines);
                existing.Lines = lines;
                
                db.SaveChanges();
                stockIn.Id = existing.Id;
                stockIn.Status = existing.Status;

                var afterJson = Serialize(existing);
                AddAudit(db, "UPDATE", existing.Id, beforeJson, afterJson, userId);
            }
            else
            {
                stockIn.Lines = lines;
                stockIn.CreatedBy = userId;
                stockIn.CreatedAt = DateTime.Now;
                stockIn.Status = DocumentStatus.Draft;

                if (string.IsNullOrWhiteSpace(stockIn.DocumentCode))
                {
                    stockIn.DocumentCode = $"SI-{DateTime.Now:yyyyMMddHHmmss}";
                }

                if (stockIn.WarehouseId == 0)
                {
                    stockIn.WarehouseId = new DbDefaultWarehouseProvider(db).GetDefaultWarehouseId();
                }

                db.StockIns.Add(stockIn);
                db.SaveChanges();

                var afterJson = Serialize(stockIn);
                AddAudit(db, "CREATE", stockIn.Id, null, afterJson, userId);
            }
        }

        public virtual void Post(int stockInId, int userId)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();

            var stockIn = db.StockIns
                .Include(s => s.Lines)
                    .ThenInclude(l => l.ProductSerials)
                .FirstOrDefault(s => s.Id == stockInId);

            if (stockIn == null) throw new Exception("Không tìm thấy phiếu nhập kho.");
            if (stockIn.Status == DocumentStatus.Posted || stockIn.Status == "đã ghi sổ") 
                throw new Exception("Phiếu này đã được ghi sổ.");

            // Auto calculate BaseQuantity from ProductUnit in db
            var lineProductIds = stockIn.Lines.Select(l => l.ProductId).Distinct().ToList();
            var unitMap = db.ProductUnits
                .Where(pu => lineProductIds.Contains(pu.ProductId))
                .ToList();

            foreach (var line in stockIn.Lines)
            {
                var pu = unitMap.FirstOrDefault(u => u.ProductId == line.ProductId && u.UnitId == line.UnitId);
                line.BaseQuantity = line.Quantity * (pu?.ConversionFactor ?? 1m);
            }

            var beforeJson = Serialize(stockIn);

            // Load serials for validation and posting
            var lineSerialsMap = new Dictionary<int, List<string>>();
            var allDocumentSerials = new List<string>();

            foreach (var line in stockIn.Lines)
            {
                var serials = new List<string>();
                if (!string.IsNullOrWhiteSpace(line.DraftSerials))
                {
                    serials = line.DraftSerials.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                }
                else
                {
                    // Legacy fallback
                    serials = line.ProductSerials.Select(ps => ps.SerialNumber).ToList();
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

                    // Check if any of these serials already exist in db.ProductSerials
                    var existingDbSerials = db.ProductSerials
                        .Where(ps => serials.Contains(ps.SerialNumber))
                        .Select(ps => ps.SerialNumber)
                        .ToList();

                    if (existingDbSerials.Any())
                    {
                        throw new Exception($"Số serial [{string.Join(", ", existingDbSerials)}] đã tồn tại trong hệ thống. Vui lòng kiểm tra và chỉnh sửa lại phiếu nháp trước khi duyệt.");
                    }
                }
            }

            // Check duplicate serial numbers within the current document (Question 1 check)
            var duplicateDocumentSerials = allDocumentSerials
                .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicateDocumentSerials.Any())
            {
                throw new Exception($"Các số serial sau bị trùng lặp trong phiếu: [{string.Join(", ", duplicateDocumentSerials)}]. Vui lòng kiểm tra lại trước khi duyệt.");
            }

            stockIn.Status = DocumentStatus.Posted;
            stockIn.PostedBy = userId;
            stockIn.PostedAt = DateTime.Now;
            db.SaveChanges();

            var postingService = new InventoryPostingService(
                new EfInventoryUnitOfWork(db),
                new DbDefaultWarehouseProvider(db),
                new SystemClock());

            // Locking strategy: Order by ProductId to avoid deadlocks
            foreach (var line in stockIn.Lines.OrderBy(l => l.ProductId))
            {
                var serials = lineSerialsMap.TryGetValue(line.Id, out var sns) ? sns.ToArray() : Array.Empty<string>();

                postingService.PostStockIn(new PostStockInCommand(
                    stockIn.Id,
                    stockIn.WarehouseId,
                    StockInKind.Purchase,
                    StockDocumentStatus.Posted,
                    line.ProductId,
                    line.BaseQuantity > 0 ? (int)line.BaseQuantity : (int)line.Quantity,
                    serials,
                    userId));
            }

            // Bind posted serials with LastStockInLineId in database
            foreach (var line in stockIn.Lines)
            {
                var serials = lineSerialsMap.TryGetValue(line.Id, out var sns) ? sns : new List<string>();
                if (serials.Any())
                {
                    var dbSerials = db.ProductSerials.Where(ps => serials.Contains(ps.SerialNumber)).ToList();
                    foreach (var s in dbSerials)
                    {
                        s.LastStockInLineId = line.Id;
                    }
                }
            }
            db.SaveChanges();

            var afterJson = Serialize(stockIn);
            AddAudit(db, "UPDATE", stockIn.Id, beforeJson, afterJson, userId);

            transaction.Commit();
        }

        public static List<string> ParseSerialRange(string input)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(input)) return result;

            var parts = input.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                var rangeMatch = Regex.Match(trimmed, @"^(.+?)(\d+)-[^\d]*(\d+)$");
                if (rangeMatch.Success)
                {
                    string prefix = rangeMatch.Groups[1].Value;
                    string startStr = rangeMatch.Groups[2].Value;
                    string endStr   = rangeMatch.Groups[3].Value;
                    if (long.TryParse(startStr, out long start) && long.TryParse(endStr, out long end) && end >= start)
                    {
                        int padLen = startStr.Length;
                        for (long i = start; i <= end; i++)
                            result.Add(prefix + i.ToString().PadLeft(padLen, '0'));
                    }
                }
                else
                {
                    result.Add(trimmed);
                }
            }
            return result;
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

        public virtual void Delete(int id, int userId)
        {
            using var db = _contextFactory();
            var stockIn = db.StockIns
                .Include(s => s.Lines)
                .FirstOrDefault(s => s.Id == id);

            if (stockIn == null) throw new Exception("Không tìm thấy phiếu nhập kho.");
            if (stockIn.Status == DocumentStatus.Posted || stockIn.Status == "đã ghi sổ")
                throw new Exception("Không thể xóa phiếu đã ghi sổ.");

            var beforeJson = JsonSerializer.Serialize(new { stockIn.Id, stockIn.DocumentCode });

            db.StockInLines.RemoveRange(stockIn.Lines);
            db.StockIns.Remove(stockIn);
            db.SaveChanges();

            AddAudit(db, "DELETE", id, beforeJson, null, userId);
        }

        private string Serialize(StockIn s)
        {
            return JsonSerializer.Serialize(new
            {
                s.Id,
                s.DocumentCode,
                s.WarehouseId,
                s.SupplierId,
                s.ImportDate,
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
                EntityName = "StockIn",
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
