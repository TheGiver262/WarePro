using System;
using System.Linq;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.ViewModels;

internal static class InvoicePaymentStatusFilter
{
    // trạng thái quá hạn được suy ra từ due date hiện tại, không chỉ dựa vào chuỗi lưu trong database
    public static IQueryable<SalesInvoice> Apply(IQueryable<SalesInvoice> query, string status)
    {
        var today = DateTime.Today;
        if (status == PaymentStatus.Overdue)
        {
            return query.Where(invoice => invoice.PaymentStatus != PaymentStatus.Paid
                && invoice.DueDate.HasValue
                && invoice.DueDate.Value < today);
        }

        if (status is PaymentStatus.Unpaid or PaymentStatus.PartiallyPaid)
        {
            return query.Where(invoice => invoice.PaymentStatus == status
                && (!invoice.DueDate.HasValue || invoice.DueDate.Value >= today));
        }

        return query.Where(invoice => invoice.PaymentStatus == status);
    }

    // unpaid và partially paid loại dòng đã quá hạn để các nhóm lọc không chồng nhau
    public static IQueryable<PurchaseInvoice> Apply(IQueryable<PurchaseInvoice> query, string status)
    {
        var today = DateTime.Today;
        if (status == PaymentStatus.Overdue)
        {
            return query.Where(invoice => invoice.PaymentStatus != PaymentStatus.Paid
                && invoice.DueDate.HasValue
                && invoice.DueDate.Value < today);
        }

        if (status is PaymentStatus.Unpaid or PaymentStatus.PartiallyPaid)
        {
            return query.Where(invoice => invoice.PaymentStatus == status
                && (!invoice.DueDate.HasValue || invoice.DueDate.Value >= today));
        }

        return query.Where(invoice => invoice.PaymentStatus == status);
    }
}
