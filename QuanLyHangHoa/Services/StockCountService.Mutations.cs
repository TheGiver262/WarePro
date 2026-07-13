using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services;

public partial class StockCountService
{
    public void UpdateDraft(
        int sessionId,
        IReadOnlyCollection<StockCountLine> lines,
        int userId)
    {
        SaveDraftLines(sessionId, lines, userId, markCounted: false);
    }

    public void CommitSession(
        int sessionId,
        IReadOnlyCollection<StockCountLine> lines,
        int userId)
    {
        SaveDraftLines(sessionId, lines, userId, markCounted: true);
    }

    private void SaveDraftLines(
        int sessionId,
        IReadOnlyCollection<StockCountLine> lines,
        int userId,
        bool markCounted)
    {
        using var db = _contextFactory();
        using var transaction = db.Database.BeginTransaction();
        AuthorizationService.RequireFreshActor(
            db,
            userId,
            PermissionAction.PostStockAdjustment);

        var session = db.StockCountSessions
            .Include(item => item.Lines)
            .SingleOrDefault(item => item.Id == sessionId)
            ?? throw new InventoryDomainException("Stock-count session does not exist.");
        if (!string.Equals(session.Status, "nh\u00e1p", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(session.Status, DocumentStatus.Draft, StringComparison.OrdinalIgnoreCase))
        {
            throw new InventoryDomainException("Only draft stock-count sessions can be edited.");
        }

        var updates = lines.ToDictionary(item => item.Id);
        if (updates.Keys.Any(id => session.Lines.All(line => line.Id != id)))
        {
            throw new InventoryDomainException("Stock-count line does not belong to this session.");
        }

        foreach (var line in session.Lines)
        {
            if (!updates.TryGetValue(line.Id, out var update))
            {
                continue;
            }

            line.CountedQuantity = update.CountedQuantity;
            line.VarianceQuantity = update.CountedQuantity < 0
                ? 0
                : update.CountedQuantity - line.SystemQuantity;
            line.SerialNumbers = update.SerialNumbers;
        }

        if (markCounted)
        {
            session.Status = CountedStatus;
        }

        db.SaveChanges();
        transaction.Commit();
    }
}
