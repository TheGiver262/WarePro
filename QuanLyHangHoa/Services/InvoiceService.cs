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

    }
}
