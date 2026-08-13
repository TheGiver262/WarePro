using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using System.Text.Json;

namespace QuanLyHangHoa.Services
{
    /// <summary>
    /// quản lý đơn vị đo và giữ an toàn lịch sử bằng cách deactivate khi đã có dữ liệu tham chiếu.
    /// </summary>
    public class UnitService
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly DatabaseWriteExecutor _writeExecutor;

        public UnitService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            _writeExecutor = new DatabaseWriteExecutor(contextFactory);
        }

        public List<Unit> GetAll()
        {
            using var db = _contextFactory();
            return db.Units.AsNoTracking().OrderBy(u => u.DisplayName).ToList();
        }

        public Task UpdateAsync(
            int id, Unit updated, byte[] expectedRowVersion, int performedBy,
            Guid operationId, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(expectedRowVersion);
            // copy token và scalar trước khi vào executor để mọi lần retry dùng cùng một yêu cầu
            var rowVersion = expectedRowVersion.ToArray();
            var code = updated.UnitCode.Trim();
            var name = updated.DisplayName.Trim();
            var isActive = updated.IsActive;

            return _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("unit.update", operationId),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(db, performedBy, PermissionAction.ManageMasterData);
                    var entity = await db.Units.SingleOrDefaultAsync(item => item.Id == id, token);
                    if (entity is null)
                        throw new StaleEntityException("Dữ liệu đã bị xóa hoặc không còn tồn tại. Vui lòng tải lại dữ liệu.");
                    db.Entry(entity).Property(item => item.RowVersion).OriginalValue = rowVersion;
                    var before = Serialize(entity);
                    entity.UnitCode = code;
                    entity.DisplayName = name;
                    entity.IsActive = isActive;
                    AddAuditEntry(db, "UPDATE", id, before, Serialize(entity), performedBy);
                },
                (db, token) => db.Units.AnyAsync(item => item.Id == id &&
                    item.UnitCode == code && item.DisplayName == name &&
                    item.IsActive == isActive && item.RowVersion != rowVersion, token),
                cancellationToken: cancellationToken);
        }

        private static void AddAuditEntry(
            AppDbContext db, string action, int entityId,
            string? before, string? after, int performedBy) =>
            db.AuditLogs.Add(new AuditLog
            {
                EntityName = "Unit",
                EntityId = entityId,
                ActionCode = action,
                BeforeJson = before,
                AfterJson = after,
                PerformedBy = performedBy,
                PerformedAt = DateTime.Now
            });
        public Task<int> AddAsync(
            Unit unit, int performedBy, Guid operationId,
            CancellationToken cancellationToken = default)
        {
            var code = unit.UnitCode.Trim();
            var name = unit.DisplayName.Trim();
            var isActive = unit.IsActive;

            return _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("unit.add", operationId),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(db, performedBy, PermissionAction.ManageMasterData);
                    // kiểm tra trước để báo lỗi rõ; unique index vẫn là hàng rào cuối nếu hai máy cùng tạo một mã
                    if (await db.Units.AnyAsync(item => item.UnitCode == code, token))
                    {
                        throw new InventoryDomainException($"Mã đơn vị '{code}' đã tồn tại.");
                    }

                    var created = new Unit { UnitCode = code, DisplayName = name, IsActive = isActive };
                    db.Units.Add(created);
                    // flush lấy id do DB sinh trước khi tạo audit tham chiếu; executor vẫn commit cả hai cùng transaction
                    await db.SaveChangesAsync(token);
                    AddAuditEntry(db, "CREATE", created.Id, null, Serialize(created), performedBy);
                    return created.Id;
                },
                (db, token) => db.Units.AnyAsync(item =>
                    item.UnitCode == code && item.DisplayName == name && item.IsActive == isActive,
                    token),
                cancellationToken: cancellationToken);
        }
        public IReadOnlyList<(string Name, int Count)> GetDependencies(int unitId)
        {
            using var db = _contextFactory();
            return GetDependencies(db, unitId);
        }

        // kiểm tra cả master data và line chứng từ vì mọi quan hệ đều dùng delete restriction.
        private static IReadOnlyList<(string Name, int Count)> GetDependencies(
            AppDbContext db,
            int unitId)
        {
            return new List<(string Name, int Count)>
            {
                ("Product", db.Products.Count(row => row.DefaultUnitId == unitId)),
                ("ProductUnit", db.ProductUnits.Count(row => row.UnitId == unitId)),
                ("PurchaseInvoiceLine", db.PurchaseInvoiceLines.Count(row => row.UnitId == unitId)),
                ("SalesInvoiceLine", db.SalesInvoiceLines.Count(row => row.UnitId == unitId)),
                ("StockInLine", db.StockInLines.Count(row => row.UnitId == unitId)),
                ("StockOutLine", db.StockOutLines.Count(row => row.UnitId == unitId)),
                ("StockTransferLine", db.StockTransferLines.Count(row => row.UnitId == unitId))
            };
        }

        public Task DeleteAsync(
            int id, byte[] expectedRowVersion, int performedBy, Guid operationId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(expectedRowVersion);
            var rowVersion = expectedRowVersion.ToArray();

            return _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("unit.delete", operationId),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(db, performedBy, PermissionAction.ManageMasterData);
                    var entity = await db.Units.SingleOrDefaultAsync(item => item.Id == id, token);
                    if (entity is null) return;
                    db.Entry(entity).Property(item => item.RowVersion).OriginalValue = rowVersion;
                    var before = Serialize(entity);
                    var hasDependencies =
                        await db.Products.AnyAsync(item => item.DefaultUnitId == id, token) ||
                        await db.ProductUnits.AnyAsync(item => item.UnitId == id, token) ||
                        await db.PurchaseInvoiceLines.AnyAsync(item => item.UnitId == id, token) ||
                        await db.SalesInvoiceLines.AnyAsync(item => item.UnitId == id, token) ||
                        await db.StockInLines.AnyAsync(item => item.UnitId == id, token) ||
                        await db.StockOutLines.AnyAsync(item => item.UnitId == id, token) ||
                        await db.StockTransferLines.AnyAsync(item => item.UnitId == id, token);
                    if (hasDependencies)
                    {
                        entity.IsActive = false;
                        AddAuditEntry(db, "DEACTIVATE", id, before, Serialize(entity), performedBy);
                    }
                    else
                    {
                        db.Units.Remove(entity);
                        AddAuditEntry(db, "DELETE", id, before, null, performedBy);
                    }
                },
                (db, token) => db.Units.AllAsync(item => item.Id != id || !item.IsActive, token),
                cancellationToken: cancellationToken);
        }
        private static string Serialize(Unit u)
        {
            return JsonSerializer.Serialize(new { u.Id, u.DisplayName, u.IsActive });
        }

    }
}
