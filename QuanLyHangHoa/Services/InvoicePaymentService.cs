using System;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class InvoicePaymentService
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly Func<DateTime> _clock;

        public InvoicePaymentService()
            : this(() => new AppDbContext(), () => DateTime.Now)
        {
        }

        public InvoicePaymentService(Func<AppDbContext> contextFactory, Func<DateTime> clock)
        {
            _contextFactory = contextFactory;
            _clock = clock;
        }

        public void RecordSalesPayment(int salesInvoiceId, decimal amount, string paymentMethod, string note, int receivedBy)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();
            var invoice = db.SalesInvoices.Find(salesInvoiceId)
                ?? throw new InvalidOperationException($"Sales invoice {salesInvoiceId} does not exist.");

            ApplyPayment(invoice, amount);
            db.InvoicePayments.Add(new InvoicePayment
            {
                SalesInvoiceId = salesInvoiceId,
                PaymentDate = _clock(),
                Amount = amount,
                PaymentMethod = paymentMethod,
                Note = note,
                ReceivedBy = receivedBy
            });

            db.SaveChanges();
            transaction.Commit();
        }

        public void RecordPurchasePayment(int purchaseInvoiceId, decimal amount, string paymentMethod, string note, int paidBy)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();
            var invoice = db.PurchaseInvoices.Find(purchaseInvoiceId)
                ?? throw new InvalidOperationException($"Purchase invoice {purchaseInvoiceId} does not exist.");

            ApplyPayment(invoice, amount);
            db.InvoicePayments.Add(new InvoicePayment
            {
                PurchaseInvoiceId = purchaseInvoiceId,
                PaymentDate = _clock(),
                Amount = amount,
                PaymentMethod = paymentMethod,
                Note = note,
                ReceivedBy = paidBy
            });

            db.SaveChanges();
            transaction.Commit();
        }

        private static void ApplyPayment(SalesInvoice invoice, decimal amount)
        {
            ValidatePaymentAmount(invoice.PaidAmount, invoice.GrandTotal, amount);
            invoice.PaidAmount += amount;
            invoice.PaymentStatus = GetPaymentStatus(invoice.PaidAmount, invoice.GrandTotal);
        }

        private static void ApplyPayment(PurchaseInvoice invoice, decimal amount)
        {
            ValidatePaymentAmount(invoice.PaidAmount, invoice.GrandTotal, amount);
            invoice.PaidAmount += amount;
            invoice.PaymentStatus = GetPaymentStatus(invoice.PaidAmount, invoice.GrandTotal);
        }

        private static void ValidatePaymentAmount(decimal paidAmount, decimal grandTotal, decimal amount)
        {
            if (amount <= 0)
            {
                throw new InvalidOperationException("Payment amount must be greater than zero.");
            }

            if (paidAmount + amount > grandTotal)
            {
                throw new InvalidOperationException("Payment amount exceeds remaining invoice balance.");
            }
        }

        private static string GetPaymentStatus(decimal paidAmount, decimal grandTotal)
        {
            if (paidAmount == 0)
            {
                return "Unpaid";
            }

            return paidAmount >= grandTotal ? "Paid" : "Partial";
        }
    }
}
