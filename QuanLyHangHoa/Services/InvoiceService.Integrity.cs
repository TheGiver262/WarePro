using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services;

public partial class InvoiceService
{
    private static StockOut? PrepareSalesInvoice(AppDbContext db, SalesInvoice invoice)
    {
        if (!invoice.StockOutId.HasValue)
        {
            invoice.Lines = CloneUnlinkedSalesLines(invoice.Lines);
            CalculateSalesInvoice(invoice);
            return null;
        }

        var stockOut = db.StockOuts
            .Include(document => document.Lines)
                .ThenInclude(line => line.Product)
            .Include(document => document.Lines)
                .ThenInclude(line => line.ProductSerials)
            .SingleOrDefault(document => document.Id == invoice.StockOutId.Value)
            ?? throw new InvalidOperationException("Linked stock-out document does not exist.");

        if (!string.Equals(stockOut.Status, DocumentStatus.Posted, StringComparison.Ordinal)
            || !string.Equals(stockOut.PurposeCode, StockOutKind.Sale.ToString(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A sales invoice can only use a posted Sale stock-out document.");
        }

        if (stockOut.CustomerId != invoice.CustomerId)
        {
            throw new InvalidOperationException("The invoice customer must match the linked stock-out customer.");
        }

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
        CalculateSalesInvoice(invoice);
        return stockOut;
    }

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

    private static void UpsertSalesInvoice(AppDbContext db, SalesInvoice invoice)
    {
        if (invoice.Id == 0)
        {
            db.SalesInvoices.Add(invoice);
            db.SaveChanges();
            return;
        }

        var existing = db.SalesInvoices
            .Include(item => item.Lines)
            .SingleOrDefault(item => item.Id == invoice.Id)
            ?? throw new InvalidOperationException("Sales invoice does not exist.");
        db.SalesInvoiceLines.RemoveRange(existing.Lines);
        db.Entry(existing).CurrentValues.SetValues(invoice);
        foreach (var line in invoice.Lines)
        {
            line.Id = 0;
            line.SalesInvoiceId = existing.Id;
            db.SalesInvoiceLines.Add(line);
        }
        db.SaveChanges();
    }

    private static void UpsertPurchaseInvoice(AppDbContext db, PurchaseInvoice invoice)
    {
        if (invoice.Id == 0)
        {
            db.PurchaseInvoices.Add(invoice);
            db.SaveChanges();
            return;
        }

        var existing = db.PurchaseInvoices
            .Include(item => item.Lines)
            .SingleOrDefault(item => item.Id == invoice.Id)
            ?? throw new InvalidOperationException("Purchase invoice does not exist.");
        db.PurchaseInvoiceLines.RemoveRange(existing.Lines);
        db.Entry(existing).CurrentValues.SetValues(invoice);
        foreach (var line in invoice.Lines)
        {
            line.Id = 0;
            line.PurchaseInvoiceId = existing.Id;
            db.PurchaseInvoiceLines.Add(line);
        }
        db.SaveChanges();
    }

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

        db.SaveChanges();
    }

    private static void SetCoverageValues(WarrantyCoverage coverage, SalesInvoice invoice, int months)
    {
        coverage.CustomerId = invoice.CustomerId;
        coverage.WarrantyStartDate = invoice.InvoiceDate;
        coverage.WarrantyEndDate = invoice.InvoiceDate.AddMonths(months);
        coverage.CoverageStatus = "Active";
    }

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
