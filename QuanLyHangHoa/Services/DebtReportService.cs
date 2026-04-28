using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;

namespace QuanLyHangHoa.Services
{
    public sealed record DebtSummary(
        int PartyId,
        string PartyName,
        decimal TotalAmount,
        decimal PaidAmount,
        decimal DebtAmount);

    public class DebtReportService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public DebtReportService()
            : this(() => new AppDbContext())
        {
        }

        public DebtReportService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public IReadOnlyList<DebtSummary> GetCustomerDebtSummary()
        {
            using var db = _contextFactory();
            return db.SalesInvoices
                .Include(invoice => invoice.Customer)
                .AsEnumerable()
                .GroupBy(invoice => new { invoice.CustomerId, invoice.Customer!.Name })
                .Select(group => new DebtSummary(
                    group.Key.CustomerId,
                    group.Key.Name,
                    group.Sum(invoice => invoice.GrandTotal),
                    group.Sum(invoice => invoice.PaidAmount),
                    group.Sum(invoice => invoice.GrandTotal - invoice.PaidAmount)))
                .Where(summary => summary.DebtAmount > 0)
                .OrderByDescending(summary => summary.DebtAmount)
                .ToList();
        }

        public IReadOnlyList<DebtSummary> GetSupplierDebtSummary()
        {
            using var db = _contextFactory();
            return db.PurchaseInvoices
                .Include(invoice => invoice.Supplier)
                .AsEnumerable()
                .GroupBy(invoice => new { invoice.SupplierId, invoice.Supplier!.Name })
                .Select(group => new DebtSummary(
                    group.Key.SupplierId,
                    group.Key.Name,
                    group.Sum(invoice => invoice.GrandTotal),
                    group.Sum(invoice => invoice.PaidAmount),
                    group.Sum(invoice => invoice.GrandTotal - invoice.PaidAmount)))
                .Where(summary => summary.DebtAmount > 0)
                .OrderByDescending(summary => summary.DebtAmount)
                .ToList();
        }
    }
}
