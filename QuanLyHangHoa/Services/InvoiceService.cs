using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class InvoiceService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public InvoiceService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public void SaveSalesInvoice(SalesInvoice invoice)
        {
            CalculateSalesInvoice(invoice);
            using var db = _contextFactory();
            
            if (invoice.Id == 0)
            {
                db.SalesInvoices.Add(invoice);
            }
            else
            {
                // Ensure no other instance is tracked for this ID
                var local = db.SalesInvoices.Local.FirstOrDefault(i => i.Id == invoice.Id);
                if (local != null) db.Entry(local).State = EntityState.Detached;

                // Load existing with lines to clear them properly
                var existing = db.SalesInvoices.Include(i => i.Lines).FirstOrDefault(i => i.Id == invoice.Id);
                if (existing != null)
                {
                    // Remove old lines
                    if (existing.Lines != null)
                        db.SalesInvoiceLines.RemoveRange(existing.Lines);
                    
                    // Update main properties
                    db.Entry(existing).CurrentValues.SetValues(invoice);
                    
                    // Add new lines
                    foreach (var line in invoice.Lines ?? new List<SalesInvoiceLine>())
                    {
                        line.SalesInvoiceId = invoice.Id;
                        line.Id = 0; // Ensure they are added
                        db.SalesInvoiceLines.Add(line);
                    }
                }
                else
                {
                    db.SalesInvoices.Update(invoice);
                }
            }
            db.SaveChanges();

            // Automate warranty coverage creation
            if (invoice.StockOutId.HasValue)
            {
                var stockOut = db.StockOuts
                    .Include(s => s.Lines)
                    .ThenInclude(l => l.ProductSerials)
                    .Include(s => s.Lines)
                    .ThenInclude(l => l.Product)
                    .FirstOrDefault(s => s.Id == invoice.StockOutId.Value);

                if (stockOut != null)
                {
                    foreach (var line in stockOut.Lines)
                    {
                        var months = line.Product.WarrantyPeriodMonths;
                        if (months <= 0) months = 12;

                        foreach (var serial in line.ProductSerials)
                        {
                            var existingCoverage = db.WarrantyCoverages
                                .FirstOrDefault(c => c.ProductSerialId == serial.Id && c.SalesInvoiceId == invoice.Id);
                            if (existingCoverage == null)
                            {
                                var coverage = new WarrantyCoverage
                                {
                                    ProductSerialId = serial.Id,
                                    CustomerId = invoice.CustomerId,
                                    SalesInvoiceId = invoice.Id,
                                    WarrantyStartDate = invoice.InvoiceDate,
                                    WarrantyEndDate = invoice.InvoiceDate.AddMonths(months),
                                    CoverageStatus = "Active"
                                };
                                db.WarrantyCoverages.Add(coverage);
                            }
                        }
                    }
                    db.SaveChanges();
                }
            }
        }

        public void SavePurchaseInvoice(PurchaseInvoice invoice)
        {
            CalculatePurchaseInvoice(invoice);
            using var db = _contextFactory();
            
            if (invoice.Id == 0)
            {
                db.PurchaseInvoices.Add(invoice);
            }
            else
            {
                // Ensure no other instance is tracked for this ID
                var local = db.PurchaseInvoices.Local.FirstOrDefault(i => i.Id == invoice.Id);
                if (local != null) db.Entry(local).State = EntityState.Detached;

                // Load existing with lines to clear them properly
                var existing = db.PurchaseInvoices.Include(i => i.Lines).FirstOrDefault(i => i.Id == invoice.Id);
                if (existing != null)
                {
                    // Remove old lines
                    if (existing.Lines != null)
                        db.PurchaseInvoiceLines.RemoveRange(existing.Lines);
                    
                    // Update main properties
                    db.Entry(existing).CurrentValues.SetValues(invoice);
                    
                    // Add new lines
                    foreach (var line in invoice.Lines ?? new List<PurchaseInvoiceLine>())
                    {
                        line.PurchaseInvoiceId = invoice.Id;
                        line.Id = 0; // Ensure they are added
                        db.PurchaseInvoiceLines.Add(line);
                    }
                }
                else
                {
                    db.PurchaseInvoices.Update(invoice);
                }
            }
            db.SaveChanges();
        }

        private static void CalculateSalesInvoice(SalesInvoice invoice)
        {
            if (invoice.Lines == null) return;

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

            UpdateSalesPaymentStatus(invoice);
        }

        private static void UpdateSalesPaymentStatus(SalesInvoice invoice)
        {
            if (invoice.PaidAmount >= invoice.GrandTotal && invoice.GrandTotal > 0)
                invoice.PaymentStatus = "Paid";
            else if (invoice.PaidAmount > 0)
                invoice.PaymentStatus = "Partial";
            else
                invoice.PaymentStatus = "Unpaid";

            if (invoice.PaymentStatus != "Paid" && invoice.DueDate.HasValue && invoice.DueDate.Value.Date < DateTime.Today)
                invoice.PaymentStatus = "Overdue";
        }

        private static void CalculatePurchaseInvoice(PurchaseInvoice invoice)
        {
            if (invoice.Lines == null) return;

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

            UpdatePurchasePaymentStatus(invoice);
        }

        private static void UpdatePurchasePaymentStatus(PurchaseInvoice invoice)
        {
            if (invoice.PaidAmount >= invoice.GrandTotal && invoice.GrandTotal > 0)
                invoice.PaymentStatus = "Paid";
            else if (invoice.PaidAmount > 0)
                invoice.PaymentStatus = "Partial";
            else
                invoice.PaymentStatus = "Unpaid";

            if (invoice.PaymentStatus != "Paid" && invoice.DueDate.HasValue && invoice.DueDate.Value.Date < DateTime.Today)
                invoice.PaymentStatus = "Overdue";
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
            return db.SalesInvoices
                .Include(i => i.Customer)
                .Include(i => i.Creator)
                .Include(i => i.Lines!)
                .ThenInclude(l => l.Product)
                .OrderByDescending(i => i.InvoiceDate)
                .ToList();
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

            return query
                .OrderByDescending(i => i.InvoiceDate)
                .Skip(skip)
                .Take(take)
                .ToList();
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
                var term = code.Trim().ToLower();
                query = query.Where(i => i.InvoiceCode != null && i.InvoiceCode.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(customerName))
            {
                var term = customerName.Trim().ToLower();
                query = query.Where(i => i.Customer != null && i.Customer.DisplayName != null && i.Customer.DisplayName.ToLower().Contains(term));
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
                query = query.Where(i => i.PaymentStatus == paymentStatus);
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
            return db.PurchaseInvoices
                .Include(i => i.Supplier)
                .Include(i => i.Creator)
                .Include(i => i.Lines!)
                .ThenInclude(l => l.Product)
                .OrderByDescending(i => i.InvoiceDate)
                .ToList();
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

            return query
                .OrderByDescending(i => i.InvoiceDate)
                .Skip(skip)
                .Take(take)
                .ToList();
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
                var term = code.Trim().ToLower();
                query = query.Where(i => i.InvoiceCode != null && i.InvoiceCode.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(supplierName))
            {
                var term = supplierName.Trim().ToLower();
                query = query.Where(i => i.Supplier != null && i.Supplier.DisplayName != null && i.Supplier.DisplayName.ToLower().Contains(term));
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
                query = query.Where(i => i.PaymentStatus == paymentStatus);
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
