using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Inventory;

public sealed record ProductSnapshot(int ProductId, bool IsSerialManaged);

public sealed record ProductSerialSnapshot(
    string SerialNumber,
    int ProductId,
    int? CurrentWarehouseId,
    SerialStatus Status);

public sealed record StockBalanceSnapshot(
    int ProductId,
    int WarehouseId,
    int OnHandQuantity,
    int AvailableQuantity,
    int ReservedQuantity);

public sealed record StockLedgerEntry(
    Guid DocumentId,
    int ProductId,
    int WarehouseId,
    StockLedgerDirection Direction,
    int Quantity,
    DateTime PostedAt,
    int PostedByUserId);

public sealed record AuditLogEntry(
    Guid DocumentId,
    AuditActionCode ActionCode,
    DateTime PerformedAt,
    int PerformedByUserId);

public sealed record PostStockInCommand(
    Guid DocumentId,
    StockInKind Kind,
    StockDocumentStatus Status,
    int ProductId,
    int Quantity,
    IReadOnlyCollection<string> SerialNumbers,
    int PostedByUserId);

public sealed record PostStockOutCommand(
    Guid DocumentId,
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
    Guid DocumentId,
    StockDocumentStatus Status,
    string ReferenceDocumentCode,
    string Reason,
    IReadOnlyCollection<StockAdjustmentLineCommand> Lines,
    int PostedByUserId);
