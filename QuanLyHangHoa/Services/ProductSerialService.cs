using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    /// <summary>
    /// tra cứu lịch sử/vị trí serial và cập nhật note kèm audit nguyên tử.
    /// </summary>
    public class ProductSerialService
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly DatabaseWriteExecutor _writeExecutor;

        public ProductSerialService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            _writeExecutor = new DatabaseWriteExecutor(contextFactory);
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

        // thay note và audit nằm cùng transaction để không có thay đổi thiếu lịch sử hoặc lịch sử giả.
        public Task UpdateNoteAsync(
            int serialId, string? note, byte[] expectedRowVersion, int userId,
            Guid operationId, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(expectedRowVersion);
            var rowVersion = expectedRowVersion.ToArray();
            var normalizedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

            return _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("product-serial.update-note", operationId),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(db, userId, PermissionAction.ManageMasterData);
                    var serial = await db.ProductSerials.SingleOrDefaultAsync(
                        item => item.Id == serialId,
                        token) ?? throw new InvalidOperationException("Không tìm thấy serial.");
                    db.Entry(serial).Property(item => item.RowVersion).OriginalValue = rowVersion;
                    var beforeJson = System.Text.Json.JsonSerializer.Serialize(new { serial.Note });
                    serial.Note = normalizedNote;
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
                },
                (db, token) => db.ProductSerials.AnyAsync(item =>
                    item.Id == serialId && item.Note == normalizedNote &&
                    item.RowVersion != rowVersion,
                    token),
                cancellationToken: cancellationToken);
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

            // khoảng ngày dựa trên CreatedAt của phiếu nhập gần nhất, không phải ngày sửa note.
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
