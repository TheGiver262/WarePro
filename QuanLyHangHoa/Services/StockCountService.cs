using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    /// <summary>
    /// chốt kiểm kê bằng các phiếu điều chỉnh liên kết, không sửa StockBalance trực tiếp.
    /// </summary>
    public partial class StockCountService
    {
        private const string CountedStatus = "đã kiểm kê";
        private const string CompletedStatus = "hoàn thành";
        private readonly Func<AppDbContext> _contextFactory;
        private readonly DatabaseWriteExecutor _writeExecutor;

        public StockCountService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            _writeExecutor = new DatabaseWriteExecutor(contextFactory);
        }

        public async Task CreateAsync(
            StockCountSession session,
            int userId,
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(session);
            // chụp cả session và line để lần retry tạo lại graph mới, không dùng entity đã có id tạm hoặc tracking state cũ.
            var snapshot = new CreateSessionSnapshot(
                session.SessionCode,
                session.WarehouseId,
                session.Status,
                session.CountDate,
                session.Notes,
                session.Lines.Select(line => new CreateLineSnapshot(
                    line.ProductId,
                    line.SystemQuantity,
                    line.CountedQuantity,
                    line.VarianceQuantity,
                    line.SerialNumbers)).ToArray());

            var sessionId = await _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("stock-count.create", operationId),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(db, userId, PermissionAction.PostStockAdjustment);
                    var freshSession = snapshot.ToEntity(userId);
                    db.StockCountSessions.Add(freshSession);
                    // flush để lấy id phiên trước khi audit; vẫn cùng transaction nên lỗi audit sẽ rollback cả phiên.
                    await db.SaveChangesAsync(token);
                    AddAudit(db, "CREATE", freshSession.Id, null, Serialize(freshSession), userId);
                    return freshSession.Id;
                },
                entityKey: snapshot.SessionCode,
                cancellationToken: cancellationToken);
            session.Id = sessionId;
        }

        private sealed record CreateSessionSnapshot(
            string SessionCode,
            int WarehouseId,
            string Status,
            DateTime CountDate,
            string? Notes,
            CreateLineSnapshot[] Lines)
        {
            public StockCountSession ToEntity(int userId) => new()
            {
                SessionCode = SessionCode,
                WarehouseId = WarehouseId,
                Status = Status,
                CountDate = CountDate,
                Notes = Notes,
                CreatedBy = userId,
                Lines = Lines.Select(line => line.ToEntity()).ToList()
            };
        }

        private sealed record CreateLineSnapshot(
            int ProductId,
            decimal SystemQuantity,
            decimal CountedQuantity,
            decimal VarianceQuantity,
            string? SerialNumbers)
        {
            public StockCountLine ToEntity() => new()
            {
                ProductId = ProductId,
                SystemQuantity = SystemQuantity,
                CountedQuantity = CountedQuantity,
                VarianceQuantity = VarianceQuantity,
                SerialNumbers = SerialNumbers
            };
        }
        internal void CreateSession(StockCountSession session, int userId) =>
            CreateAsync(session, userId, Guid.NewGuid()).GetAwaiter().GetResult();

        public Task ProcessResultsAsync(
            int sessionId, int userId, Guid operationId,
            CancellationToken cancellationToken = default) =>
            _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("stock-count.process-results", operationId),
                (db, token) => StageProcessResultsAsync(db, sessionId, userId, token),
                // trạng thái hoàn thành xác nhận commit khi phản hồi commit bị mất, tránh báo lỗi dù nghiệp vụ đã lưu.
                (db, token) => db.StockCountSessions.AnyAsync(
                    item => item.Id == sessionId && item.Status == CompletedStatus, token),
                cancellationToken: cancellationToken);

        internal void ProcessResults(int sessionId, int userId) =>
            ProcessResultsAsync(sessionId, userId, Guid.NewGuid()).GetAwaiter().GetResult();

        private async Task StageProcessResultsAsync(
            AppDbContext db, int sessionId, int userId, CancellationToken cancellationToken)
        {
            var actor = AuthorizationService.RequireFreshActor(db, userId, PermissionAction.PostStockAdjustment);

            var session = db.StockCountSessions
                .Include(item => item.Lines)
                .SingleOrDefault(item => item.Id == sessionId)
                ?? throw new InventoryDomainException("Không tìm thấy phiên kiểm kê.");

            // phiên đã hoàn thành trả ngay để thao tác gọi lại không tạo thêm chứng từ điều chỉnh.
            if (session.Status == CompletedStatus)
            {
                return;
            }

            if (session.Status != CountedStatus)
            {
                throw new InventoryDomainException("Chỉ phiên đã kiểm kê mới được xử lý chênh lệch.");
            }

            if (!AuthorizationService.CanPerform(actor, PermissionAction.ApproveStock))
            {
                throw new InventoryDomainException("You are not authorized to approve stock documents.");
            }

            // kiểm tra liên kết là lớp idempotency thứ hai ngoài status, phòng lần chạy trước dở ở client.
            if (db.StockIns.Any(item => item.StockCountSessionId == sessionId) ||
                db.StockOuts.Any(item => item.StockCountSessionId == sessionId))
            {
                throw new InventoryDomainException("Phiên kiểm kê đã có phiếu điều chỉnh liên kết.");
            }

            var beforeJson = Serialize(session);
            foreach (var line in session.Lines)
            {
                if (line.CountedQuantity < 0)
                {
                    throw new InventoryDomainException("Số lượng kiểm kê thực tế không được âm.");
                }

                // variance dương nghĩa là thiếu trên hệ thống cần nhập; âm nghĩa là thừa hệ thống cần xuất.
                line.VarianceQuantity = line.CountedQuantity - line.SystemQuantity;
            }

            // chốt và sắp line trước; tất cả sản phẩm, quantity, serial phải hợp lệ trước khi tạo chứng từ đầu tiên.
            var correctionLines = session.Lines
                .Where(item => item.VarianceQuantity != 0)
                .OrderBy(item => item.Id)
                .ToList();
            var productIds = correctionLines.Select(item => item.ProductId).Distinct().ToArray();
            var products = db.Products
                .Where(item => productIds.Contains(item.Id))
                .ToDictionary(item => item.Id);

            foreach (var line in correctionLines)
            {
                if (!products.TryGetValue(line.ProductId, out var product))
                {
                    throw new InventoryDomainException($"Product {line.ProductId} does not exist.");
                }

                var quantity = Math.Abs(line.VarianceQuantity);
                var serialNumbers = ParseSerials(line.SerialNumbers);
                ValidateCorrectionLine(product, quantity, serialNumbers);
            }

            // phiếu xuất điều chỉnh cần customer bắt buộc nên dùng một bản ghi hệ thống ổn định.
            Customer? adjustmentCustomer = null;
            if (correctionLines.Any(item => item.VarianceQuantity < 0))
            {
                adjustmentCustomer = db.Customers.SingleOrDefault(item => item.CustomerCode == "CUS-ADJ");
                if (adjustmentCustomer == null)
                {
                    adjustmentCustomer = new Customer
                    {
                        CustomerCode = "CUS-ADJ",
                        DisplayName = "Khách hàng điều chỉnh (Hệ thống)",
                        IsActive = true
                    };
                    // lưu customer hệ thống trước để có id bắt buộc cho các phiếu xuất điều chỉnh tạo ở pha sau.
                    db.Customers.Add(adjustmentCustomer);
                    await db.SaveChangesAsync(cancellationToken);
                }
            }

            // cùng một timestamp và warehouse được dùng cho mọi chứng từ sinh từ phiên kiểm kê.
            var now = DateTime.UtcNow;
            var postingService = new InventoryPostingService(
                new EfInventoryUnitOfWork(db, commitChanges: false),
                new FixedWarehouseProvider(session.WarehouseId),
                new UtcClock());

            foreach (var line in correctionLines)
            {
                var product = products[line.ProductId];
                var quantity = Math.Abs(line.VarianceQuantity);
                var serialNumbers = ParseSerials(line.SerialNumbers);

                if (line.VarianceQuantity > 0)
                {
                    await PostStockInCorrectionAsync(
                        db,
                        postingService,
                        session,
                        line,
                        product,
                        quantity,
                        serialNumbers,
                        userId,
                        now,
                        cancellationToken);
                }
                else
                {
                    await PostStockOutCorrectionAsync(
                        db,
                        postingService,
                        session,
                        line,
                        product,
                        adjustmentCustomer!.Id,
                        quantity,
                        serialNumbers,
                        userId,
                        now,
                        cancellationToken);
                }
            }

            session.Status = CompletedStatus;
            session.ApprovedBy = userId;
            session.ApprovedAt = now;
            session.PostedBy = userId;
            session.PostedAt = now;
            AddAudit(db, "POST", session.Id, beforeJson, Serialize(session), userId);
        }

        // mỗi line thiếu tạo một phiếu nhập có khóa liên kết session/line để database chống tạo lặp.
        private static async Task PostStockInCorrectionAsync(
            AppDbContext db,
            InventoryPostingService postingService,
            StockCountSession session,
            StockCountLine countLine,
            Product product,
            decimal quantity,
            IReadOnlyCollection<string> serialNumbers,
            int userId,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var documentLine = new StockInLine
            {
                ProductId = countLine.ProductId,
                UnitId = product.DefaultUnitId,
                Quantity = quantity,
                BaseQuantity = quantity,
                UnitPrice = product.CostPrice ?? product.DefaultPrice,
                DraftSerials = serialNumbers.Count == 0 ? null : string.Join(",", serialNumbers)
            };
            var document = new StockIn
            {
                DocumentCode = $"SI-ADJ-{session.SessionCode}-{countLine.Id}",
                WarehouseId = session.WarehouseId,
                ImportDate = now,
                Notes = $"Nhập để điều chỉnh tồn kho (Theo phiên kiểm kê {session.SessionCode})",
                PurposeCode = "Adjustment",
                Status = StockDocumentStatus.Approved.ToString(),
                CreatedBy = userId,
                CreatedAt = now,
                ApprovedBy = userId,
                ApprovedAt = now,
                PostedBy = userId,
                PostedAt = now,
                StockCountSessionId = session.Id,
                StockCountLineId = countLine.Id,
                Lines = new List<StockInLine> { documentLine }
            };
            // flush header và line trước posting vì ledger cần document.id, còn serial cần documentLine.id để liên kết.
            db.StockIns.Add(document);
            await db.SaveChangesAsync(cancellationToken);

            postingService.PostStockIn(new PostStockInCommand(
                document.Id,
                session.WarehouseId,
                StockInKind.Adjustment,
                StockDocumentStatus.Approved,
                countLine.ProductId,
                quantity,
                serialNumbers,
                userId,
                StockInLineId: documentLine.Id));

            if (serialNumbers.Count > 0)
            {
                var serials = db.ProductSerials.Local
                    .Where(item => serialNumbers.Contains(item.SerialNumber))
                    .ToList();
                foreach (var serial in serials)
                {
                    serial.LastStockInLineId = documentLine.Id;
                }
            }
        }

        // mỗi line thừa tạo một phiếu xuất điều chỉnh và đi qua posting service như chứng từ thường.
        private static async Task PostStockOutCorrectionAsync(
            AppDbContext db,
            InventoryPostingService postingService,
            StockCountSession session,
            StockCountLine countLine,
            Product product,
            int customerId,
            decimal quantity,
            IReadOnlyCollection<string> serialNumbers,
            int userId,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var documentLine = new StockOutLine
            {
                ProductId = countLine.ProductId,
                UnitId = product.DefaultUnitId,
                Quantity = quantity,
                BaseQuantity = quantity,
                UnitPrice = product.CostPrice ?? product.DefaultPrice,
                DraftSerials = serialNumbers.Count == 0 ? null : string.Join(",", serialNumbers)
            };
            var document = new StockOut
            {
                DocumentCode = $"SO-ADJ-{session.SessionCode}-{countLine.Id}",
                CustomerId = customerId,
                WarehouseId = session.WarehouseId,
                ExportDate = now,
                Notes = $"Xuất để điều chỉnh tồn kho (Theo phiên kiểm kê {session.SessionCode})",
                PurposeCode = "Adjustment",
                Status = StockDocumentStatus.Approved.ToString(),
                CreatedBy = userId,
                CreatedAt = now,
                ApprovedBy = userId,
                ApprovedAt = now,
                PostedBy = userId,
                PostedAt = now,
                StockCountSessionId = session.Id,
                StockCountLineId = countLine.Id,
                Lines = new List<StockOutLine> { documentLine }
            };
            // flush header và line trước posting vì ledger cần document.id, còn serial cần documentLine.id để liên kết.
            db.StockOuts.Add(document);
            await db.SaveChangesAsync(cancellationToken);

            postingService.PostStockOut(new PostStockOutCommand(
                document.Id,
                session.WarehouseId,
                StockOutKind.Adjustment,
                StockDocumentStatus.Approved,
                countLine.ProductId,
                quantity,
                serialNumbers,
                userId));

            if (serialNumbers.Count > 0)
            {
                var serials = db.ProductSerials.Local
                    .Where(item => serialNumbers.Contains(item.SerialNumber))
                    .ToList();
                foreach (var serial in serials)
                {
                    serial.LastStockOutLineId = documentLine.Id;
                }
            }
        }

        private static string[] ParseSerials(string? input)
        {
            return StockInService.ParseSerialRange(input ?? string.Empty)
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .ToArray();
        }

        // sản phẩm serial-tracked chỉ chấp nhận quantity nguyên và đúng một serial cho mỗi đơn vị gốc.
        private static void ValidateCorrectionLine(
            Product product,
            decimal quantity,
            IReadOnlyCollection<string> serialNumbers)
        {
            if (!product.IsSerialTracked && serialNumbers.Count > 0)
            {
                throw new InventoryDomainException("Non-serial products cannot receive serial numbers.");
            }

            if (!product.IsSerialTracked)
            {
                return;
            }

            if (quantity != decimal.Truncate(quantity))
            {
                throw new InventoryDomainException(
                    $"Sản phẩm {product.DisplayName} theo dõi serial nên số lượng cơ sở phải là số nguyên.");
            }

            if (serialNumbers.Count != (int)quantity)
            {
                throw new InventoryDomainException(
                    $"Sản phẩm {product.DisplayName} yêu cầu {(int)quantity} serial, nhưng hiện có {serialNumbers.Count}.");
            }
        }

        private static string Serialize(StockCountSession session)
        {
            return JsonSerializer.Serialize(new
            {
                session.Id,
                session.SessionCode,
                session.WarehouseId,
                session.Status,
                session.CountDate,
                session.CreatedBy,
                session.PostedBy,
                session.PostedAt
            }, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All)
            });
        }

        // audit cuối tham gia transaction, nên session chỉ có lịch sử POST khi mọi correction đã thành công.
        private static void AddAudit(
            AppDbContext db,
            string action,
            int entityId,
            string? before,
            string? after,
            int performedBy)
        {
            db.AuditLogs.Add(new AuditLog
            {
                EntityName = "StockCountSession",
                EntityId = entityId,
                ActionCode = action,
                BeforeJson = before,
                AfterJson = after,
                PerformedBy = performedBy,
                PerformedAt = DateTime.UtcNow
            });
        }

        private sealed class FixedWarehouseProvider : IDefaultWarehouseProvider
        {
            private readonly int _warehouseId;

            public FixedWarehouseProvider(int warehouseId)
            {
                _warehouseId = warehouseId;
            }

            public int GetDefaultWarehouseId() => _warehouseId;
        }

        private sealed class UtcClock : IClock
        {
            public DateTime Now => DateTime.UtcNow;
        }
    }
}
