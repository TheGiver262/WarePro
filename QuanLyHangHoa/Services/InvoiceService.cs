using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public partial class InvoiceService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public InvoiceService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // serializable giữ kiểm tra quyền, chứng từ kho, hóa đơn và bảo hành trong một transaction không bị chen dữ liệu
        public void SaveSalesInvoice(SalesInvoice invoice, int actorId)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);
            AuthorizationService.RequireFreshActor(db, actorId, PermissionAction.CreateSalesInvoice);
            var isNew = invoice.Id == 0;
            if (isNew)
            {
                invoice.CreatedBy = actorId;
            }
            try
            {
                var stockOut = PrepareSalesInvoice(db, invoice);
                UpsertSalesInvoice(db, invoice);
                ReconcileWarrantyCoverages(db, invoice, stockOut);
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                if (isNew)
                {
                    invoice.Id = 0;
                }
                throw;
            }
        }

        // actor được đọc mới từ database ngay trong transaction; object từ UI không quyết định quyền
        public void SavePurchaseInvoice(PurchaseInvoice invoice, int actorId)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);
            AuthorizationService.RequireFreshActor(db, actorId, PermissionAction.CreatePurchaseInvoice);
            var isNew = invoice.Id == 0;
            if (isNew)
            {
                invoice.CreatedBy = actorId;
            }
            try
            {
                PreparePurchaseInvoice(db, invoice);
                UpsertPurchaseInvoice(db, invoice);
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                if (isNew)
                {
                    invoice.Id = 0;
                }
                throw;
            }
        }

        // tổng hóa đơn luôn tính lại từ dòng bằng decimal, không tin các tổng số gửi từ giao diện
        private static void CalculateSalesInvoice(SalesInvoice invoice)
        {
            if (invoice.Lines == null || invoice.Lines.Count == 0)
                throw new InvalidOperationException("Invoice must contain at least one line.");

            foreach (var line in invoice.Lines)
            {
                CalculateLine(line.Quantity, line.UnitPrice, line.TaxRate, out var subTotal, out var taxAmount, out var grandTotal);
                line.SubTotal = subTotal;
                line.TaxAmount = taxAmount;
                line.GrandTotal = grandTotal;
            }

            invoice.SubTotal = invoice.Lines.Sum(line => line.SubTotal);
            invoice.TaxAmount = invoice.Lines.Sum(line => line.TaxAmount);
            invoice.GrandTotal = invoice.Lines.Sum(line => line.GrandTotal);

            ValidatePayment(invoice.PaidAmount, invoice.GrandTotal);
            UpdateSalesPaymentStatus(invoice);
        }

        // trạng thái thanh toán được suy ra từ PaidAmount, GrandTotal và hạn; lớp gọi không được tự gán tùy ý
        private static void UpdateSalesPaymentStatus(SalesInvoice invoice)
        {
            if (invoice.PaidAmount == invoice.GrandTotal && invoice.GrandTotal > 0)
                invoice.PaymentStatus = PaymentStatus.Paid;
            else if (invoice.PaidAmount > 0)
                invoice.PaymentStatus = PaymentStatus.PartiallyPaid;
            else
                invoice.PaymentStatus = PaymentStatus.Unpaid;

            if (invoice.PaymentStatus != PaymentStatus.Paid && invoice.DueDate.HasValue && invoice.DueDate.Value.Date < DateTime.Today)
                invoice.PaymentStatus = PaymentStatus.Overdue;
        }

        // hóa đơn mua dùng cùng công thức dòng và kiểm tra thanh toán như hóa đơn bán
        private static void CalculatePurchaseInvoice(PurchaseInvoice invoice)
        {
            if (invoice.Lines == null || invoice.Lines.Count == 0)
                throw new InvalidOperationException("Invoice must contain at least one line.");

            foreach (var line in invoice.Lines)
            {
                CalculateLine(line.Quantity, line.UnitPrice, line.TaxRate, out var subTotal, out var taxAmount, out var grandTotal);
                line.SubTotal = subTotal;
                line.TaxAmount = taxAmount;
                line.GrandTotal = grandTotal;
            }

            invoice.SubTotal = invoice.Lines.Sum(line => line.SubTotal);
            invoice.TaxAmount = invoice.Lines.Sum(line => line.TaxAmount);
            invoice.GrandTotal = invoice.Lines.Sum(line => line.GrandTotal);

            ValidatePayment(invoice.PaidAmount, invoice.GrandTotal);
            UpdatePurchasePaymentStatus(invoice);
        }

        private static void UpdatePurchasePaymentStatus(PurchaseInvoice invoice)
        {
            if (invoice.PaidAmount == invoice.GrandTotal && invoice.GrandTotal > 0)
                invoice.PaymentStatus = PaymentStatus.Paid;
            else if (invoice.PaidAmount > 0)
                invoice.PaymentStatus = PaymentStatus.PartiallyPaid;
            else
                invoice.PaymentStatus = PaymentStatus.Unpaid;

            if (invoice.PaymentStatus != PaymentStatus.Paid && invoice.DueDate.HasValue && invoice.DueDate.Value.Date < DateTime.Today)
                invoice.PaymentStatus = PaymentStatus.Overdue;
        }

        // không cho số đã trả âm hoặc vượt tổng tiền, tránh trạng thái và công nợ mâu thuẫn
        private static void ValidatePayment(decimal paidAmount, decimal grandTotal)
        {
            if (paidAmount < 0)
                throw new InvalidOperationException("Invoice paid amount cannot be negative.");
            if (paidAmount > grandTotal)
                throw new InvalidOperationException("Invoice paid amount cannot exceed the grand total.");
        }

        // taxRate lưu dạng tỷ lệ thập phân, ví dụ 0.1 là 10%; grandTotal = quantity * unitPrice + thuế
        private static void CalculateLine(
            decimal quantity,
            decimal unitPrice,
            decimal taxRate,
            out decimal subTotal,
            out decimal taxAmount,
            out decimal grandTotal)
        {
            if (quantity <= 0)
            {
                throw new InvalidOperationException("Invoice quantity must be greater than zero.");
            }

            if (unitPrice < 0)
            {
                throw new InvalidOperationException("Invoice unit price cannot be negative.");
            }

            if (taxRate < 0)
            {
                throw new InvalidOperationException("Invoice tax rate cannot be negative.");
            }

            subTotal = quantity * unitPrice;
            taxAmount = subTotal * taxRate;
            grandTotal = subTotal + taxAmount;
        }

        public List<SalesInvoice> GetAllSalesInvoices()
        {
            using var db = _contextFactory();
            var invoices = db.SalesInvoices
                .Include(i => i.Customer)
                .Include(i => i.Creator)
                .Include(i => i.Lines!)
                .ThenInclude(l => l.Product)
                .OrderByDescending(i => i.InvoiceDate)
                .ToList();
            MarkEffectivePaymentStatus(invoices);
            return invoices;
        }

        // danh sách chỉ đọc dùng no-tracking; sau khi materialize mới gắn trạng thái Overdue hiệu lực theo ngày hiện tại
        public List<SalesInvoice> GetSalesInvoicesPaged(
            string code,
            string customerName,
            DateTime? startDate,
            DateTime? endDate,
            string paymentStatus,
            decimal? minTotal,
            decimal? maxTotal,
            int skip,
            int take)
        {
            using var db = _contextFactory();
            var query = db.SalesInvoices.AsNoTracking()
                .Include(i => i.Customer)
                .Include(i => i.Creator)
                .Include(i => i.Lines!)
                .ThenInclude(l => l.Product)
                .AsQueryable();

            query = ApplySalesInvoiceFilters(query, code, customerName, startDate, endDate, paymentStatus, minTotal, maxTotal);

            var invoices = query
                .OrderByDescending(i => i.InvoiceDate)
                .Skip(skip)
                .Take(take)
                .ToList();
            MarkEffectivePaymentStatus(invoices);
            return invoices;
        }

        public int GetSalesInvoicesCount(
            string code,
            string customerName,
            DateTime? startDate,
            DateTime? endDate,
            string paymentStatus,
            decimal? minTotal,
            decimal? maxTotal)
        {
            using var db = _contextFactory();
            var query = db.SalesInvoices.AsNoTracking().AsQueryable();
            query = ApplySalesInvoiceFilters(query, code, customerName, startDate, endDate, paymentStatus, minTotal, maxTotal);
            return query.Count();
        }

        // query đếm và query phân trang dùng cùng bộ lọc để số trang không lệch dữ liệu
        private IQueryable<SalesInvoice> ApplySalesInvoiceFilters(
            IQueryable<SalesInvoice> query,
            string code,
            string customerName,
            DateTime? startDate,
            DateTime? endDate,
            string paymentStatus,
            decimal? minTotal,
            decimal? maxTotal)
        {
            if (!string.IsNullOrWhiteSpace(code))
            {
                var term = code.Trim();
                query = query.Where(i => i.InvoiceCode != null && i.InvoiceCode.Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(customerName))
            {
                var term = customerName.Trim();
                query = query.Where(i => i.Customer != null && i.Customer.DisplayName != null && i.Customer.DisplayName.Contains(term));
            }

            if (startDate.HasValue)
            {
                query = query.Where(i => i.InvoiceDate >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                // đổi ngày kết thúc thành tick cuối ngày để không bỏ hóa đơn có giờ phút trong ngày đó
                var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(i => i.InvoiceDate <= endOfDay);
            }

            if (!string.IsNullOrEmpty(paymentStatus) && paymentStatus != "Tất cả" && paymentStatus != "All")
            {
                query = ApplySalesPaymentStatusFilter(query, paymentStatus);
            }

            if (minTotal.HasValue)
            {
                query = query.Where(i => i.GrandTotal >= minTotal.Value);
            }

            if (maxTotal.HasValue)
            {
                query = query.Where(i => i.GrandTotal <= maxTotal.Value);
            }

            return query;
        }

        public List<PurchaseInvoice> GetAllPurchaseInvoices()
        {
            using var db = _contextFactory();
            var invoices = db.PurchaseInvoices
                .Include(i => i.Supplier)
                .Include(i => i.Creator)
                .Include(i => i.Lines!)
                .ThenInclude(l => l.Product)
                .OrderByDescending(i => i.InvoiceDate)
                .ToList();
            MarkEffectivePaymentStatus(invoices);
            return invoices;
        }

        // Include chỉ nạp đối tác, người tạo và dòng cần cho màn hình; không theo dõi vì không sửa tại đây
        public List<PurchaseInvoice> GetPurchaseInvoicesPaged(
            string code,
            string supplierName,
            DateTime? startDate,
            DateTime? endDate,
            string paymentStatus,
            decimal? minTotal,
            decimal? maxTotal,
            int skip,
            int take)
        {
            using var db = _contextFactory();
            var query = db.PurchaseInvoices.AsNoTracking()
                .Include(i => i.Supplier)
                .Include(i => i.Creator)
                .Include(i => i.Lines!)
                .ThenInclude(l => l.Product)
                .AsQueryable();

            query = ApplyPurchaseInvoiceFilters(query, code, supplierName, startDate, endDate, paymentStatus, minTotal, maxTotal);

            var invoices = query
                .OrderByDescending(i => i.InvoiceDate)
                .Skip(skip)
                .Take(take)
                .ToList();
            MarkEffectivePaymentStatus(invoices);
            return invoices;
        }

        public int GetPurchaseInvoicesCount(
            string code,
            string supplierName,
            DateTime? startDate,
            DateTime? endDate,
            string paymentStatus,
            decimal? minTotal,
            decimal? maxTotal)
        {
            using var db = _contextFactory();
            var query = db.PurchaseInvoices.AsNoTracking().AsQueryable();
            query = ApplyPurchaseInvoiceFilters(query, code, supplierName, startDate, endDate, paymentStatus, minTotal, maxTotal);
            return query.Count();
        }

        // giữ điều kiện ở IQueryable để database lọc trước khi phân trang
        private IQueryable<PurchaseInvoice> ApplyPurchaseInvoiceFilters(
            IQueryable<PurchaseInvoice> query,
            string code,
            string supplierName,
            DateTime? startDate,
            DateTime? endDate,
            string paymentStatus,
            decimal? minTotal,
            decimal? maxTotal)
        {
            if (!string.IsNullOrWhiteSpace(code))
            {
                var term = code.Trim();
                query = query.Where(i => i.InvoiceCode != null && i.InvoiceCode.Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(supplierName))
            {
                var term = supplierName.Trim();
                query = query.Where(i => i.Supplier != null && i.Supplier.DisplayName != null && i.Supplier.DisplayName.Contains(term));
            }

            if (startDate.HasValue)
            {
                query = query.Where(i => i.InvoiceDate >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(i => i.InvoiceDate <= endOfDay);
            }

            if (!string.IsNullOrEmpty(paymentStatus) && paymentStatus != "Tất cả" && paymentStatus != "All")
            {
                query = ApplyPurchasePaymentStatusFilter(query, paymentStatus);
            }

            if (minTotal.HasValue)
            {
                query = query.Where(i => i.GrandTotal >= minTotal.Value);
            }

            if (maxTotal.HasValue)
            {
                query = query.Where(i => i.GrandTotal <= maxTotal.Value);
            }

            return query;
        }

    }
}
