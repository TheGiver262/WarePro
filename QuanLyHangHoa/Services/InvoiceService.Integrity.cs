using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services;

public partial class InvoiceService
{
    // hóa đơn độc lập dùng dòng người dùng nhập; hóa đơn liên kết phải lấy số lượng, đơn vị và giá từ phiếu xuất đã ghi sổ
    private static StockOut? PrepareSalesInvoice(AppDbContext db, SalesInvoice invoice)
    {
        if (!invoice.StockOutId.HasValue)
        {
            invoice.Lines = CloneUnlinkedSalesLines(invoice.Lines);
            CalculateSalesInvoice(invoice);
            return null;
        }

        // nạp product và serial vì vừa đối soát dòng vừa tạo phạm vi bảo hành cho từng serial bán ra
        var stockOut = db.StockOuts
            .Include(document => document.Lines)
                .ThenInclude(line => line.Product)
            .Include(document => document.Lines)
                .ThenInclude(line => line.ProductSerials)
            .SingleOrDefault(document => document.Id == invoice.StockOutId.Value)
            ?? throw new InvalidOperationException("Linked stock-out document does not exist.");

        // lưu hóa đơn không post tồn kho; phiếu xuất phải hoàn tất trước rồi mới được dùng làm nguồn chuẩn.
        if (!string.Equals(stockOut.Status, DocumentStatus.Posted, StringComparison.Ordinal)
            || !string.Equals(stockOut.PurposeCode, StockOutKind.Sale.ToString(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A sales invoice can only use a posted Sale stock-out document.");
        }

        if (stockOut.CustomerId != invoice.CustomerId)
        {
            throw new InvalidOperationException("The invoice customer must match the linked stock-out customer.");
        }

        // kiểm tra trong transaction serializable để hai client không cùng gắn một phiếu xuất cho hai hóa đơn.
        if (db.SalesInvoices.Any(existing => existing.StockOutId == stockOut.Id && existing.Id != invoice.Id))
        {
            throw new InvalidOperationException("The linked stock-out document is already used by another invoice.");
        }

        var invalidWarrantyProduct = stockOut.Lines
            .Select(line => line.Product)
            .FirstOrDefault(product => product.WarrantyPeriodMonths < 0);
        if (invalidWarrantyProduct != null)
        {
            throw new InvalidOperationException("Product warranty period cannot be negative.");
        }

        invoice.Lines = DeriveSalesLines(db, invoice.Lines, stockOut.Lines);
        // tổng tiền và trạng thái thanh toán được tính lại từ dòng chuẩn trước khi cùng transaction lưu xuống database.
        CalculateSalesInvoice(invoice);
        return stockOut;
    }

    // phiếu nhập chỉ được dùng một lần, phải là loại Purchase đã posted và cùng nhà cung cấp với hóa đơn
    private static StockIn? PreparePurchaseInvoice(AppDbContext db, PurchaseInvoice invoice)
    {
        if (!invoice.StockInId.HasValue)
        {
            invoice.Lines = CloneUnlinkedPurchaseLines(invoice.Lines);
            CalculatePurchaseInvoice(invoice);
            return null;
        }

        var stockIn = db.StockIns
            .Include(document => document.Lines)
                .ThenInclude(line => line.Product)
            .SingleOrDefault(document => document.Id == invoice.StockInId.Value)
            ?? throw new InvalidOperationException("Linked stock-in document does not exist.");

        if (!string.Equals(stockIn.Status, DocumentStatus.Posted, StringComparison.Ordinal)
            || !string.Equals(stockIn.PurposeCode, StockInKind.Purchase.ToString(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A purchase invoice can only use a posted Purchase stock-in document.");
        }

        if (stockIn.SupplierId != invoice.SupplierId)
        {
            throw new InvalidOperationException("The invoice supplier must match the linked stock-in supplier.");
        }

        if (db.PurchaseInvoices.Any(existing => existing.StockInId == stockIn.Id && existing.Id != invoice.Id))
        {
            throw new InvalidOperationException("The linked stock-in document is already used by another invoice.");
        }

        invoice.Lines = DerivePurchaseLines(db, invoice.Lines, stockIn.Lines);
        CalculatePurchaseInvoice(invoice);
        return stockIn;
    }

    // unmatched là bản sao các dòng UI chưa ghép; mỗi dòng kho phải lấy đúng một dòng và cuối cùng danh sách phải rỗng
    private static List<SalesInvoiceLine> DeriveSalesLines(
        AppDbContext db,
        IEnumerable<SalesInvoiceLine>? requestedLines,
        IEnumerable<StockOutLine> sourceLines)
    {
        var unmatched = requestedLines?.ToList() ?? new List<SalesInvoiceLine>();
        var result = new List<SalesInvoiceLine>();

        foreach (var source in sourceLines)
        {
            var requested = TakeMatchingLine(
                unmatched,
                source.Id,
                source.ProductId,
                source.UnitId,
                source.Quantity);
            ValidateBaseQuantity(db, source.Product, source.UnitId, source.Quantity, source.BaseQuantity);
            result.Add(new SalesInvoiceLine
            {
                ProductId = source.ProductId,
                UnitId = source.UnitId,
                StockOutLineId = source.Id,
                Quantity = source.Quantity,
                UnitPrice = source.UnitPrice,
                TaxRate = requested.TaxRate
            });
        }

        if (unmatched.Count != 0 || result.Count == 0)
        {
            throw new InvalidOperationException("Invoice lines must exactly match the linked stock-out lines.");
        }

        return result;
    }

    // chỉ TaxRate được lấy từ hóa đơn; product, unit, quantity và unit price lấy từ chứng từ kho làm nguồn chuẩn
    private static List<PurchaseInvoiceLine> DerivePurchaseLines(
        AppDbContext db,
        IEnumerable<PurchaseInvoiceLine>? requestedLines,
        IEnumerable<StockInLine> sourceLines)
    {
        var unmatched = requestedLines?.ToList() ?? new List<PurchaseInvoiceLine>();
        var result = new List<PurchaseInvoiceLine>();

        foreach (var source in sourceLines)
        {
            var requested = TakeMatchingLine(
                unmatched,
                source.Id,
                source.ProductId,
                source.UnitId,
                source.Quantity);
            ValidateBaseQuantity(db, source.Product, source.UnitId, source.Quantity, source.BaseQuantity);
            result.Add(new PurchaseInvoiceLine
            {
                ProductId = source.ProductId,
                UnitId = source.UnitId,
                StockInLineId = source.Id,
                Quantity = source.Quantity,
                UnitPrice = source.UnitPrice,
                TaxRate = requested.TaxRate
            });
        }

        if (unmatched.Count != 0 || result.Count == 0)
        {
            throw new InvalidOperationException("Invoice lines must exactly match the linked stock-in lines.");
        }

        return result;
    }

    // xóa khỏi unmatched sau khi khớp để một dòng hóa đơn không được dùng cho hai dòng xuất giống nhau
    private static SalesInvoiceLine TakeMatchingLine(
        List<SalesInvoiceLine> lines,
        int sourceLineId,
        int productId,
        int unitId,
        decimal quantity)
    {
        var match = lines.FirstOrDefault(line =>
            (!line.StockOutLineId.HasValue || line.StockOutLineId == sourceLineId)
            && line.ProductId == productId
            && line.UnitId == unitId
            && line.Quantity == quantity)
            ?? throw new InvalidOperationException("Invoice lines must exactly match the linked stock-out lines.");
        lines.Remove(match);
        return match;
    }

    private static PurchaseInvoiceLine TakeMatchingLine(
        List<PurchaseInvoiceLine> lines,
        int sourceLineId,
        int productId,
        int unitId,
        decimal quantity)
    {
        var match = lines.FirstOrDefault(line =>
            (!line.StockInLineId.HasValue || line.StockInLineId == sourceLineId)
            && line.ProductId == productId
            && line.UnitId == unitId
            && line.Quantity == quantity)
            ?? throw new InvalidOperationException("Invoice lines must exactly match the linked stock-in lines.");
        lines.Remove(match);
        return match;
    }

    // kiểm tra quantity * conversionFactor đúng bằng baseQuantity để hóa đơn không che lỗi quy đổi đơn vị của chứng từ kho
    private static void ValidateBaseQuantity(
        AppDbContext db,
        Product product,
        int unitId,
        decimal quantity,
        decimal baseQuantity)
    {
        var factor = product.DefaultUnitId == unitId
            ? 1m
            : db.ProductUnits
                .Where(unit => unit.ProductId == product.Id && unit.UnitId == unitId)
                .Select(unit => (decimal?)unit.ConversionFactor)
                .SingleOrDefault()
                ?? throw new InvalidOperationException("The stock line has no valid product-unit conversion.");

        if (factor <= 0 || quantity * factor != baseQuantity)
        {
            throw new InvalidOperationException("The stock line base quantity does not match its product unit.");
        }
    }

    // clone bỏ id và navigation do UI mang về, tránh EF attach nhầm entity cũ khi lưu
    private static List<SalesInvoiceLine> CloneUnlinkedSalesLines(IEnumerable<SalesInvoiceLine>? lines) =>
        lines?.Select(line => new SalesInvoiceLine
        {
            ProductId = line.ProductId,
            UnitId = line.UnitId,
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice,
            TaxRate = line.TaxRate
        }).ToList() ?? new List<SalesInvoiceLine>();

    private static List<PurchaseInvoiceLine> CloneUnlinkedPurchaseLines(IEnumerable<PurchaseInvoiceLine>? lines) =>
        lines?.Select(line => new PurchaseInvoiceLine
        {
            ProductId = line.ProductId,
            UnitId = line.UnitId,
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice,
            TaxRate = line.TaxRate
        }).ToList() ?? new List<PurchaseInvoiceLine>();

    // khi sửa, xóa toàn bộ dòng cũ rồi tạo lại từ tập đã kiểm tra để không còn dòng thừa
    private static async Task<int> UpsertSalesInvoiceAsync(
        AppDbContext db,
        SalesInvoice invoice,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken)
    {
        if (invoice.Id == 0)
        {
            db.SalesInvoices.Add(invoice);
            // flush trong transaction để SQL gán id hóa đơn trước khi tạo coverage tham chiếu đến hóa đơn này.
            await db.SaveChangesAsync(cancellationToken);
            return invoice.Id;
        }

        var existing = await db.SalesInvoices
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == invoice.Id, cancellationToken)
            ?? throw new InvalidOperationException("Sales invoice does not exist.");

        EnsureActive(existing.Status, "Sales invoice");
        // đặt OriginalValue từ client để câu update mang điều kiện rowversion; sai mốc sẽ thành DbUpdateConcurrencyException.
        db.Entry(existing).Property(item => item.RowVersion).OriginalValue = expectedRowVersion;
        db.SalesInvoiceLines.RemoveRange(existing.Lines);
        existing.InvoiceCode = invoice.InvoiceCode;
        existing.CustomerId = invoice.CustomerId;
        existing.StockOutId = invoice.StockOutId;
        existing.InvoiceDate = invoice.InvoiceDate;
        existing.SubTotal = invoice.SubTotal;
        existing.TaxAmount = invoice.TaxAmount;
        existing.GrandTotal = invoice.GrandTotal;
        existing.PaidAmount = invoice.PaidAmount;
        existing.PaymentStatus = invoice.PaymentStatus;
        existing.DueDate = invoice.DueDate;
        existing.Notes = invoice.Notes;
        db.Entry(existing).Property(item => item.Notes).IsModified = true;

        foreach (var line in invoice.Lines)
        {
            // dòng đã kiểm tra được tạo mới hoàn toàn; reset id tránh EF hiểu nhầm là sửa dòng thuộc graph cũ.
            line.Id = 0;
            line.SalesInvoiceId = existing.Id;
            db.SalesInvoiceLines.Add(line);
        }

        invoice.Id = existing.Id;
        return existing.Id;
    }

    private static async Task<int> UpsertPurchaseInvoiceAsync(
        AppDbContext db,
        PurchaseInvoice invoice,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken)
    {
        if (invoice.Id == 0)
        {
            db.PurchaseInvoices.Add(invoice);
            // flush lấy khóa do SQL sinh ra; executor vẫn chỉ commit sau khi toàn bộ mutation và SaveChanges cuối cùng thành công.
            await db.SaveChangesAsync(cancellationToken);
            return invoice.Id;
        }

        var existing = await db.PurchaseInvoices
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == invoice.Id, cancellationToken)
            ?? throw new InvalidOperationException("Purchase invoice does not exist.");

        EnsureActive(existing.Status, "Purchase invoice");
        // dùng cùng rowversion client đã đọc; xung đột dừng lưu thay vì retry rồi ghi đè thay đổi của máy khác.
        db.Entry(existing).Property(item => item.RowVersion).OriginalValue = expectedRowVersion;
        db.PurchaseInvoiceLines.RemoveRange(existing.Lines);
        existing.InvoiceCode = invoice.InvoiceCode;
        existing.SupplierId = invoice.SupplierId;
        existing.StockInId = invoice.StockInId;
        existing.InvoiceDate = invoice.InvoiceDate;
        existing.SubTotal = invoice.SubTotal;
        existing.TaxAmount = invoice.TaxAmount;
        existing.GrandTotal = invoice.GrandTotal;
        existing.PaidAmount = invoice.PaidAmount;
        existing.PaymentStatus = invoice.PaymentStatus;
        existing.DueDate = invoice.DueDate;
        existing.Notes = invoice.Notes;
        db.Entry(existing).Property(item => item.Notes).IsModified = true;

        foreach (var line in invoice.Lines)
        {
            line.Id = 0;
            line.PurchaseInvoiceId = existing.Id;
            db.PurchaseInvoiceLines.Add(line);
        }

        invoice.Id = existing.Id;
        return existing.Id;
    }

    // desired là serial đáng được bảo hành theo phiếu xuất hiện tại; existing là coverage từng do hóa đơn này tạo
    private static void ReconcileWarrantyCoverages(
        AppDbContext db,
        SalesInvoice invoice,
        StockOut? stockOut)
    {
        var desired = stockOut?.Lines
            .Where(line => line.Product.WarrantyPeriodMonths > 0)
            .SelectMany(line => line.ProductSerials.Select(serial => new
            {
                SerialId = serial.Id,
                Months = line.Product.WarrantyPeriodMonths
            }))
            .ToDictionary(item => item.SerialId, item => item.Months)
            ?? new Dictionary<int, int>();

        var existing = db.WarrantyCoverages
            .Where(coverage => coverage.SalesInvoiceId == invoice.Id)
            .ToList();
        // coverage đã chuyển sang serial thay thế hoặc inactive là lịch sử, không được hóa đơn cũ kích hoạt lại
        var replacementSerialIds = db.WarrantyClaims
            .Where(claim => claim.ReplacementSerialId.HasValue)
            .Select(claim => claim.ReplacementSerialId!.Value)
            .ToHashSet();

        foreach (var coverage in existing)
        {
            var isDesired = desired.Remove(coverage.ProductSerialId, out var months);
            if (replacementSerialIds.Contains(coverage.ProductSerialId)
                || coverage.CoverageStatus == "Inactive")
            {
                continue;
            }

            if (!isDesired)
            {
                if (coverage.CoverageStatus == "Active")
                {
                    coverage.CoverageStatus = "Voided";
                }
                continue;
            }

            if (coverage.CoverageStatus is "Active" or "Voided")
            {
                SetCoverageValues(coverage, invoice, months);
            }
        }

        // serial còn thiếu coverage được tạo mới, nhưng không được có coverage active từ hóa đơn khác
        foreach (var item in desired)
        {
            if (db.WarrantyCoverages.Any(coverage =>
                    coverage.ProductSerialId == item.Key
                    && coverage.CoverageStatus == "Active"
                    && coverage.SalesInvoiceId != invoice.Id))
            {
                throw new InvalidOperationException("A serial already has active warranty coverage from another invoice.");
            }

            var coverage = new WarrantyCoverage
            {
                ProductSerialId = item.Key,
                SalesInvoiceId = invoice.Id
            };
            SetCoverageValues(coverage, invoice, item.Value);
            db.WarrantyCoverages.Add(coverage);
        }

    }

    // thời hạn bắt đầu từ ngày hóa đơn bán và kết thúc sau đúng số tháng bảo hành của sản phẩm
    private static void SetCoverageValues(WarrantyCoverage coverage, SalesInvoice invoice, int months)
    {
        coverage.CustomerId = invoice.CustomerId;
        coverage.WarrantyStartDate = invoice.InvoiceDate;
        coverage.WarrantyEndDate = invoice.InvoiceDate.AddMonths(months);
        coverage.CoverageStatus = "Active";
    }

    // Overdue là trạng thái hiệu lực theo hôm nay; có thể hiển thị mà không cần ghi lại mọi hóa đơn mỗi ngày
    private static void MarkEffectivePaymentStatus(IEnumerable<SalesInvoice> invoices)
    {
        foreach (var invoice in invoices)
        {
            if (IsEffectivelyOverdue(invoice.DueDate, invoice.PaymentStatus))
            {
                invoice.PaymentStatus = PaymentStatus.Overdue;
            }
        }
    }

    private static void MarkEffectivePaymentStatus(IEnumerable<PurchaseInvoice> invoices)
    {
        foreach (var invoice in invoices)
        {
            if (IsEffectivelyOverdue(invoice.DueDate, invoice.PaymentStatus))
            {
                invoice.PaymentStatus = PaymentStatus.Overdue;
            }
        }
    }

    private static bool IsEffectivelyOverdue(DateTime? dueDate, string paymentStatus) =>
        dueDate.HasValue
        && dueDate.Value.Date < DateTime.Today
        && !string.Equals(paymentStatus, PaymentStatus.Paid, StringComparison.Ordinal);
}
