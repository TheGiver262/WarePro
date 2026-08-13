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
    public partial class InvoiceService
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly DatabaseWriteExecutor _writeExecutor;

        public InvoiceService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _writeExecutor = new DatabaseWriteExecutor(_contextFactory);
        }

        public async Task<int> SaveSalesInvoiceAsync(
            SalesInvoice invoice,
            int actorId,
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(invoice);
            cancellationToken.ThrowIfCancellationRequested();
            if (invoice.Id == 0 && string.IsNullOrWhiteSpace(invoice.InvoiceCode))
            {
                invoice.InvoiceCode = await AllocateSalesInvoiceCodeAsync(
                    actorId, invoice.InvoiceDate, cancellationToken);
            }
            // input giữ snapshot scalar và dòng trước khi executor retry; mỗi attempt không dùng lại entity đã bị EF gán id hoặc tracking.
            // expectedRowVersion giữ đúng phiên bản người dùng đã đọc để mọi attempt cùng kiểm tra một mốc, không vô tình ghi đè bản mới hơn.
            var input = CreateSalesInvoiceCandidate(invoice);
            var expectedRowVersion = invoice.RowVersion.ToArray();
            var isNew = invoice.Id == 0;

            // transaction serializable gom kiểm tra quyền, đối soát phiếu xuất, lưu hóa đơn và bảo hành thành một lần ghi nguyên tử.
            var invoiceId = await _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest(
                    "invoice.sales.save",
                    operationId,
                    System.Data.IsolationLevel.Serializable),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(
                        db,
                        actorId,
                        PermissionAction.CreateSalesInvoice);

                    // tạo candidate mới trong từng attempt vì lần trước có thể đã được EF gán khóa rồi rollback hoặc commit chưa rõ kết quả.
                    var candidate = CreateSalesInvoiceCandidate(input);
                    if (isNew)
                    {
                        candidate.CreatedBy = actorId;
                        candidate.Status = InvoiceStatus.Active;
                    }

                    var stockOut = PrepareSalesInvoice(db, candidate);
                    var savedId = await UpsertSalesInvoiceAsync(
                        db,
                        candidate,
                        expectedRowVersion,
                        token);
                    // candidate đã có id từ SQL khi tạo hoặc id hiện có khi sửa, nên coverage luôn tham chiếu đúng hóa đơn trong transaction.
                    ReconcileWarrantyCoverages(db, candidate, stockOut);
                    return savedId;
                },
                (db, token) => VerifySalesInvoiceAsync(
                    db,
                    input.Id,
                    input.InvoiceCode,
                    input.StockOutId,
                    input.CustomerId,
                    actorId,
                    expectedRowVersion,
                    token),
                entityKey: input.InvoiceCode,
                cancellationToken: cancellationToken);

            invoice.Id = invoiceId;
            // nạp rowversion đã commit về model của màn hình để lần sửa tiếp theo dùng đúng phiên bản mới nhất.
            await using (var refresh = _contextFactory())
            {
                invoice.RowVersion = await refresh.SalesInvoices.AsNoTracking()
                    .Where(item => item.Id == invoiceId)
                    .Select(item => item.RowVersion)
                    .SingleAsync(cancellationToken);
            }
            return invoiceId;
        }

        private async Task<string> AllocateSalesInvoiceCodeAsync(
            int actorId,
            DateTime invoiceDate,
            CancellationToken cancellationToken)
        {
            await using var numberingDb = _contextFactory();
            AuthorizationService.RequireFreshActor(
                numberingDb,
                actorId,
                PermissionAction.CreateSalesInvoice);
            return await DocumentNumberAllocator.AllocateAsync(
                numberingDb,
                "SalesInvoice",
                "SINV",
                DateOnly.FromDateTime(invoiceDate.Date),
                cancellationToken);
        }

        public async Task<int> SavePurchaseInvoiceAsync(
            PurchaseInvoice invoice,
            int actorId,
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(invoice);
            // snapshot tách dữ liệu đầu vào khỏi ChangeTracker; retry luôn dựng lại graph dòng hóa đơn từ cùng yêu cầu ban đầu.
            var input = CreatePurchaseInvoiceCandidate(invoice);
            var expectedRowVersion = invoice.RowVersion.ToArray();
            var isNew = invoice.Id == 0;

            var invoiceId = await _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest(
                    "invoice.purchase.save",
                    operationId,
                    System.Data.IsolationLevel.Serializable),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(
                        db,
                        actorId,
                        PermissionAction.CreatePurchaseInvoice);

                    var candidate = CreatePurchaseInvoiceCandidate(input);
                    if (isNew)
                    {
                        candidate.CreatedBy = actorId;
                        candidate.Status = InvoiceStatus.Active;
                    }

                    PreparePurchaseInvoice(db, candidate);
                    return await UpsertPurchaseInvoiceAsync(
                        db,
                        candidate,
                        expectedRowVersion,
                        token);
                },
                (db, token) => VerifyPurchaseInvoiceAsync(
                    db,
                    input.Id,
                    input.InvoiceCode,
                    input.StockInId,
                    input.SupplierId,
                    actorId,
                    expectedRowVersion,
                    token),
                entityKey: input.InvoiceCode,
                cancellationToken: cancellationToken);

            invoice.Id = invoiceId;
            await using (var refresh = _contextFactory())
            {
                invoice.RowVersion = await refresh.PurchaseInvoices.AsNoTracking()
                    .Where(item => item.Id == invoiceId)
                    .Select(item => item.RowVersion)
                    .SingleAsync(cancellationToken);
            }
            return invoiceId;
        }

        private static SalesInvoice CreateSalesInvoiceCandidate(SalesInvoice source) => new()
        {
            Id = source.Id,
            InvoiceCode = source.InvoiceCode.Trim(),
            CustomerId = source.CustomerId,
            StockOutId = source.StockOutId,
            InvoiceDate = source.InvoiceDate,
            SubTotal = source.SubTotal,
            TaxAmount = source.TaxAmount,
            GrandTotal = source.GrandTotal,
            PaidAmount = source.PaidAmount,
            PaymentStatus = source.PaymentStatus,
            DueDate = source.DueDate,
            CreatedBy = source.CreatedBy,
            CreatedAt = source.CreatedAt,
            Notes = source.Notes?.Trim(),
            Status = source.Status,
            RowVersion = source.RowVersion.ToArray(),
            Lines = source.Lines.Select(line => new SalesInvoiceLine
            {
                ProductId = line.ProductId,
                UnitId = line.UnitId,
                StockOutLineId = line.StockOutLineId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                TaxRate = line.TaxRate
            }).ToList()
        };

        private static PurchaseInvoice CreatePurchaseInvoiceCandidate(PurchaseInvoice source) => new()
        {
            Id = source.Id,
            InvoiceCode = source.InvoiceCode.Trim(),
            SupplierId = source.SupplierId,
            StockInId = source.StockInId,
            InvoiceDate = source.InvoiceDate,
            SubTotal = source.SubTotal,
            TaxAmount = source.TaxAmount,
            GrandTotal = source.GrandTotal,
            PaidAmount = source.PaidAmount,
            PaymentStatus = source.PaymentStatus,
            DueDate = source.DueDate,
            CreatedBy = source.CreatedBy,
            CreatedAt = source.CreatedAt,
            Notes = source.Notes?.Trim(),
            Status = source.Status,
            RowVersion = source.RowVersion.ToArray(),
            Lines = source.Lines.Select(line => new PurchaseInvoiceLine
            {
                ProductId = line.ProductId,
                UnitId = line.UnitId,
                StockInLineId = line.StockInLineId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                TaxRate = line.TaxRate
            }).ToList()
        };

        // sau lỗi commit không chắc chắn, chỉ coi là thành công khi trạng thái tự nhiên khớp và rowversion chứng minh bản sửa đã phát sinh.
        private static Task<bool> VerifySalesInvoiceAsync(
            AppDbContext db,
            int invoiceId,
            string invoiceCode,
            int? stockOutId,
            int customerId,
            int actorId,
            byte[] expectedRowVersion,
            CancellationToken cancellationToken)
        {
            return db.SalesInvoices.AsNoTracking().AnyAsync(item =>
                (invoiceId == 0 || item.Id == invoiceId)
                && item.InvoiceCode == invoiceCode
                && item.StockOutId == stockOutId
                && item.CustomerId == customerId
                && (invoiceId != 0 || item.CreatedBy == actorId)
                && (invoiceId == 0 || item.RowVersion != expectedRowVersion),
                cancellationToken);
        }

        // với bản tạo mới, actor xác nhận bản ghi do đúng người thực hiện tạo; với bản sửa, rowversion phải khác mốc client đã gửi.
        private static Task<bool> VerifyPurchaseInvoiceAsync(
            AppDbContext db,
            int invoiceId,
            string invoiceCode,
            int? stockInId,
            int supplierId,
            int actorId,
            byte[] expectedRowVersion,
            CancellationToken cancellationToken)
        {
            return db.PurchaseInvoices.AsNoTracking().AnyAsync(item =>
                (invoiceId == 0 || item.Id == invoiceId)
                && item.InvoiceCode == invoiceCode
                && item.StockInId == stockInId
                && item.SupplierId == supplierId
                && (invoiceId != 0 || item.CreatedBy == actorId)
                && (invoiceId == 0 || item.RowVersion != expectedRowVersion),
                cancellationToken);
        }

        internal void SaveSalesInvoice(SalesInvoice invoice, int actorId)
        {
            if (invoice.Id != 0 && invoice.RowVersion.Length == 0)
            {
                using var db = _contextFactory();
                invoice.RowVersion = db.SalesInvoices.AsNoTracking()
                    .Where(item => item.Id == invoice.Id)
                    .Select(item => item.RowVersion)
                    .Single();
            }

            SaveSalesInvoiceAsync(invoice, actorId, Guid.NewGuid()).GetAwaiter().GetResult();
        }

        internal void SavePurchaseInvoice(PurchaseInvoice invoice, int actorId)
        {
            if (invoice.Id != 0 && invoice.RowVersion.Length == 0)
            {
                using var db = _contextFactory();
                invoice.RowVersion = db.PurchaseInvoices.AsNoTracking()
                    .Where(item => item.Id == invoice.Id)
                    .Select(item => item.RowVersion)
                    .Single();
            }

            SavePurchaseInvoiceAsync(invoice, actorId, Guid.NewGuid()).GetAwaiter().GetResult();
        }
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
