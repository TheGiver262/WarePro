using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services;

public partial class InvoiceService
{
    public Task VoidSalesInvoiceAsync(
        int invoiceId,
        byte[] expectedRowVersion,
        string reason,
        int actorId,
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expectedRowVersion);
        var rowVersion = expectedRowVersion.ToArray();
        var normalizedReason = ValidateVoidRequest(invoiceId, rowVersion, reason);
        var voidedAt = DateTime.Now;

        return _writeExecutor.ExecuteAsync(
            new DatabaseWriteRequest(
                "invoice.sales.void",
                operationId,
                System.Data.IsolationLevel.Serializable),
            async (db, token) =>
            {
                AuthorizationService.RequireFreshActor(
                    db,
                    actorId,
                    PermissionAction.CreateSalesInvoice);

                var invoice = await db.SalesInvoices
                    .SingleOrDefaultAsync(item => item.Id == invoiceId, token)
                    ?? throw new InvalidOperationException("Sales invoice does not exist.");
                EnsureActive(invoice.Status, "Sales invoice");
                db.Entry(invoice).Property(item => item.RowVersion).OriginalValue = rowVersion;
                invoice.Status = InvoiceStatus.Voided;
                invoice.Notes = AppendVoidNote(invoice.Notes, normalizedReason, actorId, voidedAt);
                db.Entry(invoice).Property(item => item.Notes).IsModified = true;

                var activeCoverages = await db.WarrantyCoverages
                    .Where(item => item.SalesInvoiceId == invoiceId && item.CoverageStatus == "Active")
                    .ToListAsync(token);
                foreach (var coverage in activeCoverages)
                {
                    coverage.CoverageStatus = "Voided";
                }
            },
            async (db, token) =>
            {
                var invoiceWasVoided = await db.SalesInvoices.AsNoTracking().AnyAsync(item =>
                    item.Id == invoiceId
                    && item.Status == InvoiceStatus.Voided
                    && item.Notes != null
                    && item.Notes.Contains(normalizedReason)
                    && item.RowVersion != rowVersion,
                    token);
                return invoiceWasVoided
                    && !await db.WarrantyCoverages.AsNoTracking().AnyAsync(item =>
                        item.SalesInvoiceId == invoiceId && item.CoverageStatus == "Active",
                        token);
            },
            entityKey: invoiceId.ToString(),
            cancellationToken: cancellationToken);
    }

    public Task VoidPurchaseInvoiceAsync(
        int invoiceId,
        byte[] expectedRowVersion,
        string reason,
        int actorId,
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expectedRowVersion);
        var rowVersion = expectedRowVersion.ToArray();
        var normalizedReason = ValidateVoidRequest(invoiceId, rowVersion, reason);
        var voidedAt = DateTime.Now;

        return _writeExecutor.ExecuteAsync(
            new DatabaseWriteRequest(
                "invoice.purchase.void",
                operationId,
                System.Data.IsolationLevel.Serializable),
            async (db, token) =>
            {
                AuthorizationService.RequireFreshActor(
                    db,
                    actorId,
                    PermissionAction.CreatePurchaseInvoice);

                var invoice = await db.PurchaseInvoices
                    .SingleOrDefaultAsync(item => item.Id == invoiceId, token)
                    ?? throw new InvalidOperationException("Purchase invoice does not exist.");
                EnsureActive(invoice.Status, "Purchase invoice");
                db.Entry(invoice).Property(item => item.RowVersion).OriginalValue = rowVersion;
                invoice.Status = InvoiceStatus.Voided;
                invoice.Notes = AppendVoidNote(invoice.Notes, normalizedReason, actorId, voidedAt);
                db.Entry(invoice).Property(item => item.Notes).IsModified = true;
            },
            (db, token) => db.PurchaseInvoices.AsNoTracking().AnyAsync(item =>
                item.Id == invoiceId
                && item.Status == InvoiceStatus.Voided
                && item.Notes != null
                && item.Notes.Contains(normalizedReason)
                && item.RowVersion != rowVersion,
                token),
            entityKey: invoiceId.ToString(),
            cancellationToken: cancellationToken);
    }

    private static string ValidateVoidRequest(int invoiceId, byte[] expectedRowVersion, string reason)
    {
        if (invoiceId <= 0)
            throw new ArgumentOutOfRangeException(nameof(invoiceId));
        if (expectedRowVersion.Length == 0)
            throw new ArgumentException("Expected row version is required.", nameof(expectedRowVersion));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Void reason is required.", nameof(reason));

        return reason.Trim();
    }

    private static void EnsureActive(string status, string documentName)
    {
        if (status == InvoiceStatus.Voided)
            throw new InvalidOperationException($"{documentName} is already voided and cannot be changed.");
        if (status != InvoiceStatus.Active)
            throw new InvalidOperationException($"{documentName} has an invalid status.");
    }

    private static string AppendVoidNote(
        string? notes,
        string reason,
        int actorId,
        DateTime voidedAt)
    {
        var entry = $"[HỦY {voidedAt:dd/MM/yyyy HH:mm} - người dùng {actorId}] {reason}";
        return string.IsNullOrWhiteSpace(notes)
            ? entry
            : $"{notes.TrimEnd()}{Environment.NewLine}{entry}";
    }
}
