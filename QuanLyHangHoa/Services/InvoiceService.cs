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

        public void SaveSalesInvoice(SalesInvoice invoice)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var isNew = invoice.Id == 0;
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

        public void SavePurchaseInvoice(PurchaseInvoice invoice)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var isNew = invoice.Id == 0;
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

        private static void ValidatePayment(decimal paidAmount, decimal grandTotal)
        {
            if (paidAmount < 0)
                throw new InvalidOperationException("Invoice paid amount cannot be negative.");
            if (paidAmount > grandTotal)
                throw new InvalidOperationException("Invoice paid amount cannot exceed the grand total.");
        }

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
                // To include the whole end date
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
