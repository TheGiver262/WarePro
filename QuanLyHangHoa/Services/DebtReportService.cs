using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;

namespace QuanLyHangHoa.Services
{
    public record DebtReportEntry(string PartnerCode, string PartnerName, string PhoneNumber, decimal TotalBilled, decimal TotalPaid, decimal Balance);

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
            var customers = db.Customers
                .Select(c => new
                {
                    c.CustomerCode,
                    c.DisplayName,
                    c.Phone,
                    Invoices = c.SalesInvoices!.Select(i => new { i.GrandTotal, i.PaidAmount }).ToList()
                })
                .AsNoTracking()
                .ToList();

            return customers
                .Select(x => {
                    var billed = x.Invoices.Sum(i => i.GrandTotal);
                    var paid = x.Invoices.Sum(i => i.PaidAmount);
                    return new DebtReportEntry(x.CustomerCode, x.DisplayName, x.Phone ?? "", billed, paid, billed - paid);
                })
                .Where(x => x.Balance != 0)
                .ToList();
        }

        public IReadOnlyList<DebtReportEntry> GetSupplierDebtReport()
        {
            using var db = _contextFactory();
            var suppliers = db.Suppliers
                .Select(s => new
                {
                    s.SupplierCode,
                    s.DisplayName,
                    s.Phone,
                    Invoices = s.PurchaseInvoices!.Select(i => new { i.GrandTotal, i.PaidAmount }).ToList()
                })
                .AsNoTracking()
                .ToList();

            return suppliers
                .Select(x => {
                    var billed = x.Invoices.Sum(i => i.GrandTotal);
                    var paid = x.Invoices.Sum(i => i.PaidAmount);
                    return new DebtReportEntry(x.SupplierCode, x.DisplayName, x.Phone ?? "", billed, paid, billed - paid);
                })
                .Where(x => x.Balance != 0)
                .ToList();
        }
    }
}
