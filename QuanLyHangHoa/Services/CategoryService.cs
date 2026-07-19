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
    public class CategoryService
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly DatabaseWriteExecutor _writeExecutor;

        public CategoryService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            _writeExecutor = new DatabaseWriteExecutor(contextFactory);
        }

        // danh sách được sắp theo mã để màn hình và file xuất có thứ tự ổn định
        public List<Category> GetAll()
        {
            using var db = _contextFactory();
            return db.Categories.AsNoTracking().OrderBy(c => c.CategoryCode).ToList();
        }

        public Task<int> AddAsync(
            Category category,
            int performedBy,
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            var code = category.CategoryCode.Trim();
            var name = category.DisplayName.Trim();
            var isActive = category.IsActive;

            return _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("category.add", operationId),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(db, performedBy, PermissionAction.ManageMasterData);
                    // kiểm tra trước để báo lỗi rõ; unique index vẫn là hàng rào cuối nếu hai máy cùng tạo một mã
                    if (await db.Categories.AnyAsync(item => item.CategoryCode == code, token))
                    {
                        throw new InvalidOperationException($"Category code '{code}' already exists.");
                    }

                    var created = new Category { CategoryCode = code, DisplayName = name, IsActive = isActive };
                    db.Categories.Add(created);
                    // flush lấy id do DB sinh trước khi tạo audit tham chiếu; executor vẫn commit cả hai cùng transaction
                    await db.SaveChangesAsync(token);
                    AddAudit(db, "CREATE", created.Id, null, Serialize(created), performedBy);
                    return created.Id;
                },
                (db, token) => db.Categories.AnyAsync(
                    item => item.CategoryCode == code && item.DisplayName == name && item.IsActive == isActive,
                    token),
                cancellationToken: cancellationToken);
        }

        public Task UpdateAsync(
            int id,
            Category updated,
            byte[] expectedRowVersion,
            int performedBy,
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(expectedRowVersion);
            // copy token và scalar trước khi vào executor để mọi lần retry dùng cùng một yêu cầu
            var rowVersion = expectedRowVersion.ToArray();
            var code = updated.CategoryCode.Trim();
            var name = updated.DisplayName.Trim();
            var isActive = updated.IsActive;

            return _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("category.update", operationId),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(db, performedBy, PermissionAction.ManageMasterData);
                    var entity = await db.Categories.SingleOrDefaultAsync(item => item.Id == id, token);
                    if (entity is null)
                    {
                        return;
                    }

                    db.Entry(entity).Property(item => item.RowVersion).OriginalValue = rowVersion;
                    var before = Serialize(entity);
                    entity.CategoryCode = code;
                    entity.DisplayName = name;
                    entity.IsActive = isActive;
                    AddAudit(db, "UPDATE", id, before, Serialize(entity), performedBy);
                },
                (db, token) => db.Categories.AnyAsync(
                    item => item.Id == id && item.CategoryCode == code &&
                        item.DisplayName == name && item.IsActive == isActive &&
                        item.RowVersion != rowVersion,
                    token),
                cancellationToken: cancellationToken);
        }

        public Task DeleteAsync(
            int id,
            byte[] expectedRowVersion,
            int performedBy,
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(expectedRowVersion);
            var rowVersion = expectedRowVersion.ToArray();

            return _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("category.delete", operationId),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(db, performedBy, PermissionAction.ManageMasterData);
                    var entity = await db.Categories.SingleOrDefaultAsync(item => item.Id == id, token);
                    if (entity is null)
                    {
                        return;
                    }

                    db.Entry(entity).Property(item => item.RowVersion).OriginalValue = rowVersion;
                    var before = Serialize(entity);
                    if (await db.Products.AnyAsync(product => product.CategoryId == id, token))
                    {
                        entity.IsActive = false;
                        AddAudit(db, "DEACTIVATE", id, before, Serialize(entity), performedBy);
                    }
                    else
                    {
                        db.Categories.Remove(entity);
                        AddAudit(db, "DELETE", id, before, null, performedBy);
                    }
                },
                (db, token) => db.Categories.AllAsync(item => item.Id != id || !item.IsActive, token),
                cancellationToken: cancellationToken);
        }

        private static string Serialize(Category c)
        {
            return JsonSerializer.Serialize(new { c.Id, c.CategoryCode, c.DisplayName, c.IsActive });
        }

        // lưu người thao tác và hai trạng thái để có thể đối chiếu khi cần
        private static void AddAudit(AppDbContext db, string action, int entityId, string? before, string? after, int performedBy)
        {
            db.AuditLogs.Add(new AuditLog
            {
                EntityName = "Category",
                EntityId = entityId,
                ActionCode = action,
                BeforeJson = before,
                AfterJson = after,
                PerformedBy = performedBy,
                PerformedAt = DateTime.Now
            });
        }
    }
}
