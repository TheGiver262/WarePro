using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using System.Text.Json;

namespace QuanLyHangHoa.Services
{
    public class SupplierService
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly DatabaseWriteExecutor _writeExecutor;

        public SupplierService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            _writeExecutor = new DatabaseWriteExecutor(contextFactory);
        }

        // query chỉ đọc và sắp theo mã giúp kết quả ổn định giữa các lần tải
        public List<Supplier> GetAll()
        {
            using var db = _contextFactory();
            return db.Suppliers.AsNoTracking().OrderBy(s => s.SupplierCode).ToList();
        }

        // dữ liệu chính và audit dùng cùng context, cùng transaction
        public Task UpdateAsync(
            int id, Supplier updated, byte[] expectedRowVersion, int performedBy,
            Guid operationId, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(expectedRowVersion);
            var rowVersion = expectedRowVersion.ToArray();
            var code = updated.SupplierCode.Trim();
            var name = updated.DisplayName.Trim();
            var phone = updated.Phone?.Trim();
            var email = updated.Email?.Trim();
            var address = updated.Address?.Trim();
            var isActive = updated.IsActive;

            return _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("supplier.update", operationId),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(db, performedBy, PermissionAction.ManageMasterData);
                    var entity = await db.Suppliers.SingleOrDefaultAsync(item => item.Id == id, token);
                    if (entity is null) return;
                    db.Entry(entity).Property(item => item.RowVersion).OriginalValue = rowVersion;
                    var before = Serialize(entity);
                    entity.SupplierCode = code;
                    entity.DisplayName = name;
                    entity.Phone = phone;
                    entity.Email = email;
                    entity.Address = address;
                    entity.IsActive = isActive;
                    AddAuditEntry(db, "UPDATE", id, before, Serialize(entity), performedBy);
                },
                (db, token) => db.Suppliers.AnyAsync(item => item.Id == id &&
                    item.SupplierCode == code && item.DisplayName == name &&
                    item.Phone == phone && item.Email == email && item.Address == address &&
                    item.IsActive == isActive && item.RowVersion != rowVersion, token),
                cancellationToken: cancellationToken);
        }

        private static void AddAuditEntry(
            AppDbContext db, string action, int entityId,
            string? before, string? after, int performedBy) =>
            db.AuditLogs.Add(new AuditLog
            {
                EntityName = "Supplier",
                EntityId = entityId,
                ActionCode = action,
                BeforeJson = before,
                AfterJson = after,
                PerformedBy = performedBy,
                PerformedAt = DateTime.Now
            });
        public Task<int> AddAsync(
            Supplier supplier, int performedBy, Guid operationId,
            CancellationToken cancellationToken = default)
        {
            var code = supplier.SupplierCode.Trim();
            var name = supplier.DisplayName.Trim();
            var phone = supplier.Phone?.Trim();
            var email = supplier.Email?.Trim();
            var address = supplier.Address?.Trim();
            var isActive = supplier.IsActive;

            return _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("supplier.add", operationId),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(db, performedBy, PermissionAction.ManageMasterData);
                    if (await db.Suppliers.AnyAsync(item => item.SupplierCode == code, token))
                    {
                        throw new InvalidOperationException($"Supplier code '{code}' already exists.");
                    }

                    var created = new Supplier
                    {
                        SupplierCode = code,
                        DisplayName = name,
                        Phone = phone,
                        Email = email,
                        Address = address,
                        IsActive = isActive
                    };
                    db.Suppliers.Add(created);
                    await db.SaveChangesAsync(token);
                    AddAuditEntry(db, "CREATE", created.Id, null, Serialize(created), performedBy);
                    return created.Id;
                },
                (db, token) => db.Suppliers.AnyAsync(item =>
                    item.SupplierCode == code && item.DisplayName == name &&
                    item.Phone == phone && item.Email == email && item.Address == address &&
                    item.IsActive == isActive, token),
                cancellationToken: cancellationToken);
        }

        public Task DeleteAsync(
            int id, byte[] expectedRowVersion, int performedBy, Guid operationId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(expectedRowVersion);
            var rowVersion = expectedRowVersion.ToArray();

            return _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("supplier.delete", operationId),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(db, performedBy, PermissionAction.ManageMasterData);
                    var entity = await db.Suppliers.SingleOrDefaultAsync(item => item.Id == id, token);
                    if (entity is null) return;
                    db.Entry(entity).Property(item => item.RowVersion).OriginalValue = rowVersion;
                    var before = Serialize(entity);
                    var hasDependencies =
                        await db.PurchaseInvoices.AnyAsync(item => item.SupplierId == id, token) ||
                        await db.StockIns.AnyAsync(item => item.SupplierId == id, token);
                    if (hasDependencies)
                    {
                        entity.IsActive = false;
                        AddAuditEntry(db, "DEACTIVATE", id, before, Serialize(entity), performedBy);
                    }
                    else
                    {
                        db.Suppliers.Remove(entity);
                        AddAuditEntry(db, "DELETE", id, before, null, performedBy);
                    }
                },
                (db, token) => db.Suppliers.AllAsync(item => item.Id != id || !item.IsActive, token),
                cancellationToken: cancellationToken);
        }
        private static string Serialize(Supplier s)
        {
            return JsonSerializer.Serialize(new { s.Id, s.SupplierCode, s.DisplayName, s.Phone, s.Email, s.Address, s.IsActive });
        }

    }
}
