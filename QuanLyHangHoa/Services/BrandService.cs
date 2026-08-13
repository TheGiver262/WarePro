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
    public class BrandService
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly DatabaseWriteExecutor _writeExecutor;

        public BrandService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            _writeExecutor = new DatabaseWriteExecutor(contextFactory);
        }

        // AsNoTracking phù hợp danh sách chỉ đọc, tránh EF giữ bản sao không cần thiết trong context
        public List<Brand> GetAll()
        {
            using var db = _contextFactory();
            return db.Brands.AsNoTracking().OrderBy(b => b.BrandCode).ToList();
        }

        // dữ liệu và audit nằm chung transaction để không có thay đổi nào thiếu dấu vết
        public Task UpdateAsync(
            int id, Brand updated, byte[] expectedRowVersion, int performedBy,
            Guid operationId, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(expectedRowVersion);
            // copy token và scalar trước khi vào executor để mọi lần retry dùng cùng một yêu cầu
            var rowVersion = expectedRowVersion.ToArray();
            var code = updated.BrandCode.Trim();
            var name = updated.DisplayName.Trim();
            var country = updated.OriginCountry?.Trim();
            var isActive = updated.IsActive;

            return _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("brand.update", operationId),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(db, performedBy, PermissionAction.ManageMasterData);
                    var entity = await db.Brands.SingleOrDefaultAsync(item => item.Id == id, token);
                    if (entity is null)
                        throw new StaleEntityException("Dữ liệu đã bị xóa hoặc không còn tồn tại. Vui lòng tải lại dữ liệu.");
                    db.Entry(entity).Property(item => item.RowVersion).OriginalValue = rowVersion;
                    var before = Serialize(entity);
                    entity.BrandCode = code;
                    entity.DisplayName = name;
                    entity.OriginCountry = country;
                    entity.IsActive = isActive;
                    AddAuditEntry(db, "UPDATE", id, before, Serialize(entity), performedBy);
                },
                (db, token) => db.Brands.AnyAsync(item => item.Id == id &&
                    item.BrandCode == code && item.DisplayName == name &&
                    item.OriginCountry == country && item.IsActive == isActive &&
                    item.RowVersion != rowVersion, token),
                cancellationToken: cancellationToken);
        }

        private static void AddAuditEntry(
            AppDbContext db, string action, int entityId,
            string? before, string? after, int performedBy) =>
            db.AuditLogs.Add(new AuditLog
            {
                EntityName = "Brand",
                EntityId = entityId,
                ActionCode = action,
                BeforeJson = before,
                AfterJson = after,
                PerformedBy = performedBy,
                PerformedAt = DateTime.Now
            });
        public Task<int> AddAsync(
            Brand brand, int performedBy, Guid operationId,
            CancellationToken cancellationToken = default)
        {
            var code = brand.BrandCode.Trim();
            var name = brand.DisplayName.Trim();
            var country = brand.OriginCountry?.Trim();
            var isActive = brand.IsActive;

            return _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("brand.add", operationId),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(db, performedBy, PermissionAction.ManageMasterData);
                    // kiểm tra trước để báo lỗi rõ; unique index vẫn là hàng rào cuối nếu hai máy cùng tạo một mã
                    if (await db.Brands.AnyAsync(item => item.BrandCode == code, token))
                    {
                        throw new InvalidOperationException($"Brand code '{code}' already exists.");
                    }

                    var created = new Brand
                    {
                        BrandCode = code,
                        DisplayName = name,
                        OriginCountry = country,
                        IsActive = isActive
                    };
                    db.Brands.Add(created);
                    // flush lấy id do DB sinh trước khi tạo audit tham chiếu; executor vẫn commit cả hai cùng transaction
                    await db.SaveChangesAsync(token);
                    AddAuditEntry(db, "CREATE", created.Id, null, Serialize(created), performedBy);
                    return created.Id;
                },
                (db, token) => db.Brands.AnyAsync(item =>
                    item.BrandCode == code && item.DisplayName == name &&
                    item.OriginCountry == country && item.IsActive == isActive, token),
                cancellationToken: cancellationToken);
        }

        public Task DeleteAsync(
            int id, byte[] expectedRowVersion, int performedBy, Guid operationId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(expectedRowVersion);
            var rowVersion = expectedRowVersion.ToArray();

            return _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("brand.delete", operationId),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(db, performedBy, PermissionAction.ManageMasterData);
                    var entity = await db.Brands.SingleOrDefaultAsync(item => item.Id == id, token);
                    if (entity is null) return;
                    db.Entry(entity).Property(item => item.RowVersion).OriginalValue = rowVersion;
                    var before = Serialize(entity);
                    if (await db.Products.AnyAsync(product => product.BrandId == id, token))
                    {
                        entity.IsActive = false;
                        AddAuditEntry(db, "DEACTIVATE", id, before, Serialize(entity), performedBy);
                    }
                    else
                    {
                        db.Brands.Remove(entity);
                        AddAuditEntry(db, "DELETE", id, before, null, performedBy);
                    }
                },
                (db, token) => db.Brands.AllAsync(item => item.Id != id || !item.IsActive, token),
                cancellationToken: cancellationToken);
        }
        private static string Serialize(Brand b)
        {
            return JsonSerializer.Serialize(new { b.Id, b.BrandCode, b.DisplayName, b.OriginCountry, b.IsActive });
        }

    }
}
