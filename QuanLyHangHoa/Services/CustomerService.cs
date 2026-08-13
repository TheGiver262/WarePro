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
    public class CustomerService
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly DatabaseWriteExecutor _writeExecutor;

        public CustomerService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            _writeExecutor = new DatabaseWriteExecutor(contextFactory);
        }

        // CUS-ADJ là khách kỹ thuật cho nghiệp vụ điều chỉnh, không hiển thị trong danh mục người dùng
        public List<Customer> GetAll()
        {
            using var db = _contextFactory();
            return db.Customers.AsNoTracking().Where(c => c.CustomerCode != "CUS-ADJ").OrderBy(c => c.CustomerCode).ToList();
        }

        // thay đổi khách hàng và audit được khóa trong cùng transaction
        public Task UpdateAsync(
            int id, Customer updated, byte[] expectedRowVersion, int performedBy,
            Guid operationId, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(expectedRowVersion);
            // copy token và scalar trước khi vào executor để mọi lần retry dùng cùng một yêu cầu
            var rowVersion = expectedRowVersion.ToArray();
            var code = updated.CustomerCode.Trim();
            var name = updated.DisplayName.Trim();
            var phone = updated.Phone?.Trim();
            var email = updated.Email?.Trim();
            var address = updated.Address?.Trim();
            var isActive = updated.IsActive;

            return _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("customer.update", operationId),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(db, performedBy, PermissionAction.ManageMasterData);
                    var entity = await db.Customers.SingleOrDefaultAsync(item => item.Id == id, token);
                    if (entity is null)
                        throw new StaleEntityException("Dữ liệu đã bị xóa hoặc không còn tồn tại. Vui lòng tải lại dữ liệu.");
                    db.Entry(entity).Property(item => item.RowVersion).OriginalValue = rowVersion;
                    var before = Serialize(entity);
                    entity.CustomerCode = code;
                    entity.DisplayName = name;
                    entity.Phone = phone;
                    entity.Email = email;
                    entity.Address = address;
                    entity.IsActive = isActive;
                    AddAuditEntry(db, "UPDATE", id, before, Serialize(entity), performedBy);
                },
                (db, token) => db.Customers.AnyAsync(item => item.Id == id &&
                    item.CustomerCode == code && item.DisplayName == name &&
                    item.Phone == phone && item.Email == email && item.Address == address &&
                    item.IsActive == isActive && item.RowVersion != rowVersion, token),
                cancellationToken: cancellationToken);
        }

        private static void AddAuditEntry(
            AppDbContext db, string action, int entityId,
            string? before, string? after, int performedBy) =>
            db.AuditLogs.Add(new AuditLog
            {
                EntityName = "Customer",
                EntityId = entityId,
                ActionCode = action,
                BeforeJson = before,
                AfterJson = after,
                PerformedBy = performedBy,
                PerformedAt = DateTime.Now
            });
        public Task<int> AddAsync(
            Customer customer, int performedBy, Guid operationId,
            CancellationToken cancellationToken = default)
        {
            var code = customer.CustomerCode.Trim();
            var name = customer.DisplayName.Trim();
            var phone = customer.Phone?.Trim();
            var email = customer.Email?.Trim();
            var address = customer.Address?.Trim();
            var isActive = customer.IsActive;

            return _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("customer.add", operationId),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(db, performedBy, PermissionAction.ManageMasterData);
                    // kiểm tra trước để báo lỗi rõ; unique index vẫn là hàng rào cuối nếu hai máy cùng tạo một mã
                    if (await db.Customers.AnyAsync(item => item.CustomerCode == code, token))
                    {
                        throw new InvalidOperationException($"Customer code '{code}' already exists.");
                    }

                    var created = new Customer
                    {
                        CustomerCode = code,
                        DisplayName = name,
                        Phone = phone,
                        Email = email,
                        Address = address,
                        IsActive = isActive
                    };
                    db.Customers.Add(created);
                    // flush lấy id do DB sinh trước khi tạo audit tham chiếu; executor vẫn commit cả hai cùng transaction
                    await db.SaveChangesAsync(token);
                    AddAuditEntry(db, "CREATE", created.Id, null, Serialize(created), performedBy);
                    return created.Id;
                },
                (db, token) => db.Customers.AnyAsync(item =>
                    item.CustomerCode == code && item.DisplayName == name &&
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
                new DatabaseWriteRequest("customer.delete", operationId),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(db, performedBy, PermissionAction.ManageMasterData);
                    var entity = await db.Customers.SingleOrDefaultAsync(item => item.Id == id, token);
                    if (entity is null) return;
                    db.Entry(entity).Property(item => item.RowVersion).OriginalValue = rowVersion;
                    var before = Serialize(entity);
                    var hasDependencies =
                        await db.SalesInvoices.AnyAsync(item => item.CustomerId == id, token) ||
                        await db.StockOuts.AnyAsync(item => item.CustomerId == id, token) ||
                        await db.WarrantyCoverages.AnyAsync(item => item.CustomerId == id, token);
                    if (hasDependencies)
                    {
                        entity.IsActive = false;
                        AddAuditEntry(db, "DEACTIVATE", id, before, Serialize(entity), performedBy);
                    }
                    else
                    {
                        db.Customers.Remove(entity);
                        AddAuditEntry(db, "DELETE", id, before, null, performedBy);
                    }
                },
                (db, token) => db.Customers.AllAsync(item => item.Id != id || !item.IsActive, token),
                cancellationToken: cancellationToken);
        }
        private static string Serialize(Customer c)
        {
            return JsonSerializer.Serialize(new { c.Id, c.CustomerCode, c.DisplayName, c.Phone, c.Email, c.Address, c.IsActive });
        }

    }
}
