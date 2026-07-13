using System;
using System.Linq;

using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services;

public partial class InvoiceService
{
    private static IQueryable<SalesInvoice> ApplySalesPaymentStatusFilter(
        IQueryable<SalesInvoice> query,
        string paymentStatus)
    {
        var today = DateTime.Today;
        if (paymentStatus == PaymentStatus.Overdue)
        {
            return query.Where(invoice =>
                invoice.PaymentStatus != PaymentStatus.Paid
                && invoice.DueDate.HasValue
                && invoice.DueDate.Value < today);
        }

        if (paymentStatus is PaymentStatus.Unpaid or PaymentStatus.PartiallyPaid)
        {
            return query.Where(invoice =>
                invoice.PaymentStatus == paymentStatus
                && (!invoice.DueDate.HasValue || invoice.DueDate.Value >= today));
        }

        return query.Where(invoice => invoice.PaymentStatus == paymentStatus);
    }

    private static IQueryable<PurchaseInvoice> ApplyPurchasePaymentStatusFilter(
        IQueryable<PurchaseInvoice> query,
        string paymentStatus)
    {
        var today = DateTime.Today;
        if (paymentStatus == PaymentStatus.Overdue)
        {
            return query.Where(invoice =>
                invoice.PaymentStatus != PaymentStatus.Paid
                && invoice.DueDate.HasValue
                && invoice.DueDate.Value < today);
        }

        if (paymentStatus is PaymentStatus.Unpaid or PaymentStatus.PartiallyPaid)
        {
            return query.Where(invoice =>
                invoice.PaymentStatus == paymentStatus
                && (!invoice.DueDate.HasValue || invoice.DueDate.Value >= today));
        }

        return query.Where(invoice => invoice.PaymentStatus == paymentStatus);
    }
}
