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
    public class StockCountService
    {
        private readonly Func<AppDbContext> _contextFactory;


        public StockCountService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public void CreateSession(StockCountSession session)
        {
            using var db = _contextFactory();
            db.StockCountSessions.Add(session);
            db.SaveChanges();
            AddAudit(db, "CREATE", session.Id, null, Serialize(session), session.CreatedBy);
        }

        public void ProcessResults(int sessionId, int userId)
        {
            using var db = _contextFactory();
            var session = db.StockCountSessions
                .Include(s => s.Lines)
                .FirstOrDefault(s => s.Id == sessionId);

            if (session == null || session.Status != "đã kiểm kê") return;

            var beforeJson = Serialize(session);

            var stockInService = new StockInService(_contextFactory);
            var stockOutService = new StockOutService(_contextFactory);

            // Lấy hoặc tự động tạo khách hàng đặc biệt cho phiếu xuất kho điều chỉnh
            var defaultCustomer = db.Customers.FirstOrDefault(c => c.CustomerCode == "CUS-ADJ");
            if (defaultCustomer == null)
            {
                defaultCustomer = new Customer
                {
                    CustomerCode = "CUS-ADJ",
                    DisplayName = "Khách hàng điều chỉnh (Hệ thống)",
                    IsActive = true
                };
                db.Customers.Add(defaultCustomer);
                db.SaveChanges();
            }
            int defaultCustomerId = defaultCustomer.Id;

            // Tạo các phiếu nhập/xuất kho nháp tương ứng với chênh lệch kiểm kê
            foreach (var line in session.Lines!)
            {
                if (line.VarianceQuantity == 0) continue;

                var product = db.Products.Find(line.ProductId);
                if (product == null) continue;

                if (line.VarianceQuantity > 0)
                {
                    var stockIn = new StockIn
                    {
                        DocumentCode = $"SI-ADJ-{session.SessionCode}-{line.Id}",
                        WarehouseId = session.WarehouseId,
                        ImportDate = DateTime.Now,
                        Notes = $"Nhập để điều chỉnh tồn kho (Theo phiên kiểm kê {session.SessionCode})",
                        PurposeCode = "Adjustment",
                        Status = DocumentStatus.Draft,
                        CreatedBy = userId,
                        CreatedAt = DateTime.Now
                    };

                    var inLine = new StockInLine
                    {
                        ProductId = line.ProductId,
                        UnitId = product.DefaultUnitId,
                        Quantity = line.VarianceQuantity,
                        BaseQuantity = line.VarianceQuantity,
                        UnitPrice = product.CostPrice ?? product.DefaultPrice,
                        DraftSerials = line.SerialNumbers
                    };

                    stockInService.SaveDraft(stockIn, new List<StockInLine> { inLine }, userId);
                }
                else
                {
                    var stockOut = new StockOut
                    {
                        DocumentCode = $"SO-ADJ-{session.SessionCode}-{line.Id}",
                        CustomerId = defaultCustomerId,
                        WarehouseId = session.WarehouseId,
                        ExportDate = DateTime.Now,
                        Notes = $"Xuất để điều chỉnh tồn kho (Theo phiên kiểm kê {session.SessionCode})",
                        PurposeCode = "Adjustment",
                        Status = DocumentStatus.Draft,
                        CreatedBy = userId,
                        CreatedAt = DateTime.Now
                    };

                    var outLine = new StockOutLine
                    {
                        ProductId = line.ProductId,
                        UnitId = product.DefaultUnitId,
                        Quantity = Math.Abs(line.VarianceQuantity),
                        BaseQuantity = Math.Abs(line.VarianceQuantity),
                        UnitPrice = product.CostPrice ?? product.DefaultPrice,
                        DraftSerials = line.SerialNumbers
                    };

                    stockOutService.SaveDraft(stockOut, new List<StockOutLine> { outLine }, userId);
                }
            }

            session.Status = "hoàn thành";
            session.PostedBy = userId;
            session.PostedAt = DateTime.Now;
            db.SaveChanges();

            var afterJson = Serialize(session);
            AddAudit(db, "POST", session.Id, beforeJson, afterJson, userId);
        }

        private string Serialize(StockCountSession s)
        {
            return JsonSerializer.Serialize(new
            {
                s.Id,
                s.SessionCode,
                s.WarehouseId,
                s.Status,
                s.CountDate,
                s.CreatedBy,
                s.PostedBy,
                s.PostedAt
            }, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All)
            });
        }

        private void AddAudit(AppDbContext db, string action, int entityId, string? before, string? after, int performedBy)
        {
            db.AuditLogs.Add(new AuditLog
            {
                EntityName = "StockCountSession",
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
