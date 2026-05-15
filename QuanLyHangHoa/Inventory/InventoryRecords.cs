using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Inventory;

public sealed record ProductSnapshot(int ProductId, bool IsSerialTracked);

public sealed record ProductSerialSnapshot(
    string SerialNumber,
    int ProductId,
    int? CurrentWarehouseId,
    SerialStatus Status,
    int? StockTransferLineId = null);

public sealed record StockBalanceSnapshot(
    int ProductId,
    int WarehouseId,
    int OnHandQuantity,
    int AvailableQuantity,
    int ReservedQuantity);

public sealed record StockLedgerEntry(
    int DocumentId,
    int ProductId,
    int WarehouseId,
    StockLedgerDirection Direction,
    int Quantity,
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
    int Quantity,
    IReadOnlyCollection<string> SerialNumbers,
    int PostedByUserId);

public sealed record PostStockOutCommand(
    int DocumentId,
    int WarehouseId,
    StockOutKind Kind,
    StockDocumentStatus Status,
    int ProductId,
    int Quantity,
    IReadOnlyCollection<string> SerialNumbers,
    int PostedByUserId);

public sealed record StockAdjustmentLineCommand(
    int ProductId,
    StockLedgerDirection Direction,
    int Quantity,
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
    int Quantity,
    IReadOnlyCollection<string> SerialNumbers,
    int PostedByUserId);
