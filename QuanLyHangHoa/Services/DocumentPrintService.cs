using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;

namespace QuanLyHangHoa.Services;

public sealed class DocumentPrintService
{
    private readonly Func<AppDbContext> _contextFactory;

    public DocumentPrintService(Func<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public DocumentPrintModel LoadPurchaseInvoice(int id)
    {
        using var db = _contextFactory();
        var invoice = db.PurchaseInvoices.AsNoTracking()
            .Include(item => item.Supplier)
            .Include(item => item.Creator)
            .Include(item => item.StockIn).ThenInclude(item => item!.Warehouse)
            .Include(item => item.Lines).ThenInclude(line => line.Product)
            .Include(item => item.Lines).ThenInclude(line => line.Unit)
            .SingleOrDefault(item => item.Id == id)
            ?? throw new InvalidOperationException("Không tìm thấy hóa đơn mua.");
        return DocumentPrintModel.FromPurchaseInvoice(invoice);
    }

    public DocumentPrintModel LoadSalesInvoice(int id)
    {
        using var db = _contextFactory();
        var invoice = db.SalesInvoices.AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Creator)
            .Include(item => item.StockOut).ThenInclude(item => item!.Warehouse)
            .Include(item => item.Lines).ThenInclude(line => line.Product)
            .Include(item => item.Lines).ThenInclude(line => line.Unit)
            .SingleOrDefault(item => item.Id == id)
            ?? throw new InvalidOperationException("Không tìm thấy hóa đơn bán.");
        return DocumentPrintModel.FromSalesInvoice(invoice);
    }

    public DocumentPrintModel LoadStockIn(int id)
    {
        using var db = _contextFactory();
        var stockIn = db.StockIns.AsNoTracking()
            .Include(item => item.Supplier)
            .Include(item => item.Warehouse)
            .Include(item => item.Creator)
            .Include(item => item.Lines).ThenInclude(line => line.Product)
            .Include(item => item.Lines).ThenInclude(line => line.Unit)
            .SingleOrDefault(item => item.Id == id)
            ?? throw new InvalidOperationException("Không tìm thấy phiếu nhập kho.");
        return DocumentPrintModel.FromStockIn(stockIn);
    }

    public DocumentPrintModel LoadStockOut(int id)
    {
        using var db = _contextFactory();
        var stockOut = db.StockOuts.AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Warehouse)
            .Include(item => item.Creator)
            .Include(item => item.Lines).ThenInclude(line => line.Product)
            .Include(item => item.Lines).ThenInclude(line => line.Unit)
            .SingleOrDefault(item => item.Id == id)
            ?? throw new InvalidOperationException("Không tìm thấy phiếu xuất kho.");
        return DocumentPrintModel.FromStockOut(stockOut);
    }
}
