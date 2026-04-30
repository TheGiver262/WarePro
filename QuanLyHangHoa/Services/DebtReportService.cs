using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;

namespace QuanLyHangHoa.Services
{
    public record DebtReportEntry(string PartnerName, decimal TotalBilled, decimal TotalPaid, decimal Balance);

    public class DebtReportService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public DebtReportService() : this(() => new AppDbContext()) { }

        public DebtReportService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public IReadOnlyList<DebtReportEntry> GetCustomerDebtReport()
        {
            using var db = _contextFactory();
            return db.Customers
                .AsNoTracking()
                .Include(c => c.SalesInvoices)
                .Select(c => new DebtReportEntry(
                    c.DisplayName,
                    c.SalesInvoices != null ? c.SalesInvoices.Sum(i => i.GrandTotal) : 0,
                    c.SalesInvoices != null ? c.SalesInvoices.Sum(i => i.PaidAmount) : 0,
                    c.SalesInvoices != null ? c.SalesInvoices.Sum(i => i.GrandTotal - i.PaidAmount) : 0))
                .Where(r => r.Balance != 0)
                .ToList();
        }

        public IReadOnlyList<DebtReportEntry> GetSupplierDebtReport()
        {
            using var db = _contextFactory();
            return db.Suppliers
                .AsNoTracking()
                .Include(s => s.PurchaseInvoices)
                .Select(s => new DebtReportEntry(
                    s.DisplayName,
                    s.PurchaseInvoices != null ? s.PurchaseInvoices.Sum(i => i.GrandTotal) : 0,
                    s.PurchaseInvoices != null ? s.PurchaseInvoices.Sum(i => i.PaidAmount) : 0,
                    s.PurchaseInvoices != null ? s.PurchaseInvoices.Sum(i => i.GrandTotal - i.PaidAmount) : 0))
                .Where(r => r.Balance != 0)
                .ToList();
        }
    }
}
