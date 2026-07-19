using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services;

public partial class StockCountService
{
    public Task UpdateDraftAsync(
        int sessionId,
        IReadOnlyCollection<StockCountLine> lines,
        int userId,
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshots = SnapshotLines(lines);
        return _writeExecutor.ExecuteAsync(
            new DatabaseWriteRequest("stock-count.update-draft", operationId),
            (db, token) => StageSaveDraftLinesAsync(
                db, sessionId, snapshots, userId, markCounted: false),
            cancellationToken: cancellationToken);
    }

    internal void UpdateDraft(
        int sessionId, IReadOnlyCollection<StockCountLine> lines, int userId) =>
        UpdateDraftAsync(sessionId, lines, userId, Guid.NewGuid()).GetAwaiter().GetResult();

    public Task CommitSessionAsync(
        int sessionId,
        IReadOnlyCollection<StockCountLine> lines,
        int userId,
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshots = SnapshotLines(lines);
        return _writeExecutor.ExecuteAsync(
            new DatabaseWriteRequest("stock-count.commit-session", operationId),
            (db, token) => StageSaveDraftLinesAsync(
                db, sessionId, snapshots, userId, markCounted: true),
            (db, token) => db.StockCountSessions.AnyAsync(
                item => item.Id == sessionId && item.Status == CountedStatus, token),
            cancellationToken: cancellationToken);
    }

    internal void CommitSession(
        int sessionId, IReadOnlyCollection<StockCountLine> lines, int userId) =>
        CommitSessionAsync(sessionId, lines, userId, Guid.NewGuid()).GetAwaiter().GetResult();

    private Task StageSaveDraftLinesAsync(
        AppDbContext db,
        int sessionId,
        IReadOnlyCollection<LineUpdateSnapshot> lines,
        int userId,
        bool markCounted)
    {
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

            db.Entry(line).Property(item => item.RowVersion).OriginalValue = update.RowVersion;

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

        return Task.CompletedTask;
    }

    private static LineUpdateSnapshot[] SnapshotLines(
        IReadOnlyCollection<StockCountLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var snapshots = lines.Select(line => new LineUpdateSnapshot(
            line.Id,
            line.CountedQuantity,
            line.SerialNumbers,
            line.RowVersion.ToArray())).ToArray();
        if (snapshots.Any(line => line.Id > 0 && line.RowVersion.Length == 0))
        {
            throw new ArgumentException("RowVersion is required for stock-count updates.", nameof(lines));
        }

        return snapshots;
    }

    private sealed record LineUpdateSnapshot(
        int Id,
        decimal CountedQuantity,
        string? SerialNumbers,
        byte[] RowVersion);
}
