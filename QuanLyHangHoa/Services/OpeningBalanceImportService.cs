using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services.DataImport;

namespace QuanLyHangHoa.Services;

public sealed class OpeningBalanceImportService
{
    private const string UnauthorizedMessage = "The current user is not authorized for this action.";
    private const string PayloadMismatchMessage =
        "The operation ID was already used with a different import payload.";
    private readonly DatabaseWriteExecutor _writeExecutor;
    private readonly ExcelImportService _excelImportService = new();
    private readonly CsvImportService _csvImportService = new();

    public OpeningBalanceImportService(Func<AppDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _writeExecutor = new DatabaseWriteExecutor(contextFactory);
    }

    internal ImportResult<OpeningBalanceImportRow> ImportFile(
        string filePath,
        int postedByUserId) =>
        ImportFileAsync(filePath, postedByUserId, Guid.NewGuid())
            .GetAwaiter()
            .GetResult();

    public async Task<ImportResult<OpeningBalanceImportRow>> ImportFileAsync(
        string filePath,
        int postedByUserId,
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var parsed = extension switch
        {
            ".xlsx" or ".xls" => _excelImportService.Import<OpeningBalanceImportRow>(filePath),
            ".csv" => _csvImportService.Import<OpeningBalanceImportRow>(filePath),
            _ => throw new NotSupportedException("Định dạng file không được hỗ trợ.")
        };

        if (parsed.Errors.Count > 0)
        {
            return parsed;
        }

        return await ImportRowsAsync(
            parsed.ImportedItems,
            postedByUserId,
            operationId,
            cancellationToken);
    }

    internal ImportResult<OpeningBalanceImportRow> ImportRows(
        IEnumerable<OpeningBalanceImportRow> rows,
        int postedByUserId) =>
        ImportRowsAsync(rows, postedByUserId, Guid.NewGuid())
            .GetAwaiter()
            .GetResult();

    public async Task<ImportResult<OpeningBalanceImportRow>> ImportRowsAsync(
        IEnumerable<OpeningBalanceImportRow> rows,
        int postedByUserId,
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        cancellationToken.ThrowIfCancellationRequested();

        // clone thành snapshot scalar vì executor có thể gọi lại callback; không đọc lại enumerable hoặc object do bên gọi còn giữ.
        var sourceRows = rows.Select(CloneRow).ToArray();
        var shapeResult = new ImportResult<OpeningBalanceImportRow>();
        var preparedRows = PrepareScalarRows(sourceRows, shapeResult);
        if (shapeResult.Errors.Count > 0)
        {
            return shapeResult;
        }

        var payloadMarker = CreatePayloadMarker(preparedRows);
        var documentCode = $"SI-OB-{operationId:N}";
        var postedAt = DateTime.UtcNow;
        // ảnh chụp này dùng để phân biệt lần ghi đã commit nhưng client mất kết nối với một lần ghi chưa hoàn tất.
        var expectedCommittedRows = preparedRows
            .Select(row => (
                row.ProductId,
                row.Quantity,
                string.Join(
                    ',',
                    row.SerialNumbers
                        .Select(serial => serial.Trim().ToUpperInvariant())
                        .OrderBy(serial => serial, StringComparer.Ordinal))))
            .ToArray();

        try
        {
            return await _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest(
                    "opening-balance.import",
                    operationId,
                    IsolationLevel.Serializable),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(
                        db,
                        postedByUserId,
                        PermissionAction.PostStockIn);

                    var existingDocument = await db.StockIns
                        .AsNoTracking()
                        .SingleOrDefaultAsync(
                            stockIn => stockIn.DocumentCode == documentCode,
                            token);
                    if (existingDocument is not null)
                    {
                        EnsurePayloadMatches(existingDocument.Notes, payloadMarker);
                        var committedRowCount = await db.StockInLines
                            .CountAsync(
                                line => line.StockInId == existingDocument.Id,
                                token);
                        return Success(sourceRows, committedRowCount);
                    }

                    // quy tắc phụ thuộc database phải kiểm tra lại trong từng attempt vì sản phẩm, đơn vị hoặc serial có thể vừa đổi.
                    var validationResult = new ImportResult<OpeningBalanceImportRow>();
                    var validatedRows = await ValidateDatabaseRulesAsync(
                        db,
                        preparedRows,
                        validationResult,
                        token);
                    if (validationResult.Errors.Count > 0)
                    {
                        return validationResult;
                    }

                    var warehouseId = await db.Warehouses
                        .Where(warehouse => warehouse.IsActive)
                        .OrderByDescending(warehouse => warehouse.IsDefault)
                        .ThenBy(warehouse => warehouse.Id)
                        .Select(warehouse => warehouse.Id)
                        .FirstOrDefaultAsync(token);
                    if (warehouseId == 0)
                    {
                        throw new InventoryDomainException(
                            "Không tìm thấy kho đang hoạt động để nhập tồn đầu kỳ.");
                    }

                    var stockIn = new StockIn
                    {
                        DocumentCode = documentCode,
                        WarehouseId = warehouseId,
                        PurposeCode = "OpeningBalance",
                        Status = StockDocumentStatus.Approved.ToString(),
                        ImportDate = postedAt,
                        Notes = $"Import tồn đầu kỳ từ Excel/CSV {payloadMarker}",
                        CreatedBy = postedByUserId,
                        CreatedAt = postedAt,
                        ApprovedBy = postedByUserId,
                        ApprovedAt = postedAt,
                        PostedBy = postedByUserId,
                        PostedAt = postedAt
                    };
                    db.StockIns.Add(stockIn);

                    // giữ cặp dữ liệu đã kiểm tra và entity để sau flush có đúng line id khi nối lịch sử serial.
                    var persistedLines = new List<(ValidatedOpeningBalanceRow Prepared, StockInLine Line)>();
                    foreach (var prepared in validatedRows.OrderBy(row => row.ProductId))
                    {
                        var line = new StockInLine
                        {
                            StockIn = stockIn,
                            ProductId = prepared.ProductId,
                            UnitId = prepared.UnitId,
                            Quantity = prepared.Quantity,
                            BaseQuantity = prepared.Quantity,
                            UnitPrice = prepared.UnitPrice,
                            DraftSerials = prepared.SerialNumbers.Length == 0
                                ? null
                                : string.Join(",", prepared.SerialNumbers)
                        };
                        db.StockInLines.Add(line);
                        persistedLines.Add((prepared, line));
                    }

                    await db.SaveChangesAsync(token);
                    // flush để lấy stockIn.Id cho posting và line.Id cho liên kết lịch sử serial.
                    var warehouseProvider = new FixedWarehouseProvider(warehouseId);
                    var postingService = new InventoryPostingService(
                        new EfInventoryUnitOfWork(db),
                        warehouseProvider,
                        new FixedClock(postedAt));

                    foreach (var item in persistedLines)
                    {
                        token.ThrowIfCancellationRequested();
                        // sổ kho và số dư chỉ đi qua posting service; import không tự cộng tồn để tránh hai nguồn sự thật.
                        postingService.PostStockIn(new PostStockInCommand(
                            stockIn.Id,
                            warehouseId,
                            StockInKind.OpeningBalance,
                            StockDocumentStatus.Approved,
                            item.Prepared.ProductId,
                            item.Prepared.Quantity,
                            item.Prepared.SerialNumbers,
                            postedByUserId));

                        if (item.Prepared.SerialNumbers.Length > 0)
                        {
                            var serials = await db.ProductSerials
                                .Where(serial => item.Prepared.SerialNumbers.Contains(serial.SerialNumber))
                                .ToListAsync(token);
                            foreach (var serial in serials)
                            {
                                serial.LastStockInLineId = item.Line.Id;
                            }
                        }
                    }

                    return Success(sourceRows, sourceRows.Length);
                },
                // verifier đọc trạng thái tự nhiên của phiếu, dòng và marker thay vì tin riêng operation id.
                (db, token) => VerifyCommittedRowsAsync(
                    db,
                    documentCode,
                    payloadMarker,
                    expectedCommittedRows,
                    token),
                entityKey: documentCode,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException ex) when (
            string.Equals(ex.Message, UnauthorizedMessage, StringComparison.Ordinal))
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ImportResult<OpeningBalanceImportRow>
            {
                Errors =
                [
                    new RowError
                    {
                        RowNumber = 0,
                        Data = "Chứng từ nhập tồn đầu kỳ",
                        ErrorMessage = GetRootMessage(ex)
                    }
                ]
            };
        }
    }

    private static List<PreparedOpeningBalanceRow> PrepareScalarRows(
        IReadOnlyCollection<OpeningBalanceImportRow> sourceRows,
        ImportResult<OpeningBalanceImportRow> result)
    {
        var prepared = new List<PreparedOpeningBalanceRow>();
        if (sourceRows.Count == 0)
        {
            result.Errors.Add(new RowError
            {
                RowNumber = 0,
                ErrorMessage = "Không có dữ liệu tồn đầu kỳ để import."
            });
            return prepared;
        }

        var documentSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in sourceRows)
        {
            try
            {
                if (row.Quantity <= 0)
                {
                    throw new InventoryDomainException(
                        "Stock-in quantity must be greater than zero.");
                }

                var serialNumbers = StockInService.ParseSerialRange(row.SerialNumbers)
                    .Select(serial => serial.Trim())
                    .Where(serial => serial.Length > 0)
                    .ToArray();
                if (serialNumbers.Length !=
                    serialNumbers.Distinct(StringComparer.OrdinalIgnoreCase).Count())
                {
                    throw new InventoryDomainException("Duplicate serials are not allowed.");
                }

                if (serialNumbers.Any(serial => !documentSerials.Add(serial)))
                {
                    throw new InventoryDomainException("Duplicate serials are not allowed.");
                }

                prepared.Add(new PreparedOpeningBalanceRow(
                    row.RowNumber,
                    row.ProductId,
                    row.Quantity,
                    serialNumbers));
            }
            catch (InventoryDomainException ex)
            {
                result.Errors.Add(ToRowError(row, ex.Message));
            }
        }

        return prepared;
    }

    private static async Task<List<ValidatedOpeningBalanceRow>> ValidateDatabaseRulesAsync(
        AppDbContext db,
        IReadOnlyCollection<PreparedOpeningBalanceRow> preparedRows,
        ImportResult<OpeningBalanceImportRow> result,
        CancellationToken cancellationToken)
    {
        var productIds = preparedRows
            .Select(row => row.ProductId)
            .Distinct()
            .ToArray();
        var products = await db.Products
            .AsNoTracking()
            .Where(product => productIds.Contains(product.Id) && product.IsActive)
            .ToDictionaryAsync(product => product.Id, cancellationToken);
        var baseUnitIds = await db.ProductUnits
            .AsNoTracking()
            .Where(unit => productIds.Contains(unit.ProductId) && unit.IsBaseUnit)
            .GroupBy(unit => unit.ProductId)
            .Select(group => new { ProductId = group.Key, UnitId = group.Min(unit => unit.UnitId) })
            .ToDictionaryAsync(item => item.ProductId, item => item.UnitId, cancellationToken);
        var requestedSerials = preparedRows.SelectMany(row => row.SerialNumbers).ToArray();
        var existingSerials = requestedSerials.Length == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(
                await db.ProductSerials
                    .AsNoTracking()
                    .Where(serial => requestedSerials.Contains(serial.SerialNumber))
                    .Select(serial => serial.SerialNumber)
                    .ToListAsync(cancellationToken),
                StringComparer.OrdinalIgnoreCase);

        var validated = new List<ValidatedOpeningBalanceRow>();
        foreach (var row in preparedRows)
        {
            if (!products.TryGetValue(row.ProductId, out var product))
            {
                result.Errors.Add(ToRowError(row, $"Product {row.ProductId} does not exist."));
                continue;
            }

            if (product.IsSerialTracked &&
                (row.Quantity != decimal.Truncate(row.Quantity) ||
                 row.SerialNumbers.Length != (int)row.Quantity))
            {
                result.Errors.Add(ToRowError(row, "Serial count must match stock-in quantity."));
                continue;
            }

            if (!product.IsSerialTracked && row.SerialNumbers.Length > 0)
            {
                result.Errors.Add(ToRowError(row, "Non-serial products cannot receive serial numbers."));
                continue;
            }

            if (row.SerialNumbers.Any(existingSerials.Contains))
            {
                result.Errors.Add(ToRowError(row, "One or more serial numbers already exist."));
                continue;
            }

            var unitId = baseUnitIds.GetValueOrDefault(product.Id, product.DefaultUnitId);
            validated.Add(new ValidatedOpeningBalanceRow(
                row.RowNumber,
                product.Id,
                row.Quantity,
                unitId,
                product.CostPrice ?? product.DefaultPrice,
                row.SerialNumbers));
        }

        return validated;
    }

    private static OpeningBalanceImportRow CloneRow(OpeningBalanceImportRow row) => new()
    {
        RowNumber = row.RowNumber,
        ProductId = row.ProductId,
        Quantity = row.Quantity,
        SerialNumbers = row.SerialNumbers
    };

    private static ImportResult<OpeningBalanceImportRow> Success(
        IReadOnlyCollection<OpeningBalanceImportRow> sourceRows,
        int successCount)
    {
        var result = new ImportResult<OpeningBalanceImportRow>
        {
            SuccessCount = successCount
        };
        result.ImportedItems.AddRange(sourceRows);
        return result;
    }

    private static RowError ToRowError(OpeningBalanceImportRow row, string message) => new()
    {
        RowNumber = row.RowNumber,
        Data = $"ProductId={row.ProductId}; Quantity={row.Quantity}; SerialNumbers={row.SerialNumbers}",
        ErrorMessage = message
    };

    private static RowError ToRowError(PreparedOpeningBalanceRow row, string message) => new()
    {
        RowNumber = row.RowNumber,
        Data = $"ProductId={row.ProductId}; Quantity={row.Quantity}; SerialNumbers={string.Join(',', row.SerialNumbers)}",
        ErrorMessage = message
    };

    private static string CreatePayloadMarker(
        IEnumerable<PreparedOpeningBalanceRow> rows)
    {
        var canonical = rows
            .Select(row => new
            {
                row.ProductId,
                Quantity = row.Quantity.ToString("G29", CultureInfo.InvariantCulture),
                SerialNumbers = row.SerialNumbers
                    .Select(serial => serial.Trim().ToUpperInvariant())
                    .OrderBy(serial => serial, StringComparer.Ordinal)
                    .ToArray()
            })
            .OrderBy(row => row.ProductId)
            .ThenBy(row => row.Quantity, StringComparer.Ordinal)
            .ThenBy(
                row => string.Join('\u001F', row.SerialNumbers),
                StringComparer.Ordinal)
            .ToArray();
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(canonical));
        return $"[import-payload-sha256:{Convert.ToHexString(hash)}]";
    }

    private static void EnsurePayloadMatches(string? notes, string payloadMarker)
    {
        if (notes?.Contains(payloadMarker, StringComparison.Ordinal) != true)
        {
            throw new InvalidOperationException(PayloadMismatchMessage);
        }
    }

    internal static async Task<bool> VerifyCommittedRowsAsync(
        AppDbContext db,
        string documentCode,
        string payloadMarker,
        IReadOnlyCollection<(int ProductId, decimal Quantity, string SerialNumbers)> expectedRows,
        CancellationToken cancellationToken)
    {
        var stockInId = await db.StockIns
            .AsNoTracking()
            .Where(stockIn =>
                stockIn.DocumentCode == documentCode &&
                stockIn.Notes != null &&
                stockIn.Notes.Contains(payloadMarker))
            .Select(stockIn => (int?)stockIn.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (stockInId is null)
        {
            return false;
        }

        var actualRows = await db.StockInLines
            .AsNoTracking()
            .Where(line => line.StockInId == stockInId.Value)
            .Select(line => new { line.ProductId, line.Quantity, line.DraftSerials })
            .ToArrayAsync(cancellationToken);

        var expectedIdentities = expectedRows
            .Select(row => $"{row.ProductId}\u001F{row.Quantity:G29}\u001F{row.SerialNumbers}")
            .OrderBy(identity => identity, StringComparer.Ordinal);
        var actualIdentities = actualRows
            .Select(row =>
                $"{row.ProductId}\u001F{row.Quantity:G29}\u001F{NormalizeSerials(row.DraftSerials)}")
            .OrderBy(identity => identity, StringComparer.Ordinal);
        return expectedIdentities.SequenceEqual(actualIdentities, StringComparer.Ordinal);
    }

    private static string NormalizeSerials(string? serialNumbers) =>
        string.Join(
            ',',
            (serialNumbers ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(serial => serial.ToUpperInvariant())
                .OrderBy(serial => serial, StringComparer.Ordinal));

    private static string GetRootMessage(Exception exception)
    {
        while (exception.InnerException is not null)
        {
            exception = exception.InnerException;
        }

        return exception.Message;
    }

    private sealed record PreparedOpeningBalanceRow(
        int RowNumber,
        int ProductId,
        decimal Quantity,
        string[] SerialNumbers);

    private sealed record ValidatedOpeningBalanceRow(
        int RowNumber,
        int ProductId,
        decimal Quantity,
        int UnitId,
        decimal UnitPrice,
        string[] SerialNumbers);

    private sealed class FixedWarehouseProvider : IDefaultWarehouseProvider
    {
        private readonly int _warehouseId;

        public FixedWarehouseProvider(int warehouseId)
        {
            _warehouseId = warehouseId;
        }

        public int GetDefaultWarehouseId() => _warehouseId;
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTime now)
        {
            Now = now;
        }

        public DateTime Now { get; }
    }
}
