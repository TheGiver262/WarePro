using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.ViewModels;

public partial class SalesInvoiceLineEditor
{
    public int? SourceLineId { get; set; }
    public int? SourceUnitId { get; set; }
}

public partial class PurchaseInvoiceLineEditor
{
    public int? SourceLineId { get; set; }
    public int? SourceUnitId { get; set; }
}

public partial class SalesInvoiceViewModel
{
    partial void OnSelectedStockOutChanged(StockOut? value)
    {
        if (value == null || value.Id == 0 || !_referenceDataLoaded)
        {
            return;
        }

        using var db = _contextFactory();
        var source = db.StockOuts
            .AsNoTracking()
            .Include(document => document.Lines)
            .SingleOrDefault(document => document.Id == value.Id);
        if (source == null)
        {
            return;
        }

        SelectedCustomer = AvailableCustomers.FirstOrDefault(customer => customer.Id == source.CustomerId);
        Lines.Clear();
        foreach (var line in InvoiceLinkedDocumentMapper.MapSales(source.Lines, AvailableProducts))
        {
            Lines.Add(line);
        }
    }
}

public partial class PurchaseInvoiceViewModel
{
    partial void OnSelectedStockInChanged(StockIn? value)
    {
        if (value == null || value.Id == 0 || !_referenceDataLoaded)
        {
            return;
        }

        using var db = _contextFactory();
        var source = db.StockIns
            .AsNoTracking()
            .Include(document => document.Lines)
            .SingleOrDefault(document => document.Id == value.Id);
        if (source == null)
        {
            return;
        }

        SelectedSupplier = AvailableSuppliers.FirstOrDefault(supplier => supplier.Id == source.SupplierId);
        Lines.Clear();
        foreach (var line in InvoiceLinkedDocumentMapper.MapPurchase(source.Lines, AvailableProducts))
        {
            Lines.Add(line);
        }
    }
}
