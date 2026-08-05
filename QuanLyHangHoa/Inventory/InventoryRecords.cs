using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Inventory;

public sealed record ProductSnapshot(int ProductId, bool IsSerialTracked);

public sealed record ProductSerialSnapshot(
    string SerialNumber,
    int ProductId,
    int? CurrentWarehouseId,
    SerialStatus Status,
    int? StockTransferLineId = null,
    int? StockInLineId = null);

/// <summary>
/// số lượng theo đơn vị tồn kho gốc tại một cặp sản phẩm-kho.
/// </summary>
public sealed record StockBalanceSnapshot(
    int ProductId,
    int WarehouseId,
    decimal OnHandQuantity,
    decimal AvailableQuantity,
    decimal ReservedQuantity);

public sealed record StockLedgerEntry(
    int DocumentId,
    string SourceDocumentType,
    int ProductId,
    int WarehouseId,
    StockLedgerDirection Direction,
    decimal Quantity,
    DateTime PostedAt,
    int PostedByUserId,
    int? ProductSerialId = null);

public sealed record AuditLogEntry(
    int DocumentId,
    AuditActionCode ActionCode,
    DateTime PerformedAt,
    int PerformedByUserId);

public sealed record PostStockInCommand(
    int DocumentId,
    int WarehouseId,
    StockInKind Kind,
    StockDocumentStatus Status,
    int ProductId,
    decimal Quantity,
    IReadOnlyCollection<string> SerialNumbers,
    int PostedByUserId,
    int? StockInLineId = null);

public sealed record PostStockOutCommand(
    int DocumentId,
    int WarehouseId,
    StockOutKind Kind,
    StockDocumentStatus Status,
    int ProductId,
    decimal Quantity,
    IReadOnlyCollection<string> SerialNumbers,
    int PostedByUserId);

public sealed record StockAdjustmentLineCommand(
    int ProductId,
    StockLedgerDirection Direction,
    decimal Quantity,
    IReadOnlyCollection<string> SerialNumbers);

public sealed record PostStockAdjustmentCommand(
    int DocumentId,
    StockDocumentStatus Status,
    string ReferenceDocumentCode,
    string Reason,
    IReadOnlyCollection<StockAdjustmentLineCommand> Lines,
    int PostedByUserId);

public sealed record PostStockTransferCommand(
    int DocumentId,
    int FromWarehouseId,
    int ToWarehouseId,
    StockDocumentStatus Status,
    int ProductId,
    decimal Quantity,
    IReadOnlyCollection<string> SerialNumbers,
    int PostedByUserId);
