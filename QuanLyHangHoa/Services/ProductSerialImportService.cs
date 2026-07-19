using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Text.Json;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services;

public interface IProductSerialImportService
{
    Task<(int SuccessCount, string Message)> ImportFromExcelAsync(string filePath, int actorId);

    Task<(int SuccessCount, string Message)> ImportFromExcelAsync(
        string filePath,
        int actorId,
        Guid operationId,
        CancellationToken cancellationToken = default);
}

public sealed class ProductSerialImportService : IProductSerialImportService
{
    private const string UnauthorizedMessage = "The current user is not authorized for this action.";
    private const string PayloadMismatchMessage =
        "The operation ID was already used with a different import payload.";
    private readonly DatabaseWriteExecutor _writeExecutor;

    public ProductSerialImportService(Func<AppDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _writeExecutor = new DatabaseWriteExecutor(contextFactory);
    }

    public Task<(int SuccessCount, string Message)> ImportFromExcelAsync(
        string filePath,
        int actorId) =>
        ImportFromExcelAsync(filePath, actorId, Guid.NewGuid());

    public async Task<(int SuccessCount, string Message)> ImportFromExcelAsync(
        string filePath,
        int actorId,
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return (0, $"Lỗi: Không tìm thấy file Excel tại {filePath}");
        }

        // parse file một lần thành scalar; retry chỉ dựng lại entity từ snapshot này, không mở lại workbook.
        List<PreparedSerialRow> rows;
        int parsedSkipCount;
        try
        {
            (rows, parsedSkipCount) = ParseWorkbook(filePath);
        }
        catch (Exception ex)
        {
            return (0, FormatError(ex));
        }

        cancellationToken.ThrowIfCancellationRequested();
        // marker khóa documentCode với đúng nội dung import, ngăn cùng operation id bị dùng lại cho file khác.
        var payloadMarker = CreatePayloadMarker(rows, parsedSkipCount);
        var documentCode = $"IMPORT-SR-{operationId:N}";
        var createdAt = DateTime.UtcNow;
        IReadOnlyCollection<(int ProductId, string SerialNumber)> expectedCommittedRows = [];

        try
        {
            return await _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest(
                    "product-serial.import",
                    operationId,
                    IsolationLevel.Serializable),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(
                        db,
                        actorId,
                        PermissionAction.ManageMasterData);

                    var existingBatch = await db.StockIns
                        .AsNoTracking()
                        .SingleOrDefaultAsync(
                            stockIn => stockIn.DocumentCode == documentCode,
                            token);
                    if (existingBatch is not null)
                    {
                        EnsurePayloadMatches(existingBatch.Notes, payloadMarker);
                        var existingCount = await db.ProductSerials
                            .CountAsync(
                                serial => serial.LastStockInLine.StockInId == existingBatch.Id,
                                token);
                        return (existingCount, BuildMessage(existingCount, parsedSkipCount));
                    }

                    // danh mục và kho được đọc trong callback để mỗi retry dùng trạng thái database mới nhất.
                    var products = (await db.Products
                            .AsNoTracking()
                            .Where(product => product.IsActive)
                            .ToListAsync(token))
                        .ToDictionary(
                            product => product.ProductCode,
                            StringComparer.OrdinalIgnoreCase);

                    var warehouse = await db.Warehouses
                        .FirstOrDefaultAsync(
                            item => item.IsDefault && item.IsActive,
                            token)
                        ?? await db.Warehouses
                            .Where(item => item.IsActive)
                            .OrderBy(item => item.Id)
                            .FirstOrDefaultAsync(token);

                    if (warehouse is null)
                    {
                        warehouse = new Warehouse
                        {
                            WarehouseCode = "WH001",
                            DisplayName = "Kho chính",
                            IsActive = true,
                            IsDefault = true
                        };
                    }

                    var mappedRows = new List<(string SerialNumber, Product Product)>();
                    var skipCount = parsedSkipCount;
                    foreach (var row in rows)
                    {
                        if (products.TryGetValue(row.ProductCode, out var product))
                        {
                            mappedRows.Add((row.SerialNumber, product));
                        }
                        else
                        {
                            skipCount++;
                        }
                    }

                    var requestedSerials = mappedRows
                        .Select(row => row.SerialNumber)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    // set bắt đầu bằng serial đã có trong database; Add bên dưới đồng thời loại trùng giữa các dòng của file.
                    var usedSerials = requestedSerials.Length == 0
                        ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        : new HashSet<string>(
                            await db.ProductSerials
                                .Where(serial => requestedSerials.Contains(serial.SerialNumber))
                                .Select(serial => serial.SerialNumber)
                                .ToListAsync(token),
                            StringComparer.OrdinalIgnoreCase);

                    var stockIn = new StockIn
                    {
                        DocumentCode = documentCode,
                        Status = "Posted",
                        PurposeCode = "OpeningBalance",
                        Notes = payloadMarker,
                        Warehouse = warehouse,
                        ImportDate = createdAt,
                        CreatedAt = createdAt,
                        PostedAt = createdAt,
                        CreatedBy = actorId,
                        PostedBy = actorId
                    };

                    var successCount = 0;
                    foreach (var group in mappedRows
                                 .GroupBy(row => row.Product.Id)
                                 .OrderBy(group => group.Key))
                    {
                        var product = group.First().Product;
                        var serialNumbers = group
                            .Select(row => row.SerialNumber)
                            .Where(serialNumber => usedSerials.Add(serialNumber))
                            .ToArray();
                        if (serialNumbers.Length == 0)
                        {
                            continue;
                        }

                        var line = new StockInLine
                        {
                            ProductId = product.Id,
                            Quantity = serialNumbers.Length,
                            BaseQuantity = serialNumbers.Length,
                            UnitPrice = product.DefaultPrice,
                            UnitId = product.DefaultUnitId
                        };

                        foreach (var serialNumber in serialNumbers)
                        {
                            line.ProductSerials.Add(new ProductSerial
                            {
                                SerialNumber = serialNumber,
                                ProductId = product.Id,
                                CurrentStatus = "InStock",
                                CurrentWarehouse = warehouse
                            });
                            successCount++;
                        }

                        stockIn.Lines.Add(line);
                    }

                    // lưu tập tự nhiên product-serial đã dựng để verifier đối chiếu nếu kết quả commit không rõ ràng.
                    expectedCommittedRows = stockIn.Lines
                        .SelectMany(line => line.ProductSerials.Select(serial =>
                            (line.ProductId, serial.SerialNumber)))
                        .ToArray();

                    if (successCount > 0)
                    {
                        db.StockIns.Add(stockIn);
                        db.AuditLogs.Add(new AuditLog
                        {
                            EntityName = "ProductSerialImport",
                            EntityId = 0,
                            ActionCode = "IMPORT",
                            PerformedBy = actorId,
                            PerformedAt = createdAt,
                            AfterJson = System.Text.Json.JsonSerializer.Serialize(new
                            {
                                OperationId = operationId.ToString("N"),
                                SuccessCount = successCount,
                                SkipCount = skipCount
                            })
                        });
                    }

                    return (successCount, BuildMessage(successCount, skipCount));
                },
                // chỉ coi là đã thành công khi marker và toàn bộ cặp product-serial đều tồn tại đúng trong phiếu.
                (db, token) => VerifyCommittedBatchAsync(
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
            return (0, FormatError(ex));
        }
    }

    private static (List<PreparedSerialRow> Rows, int SkipCount) ParseWorkbook(string filePath)
    {
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);
        using var workbook = new XLWorkbook(stream);

        if (!workbook.TryGetWorksheet("Sản phẩm", out var productSheet))
        {
            throw new InvalidDataException("Không tìm thấy sheet 'Sản phẩm'");
        }

        if (!workbook.TryGetWorksheet("Serial", out var serialSheet))
        {
            throw new InvalidDataException("Không tìm thấy sheet 'Serial'");
        }

        var productHeaders = productSheet.Row(1).CellsUsed().ToDictionary(
            cell => cell.Value.ToString(),
            cell => cell.Address.ColumnNumber,
            StringComparer.OrdinalIgnoreCase);
        var sourceIdToProductCode = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var row in productSheet.RangeUsed()!.RowsUsed().Skip(1))
        {
            var sourceId = productHeaders.TryGetValue("id", out var idColumn)
                ? row.Cell(idColumn).GetString()
                : string.Empty;
            var productCode = productHeaders.TryGetValue("ProductCode", out var codeColumn)
                ? row.Cell(codeColumn).GetString()
                : string.Empty;

            if (!string.IsNullOrWhiteSpace(sourceId) &&
                !string.IsNullOrWhiteSpace(productCode))
            {
                sourceIdToProductCode[sourceId] = productCode.Trim();
            }
        }

        var serialHeaders = serialSheet.Row(1).CellsUsed().ToDictionary(
            cell => cell.Value.ToString(),
            cell => cell.Address.ColumnNumber,
            StringComparer.OrdinalIgnoreCase);
        var preparedRows = new List<PreparedSerialRow>();
        var skipCount = 0;

        foreach (var row in serialSheet.RangeUsed()!.RowsUsed().Skip(1))
        {
            var serialNumber = serialHeaders.TryGetValue("SerialCode", out var serialColumn)
                ? row.Cell(serialColumn).GetString().Trim()
                : string.Empty;
            var sourceProductId = serialHeaders.TryGetValue("ProductId", out var productColumn)
                ? row.Cell(productColumn).GetString()
                : string.Empty;

            if (string.IsNullOrWhiteSpace(serialNumber) ||
                string.IsNullOrWhiteSpace(sourceProductId) ||
                !sourceIdToProductCode.TryGetValue(sourceProductId, out var productCode))
            {
                skipCount++;
                continue;
            }

            preparedRows.Add(new PreparedSerialRow(serialNumber, productCode));
        }

        return (preparedRows, skipCount);
    }

    private static string CreatePayloadMarker(
        IEnumerable<PreparedSerialRow> rows,
        int parsedSkipCount)
    {
        var canonical = new
        {
            ParsedSkipCount = parsedSkipCount,
            Rows = rows
                .Select(row => new
                {
                    ProductCode = row.ProductCode.Trim().ToUpperInvariant(),
                    SerialNumber = row.SerialNumber.Trim().ToUpperInvariant()
                })
                .OrderBy(row => row.ProductCode, StringComparer.Ordinal)
                .ThenBy(row => row.SerialNumber, StringComparer.Ordinal)
                .ToArray()
        };
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

    internal static async Task<bool> VerifyCommittedBatchAsync(
        AppDbContext db,
        string documentCode,
        string payloadMarker,
        IReadOnlyCollection<(int ProductId, string SerialNumber)> expectedRows,
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

        var actualRows = await db.ProductSerials
            .AsNoTracking()
            .Where(serial => serial.LastStockInLine.StockInId == stockInId.Value)
            .Select(serial => new { serial.ProductId, serial.SerialNumber })
            .ToArrayAsync(cancellationToken);

        var expectedIdentities = expectedRows
            .Select(row => $"{row.ProductId}\u001F{row.SerialNumber.Trim().ToUpperInvariant()}")
            .OrderBy(identity => identity, StringComparer.Ordinal);
        var actualIdentities = actualRows
            .Select(row => $"{row.ProductId}\u001F{row.SerialNumber.Trim().ToUpperInvariant()}")
            .OrderBy(identity => identity, StringComparer.Ordinal);
        return expectedIdentities.SequenceEqual(actualIdentities, StringComparer.Ordinal);
    }

    private static string BuildMessage(int successCount, int skipCount)
    {
        var message = $"Đã nạp thành công {successCount} số serial.";
        if (skipCount > 0)
        {
            message += $" (Bỏ qua {skipCount} dòng không hợp lệ hoặc thiếu sản phẩm).";
        }

        return message;
    }

    private static string FormatError(Exception exception)
    {
        var message = exception.Message;
        if (exception.InnerException is not null)
        {
            message += $" Inner: {exception.InnerException.Message}";
        }

        return $"Lỗi Import: {message}";
    }

    private sealed record PreparedSerialRow(string SerialNumber, string ProductCode);
}
