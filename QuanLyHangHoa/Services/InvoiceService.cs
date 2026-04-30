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

        public InvoiceService()
            : this(() => new AppDbContext())
        {
        }

        public InvoiceService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public void SaveSalesInvoice(SalesInvoice invoice)
        {
            CalculateSalesInvoice(invoice);
            using var db = _contextFactory();
            db.SalesInvoices.Add(invoice);
            db.SaveChanges();
        }

        public void SavePurchaseInvoice(PurchaseInvoice invoice)
        {
            CalculatePurchaseInvoice(invoice);
            using var db = _contextFactory();
            db.PurchaseInvoices.Add(invoice);
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
            invoice.PaymentStatus = GetPaymentStatus(invoice.PaidAmount, invoice.GrandTotal);
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
            invoice.PaymentStatus = GetPaymentStatus(invoice.PaidAmount, invoice.GrandTotal);
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
                .Include(i => i.Lines!)
                .ThenInclude(l => l.Product)
                .OrderByDescending(i => i.InvoiceDate)
                .ToList();
        }

        private static string GetPaymentStatus(decimal paidAmount, decimal grandTotal)
        {
            if (paidAmount < 0 || paidAmount > grandTotal)
            {
                throw new InvalidOperationException("Số tiền đã thanh toán phải nằm trong khoảng từ 0 đến tổng tiền.");
            }

            if (paidAmount == 0)
            {
                return "Chưa thanh toán";
            }

            return paidAmount >= grandTotal ? "Đã thanh toán" : "Thanh toán một phần";
        }
    }
}
