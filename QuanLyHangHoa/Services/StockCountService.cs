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
    /// <summary>
    /// chốt kiểm kê bằng các phiếu điều chỉnh liên kết, không sửa StockBalance trực tiếp.
    /// </summary>
    public partial class StockCountService
    {
        private const string CountedStatus = "đã kiểm kê";
        private const string CompletedStatus = "hoàn thành";
        private readonly Func<AppDbContext> _contextFactory;

        public StockCountService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public void CreateSession(StockCountSession session, int userId)
        {
            using var db = _contextFactory();
            AuthorizationService.RequireFreshActor(db, userId, PermissionAction.PostStockAdjustment);
            session.CreatedBy = userId;
            db.StockCountSessions.Add(session);
            db.SaveChanges();
            AddAudit(db, "CREATE", session.Id, null, Serialize(session), session.CreatedBy);
        }

        // transaction bao phủ session, chứng từ bù, posting, serial và audit của toàn bộ kết quả.
        public void ProcessResults(int sessionId, int userId)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();
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
                    db.Customers.Add(adjustmentCustomer);
                    db.SaveChanges();
                }
            }

            // cùng một timestamp và warehouse được dùng cho mọi chứng từ sinh từ phiên kiểm kê.
            var now = DateTime.UtcNow;
            var postingService = new InventoryPostingService(
                new EfInventoryUnitOfWork(db),
                new FixedWarehouseProvider(session.WarehouseId),
                new UtcClock());

            foreach (var line in correctionLines)
            {
                var product = products[line.ProductId];
                var quantity = Math.Abs(line.VarianceQuantity);
                var serialNumbers = ParseSerials(line.SerialNumbers);

                if (line.VarianceQuantity > 0)
                {
                    PostStockInCorrection(
                        db,
                        postingService,
                        session,
                        line,
                        product,
                        quantity,
                        serialNumbers,
                        userId,
                        now);
                }
                else
                {
                    PostStockOutCorrection(
                        db,
                        postingService,
                        session,
                        line,
                        product,
                        adjustmentCustomer!.Id,
                        quantity,
                        serialNumbers,
                        userId,
                        now);
                }
            }

            session.Status = CompletedStatus;
            session.ApprovedBy = userId;
            session.ApprovedAt = now;
            session.PostedBy = userId;
            session.PostedAt = now;
            db.SaveChanges();
            AddAudit(db, "POST", session.Id, beforeJson, Serialize(session), userId);
            transaction.Commit();
        }

        // mỗi line thiếu tạo một phiếu nhập có khóa liên kết session/line để database chống tạo lặp.
        private static void PostStockInCorrection(
            AppDbContext db,
            InventoryPostingService postingService,
            StockCountSession session,
            StockCountLine countLine,
            Product product,
            decimal quantity,
            IReadOnlyCollection<string> serialNumbers,
            int userId,
            DateTime now)
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
            db.StockIns.Add(document);
            db.SaveChanges();

            postingService.PostStockIn(new PostStockInCommand(
                document.Id,
                session.WarehouseId,
                StockInKind.Adjustment,
                StockDocumentStatus.Approved,
                countLine.ProductId,
                quantity,
                serialNumbers,
                userId));

            if (serialNumbers.Count > 0)
            {
                var serials = db.ProductSerials
                    .Where(item => serialNumbers.Contains(item.SerialNumber))
                    .ToList();
                foreach (var serial in serials)
                {
                    serial.LastStockInLineId = documentLine.Id;
                }
                db.SaveChanges();
            }
        }

        // mỗi line thừa tạo một phiếu xuất điều chỉnh và đi qua posting service như chứng từ thường.
        private static void PostStockOutCorrection(
            AppDbContext db,
            InventoryPostingService postingService,
            StockCountSession session,
            StockCountLine countLine,
            Product product,
            int customerId,
            decimal quantity,
            IReadOnlyCollection<string> serialNumbers,
            int userId,
            DateTime now)
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
            db.StockOuts.Add(document);
            db.SaveChanges();

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
                var serials = db.ProductSerials
                    .Where(item => serialNumbers.Contains(item.SerialNumber))
                    .ToList();
                foreach (var serial in serials)
                {
                    serial.LastStockOutLineId = documentLine.Id;
                }
                db.SaveChanges();
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
            db.SaveChanges();
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
