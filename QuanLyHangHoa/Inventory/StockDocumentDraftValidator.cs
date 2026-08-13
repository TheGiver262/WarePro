using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Helpers;
using QuanLyHangHoa.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyHangHoa.Inventory;

public static class StockDocumentDraftValidator
{
    public static Task ValidateAsync(
        AppDbContext db,
        IReadOnlyCollection<StockInLine> lines,
        CancellationToken cancellationToken) =>
        ValidateAsync(
            db,
            lines.Select(line => new DraftLine(
                line.ProductId,
                line.UnitId,
                line.Quantity,
                line.DraftSerials,
                line.ProductSerials.Select(serial => serial.SerialNumber),
                value => line.BaseQuantity = value)).ToArray(),
            cancellationToken);

    public static Task ValidateAsync(
        AppDbContext db,
        IReadOnlyCollection<StockOutLine> lines,
        CancellationToken cancellationToken) =>
        ValidateAsync(
            db,
            lines.Select(line => new DraftLine(
                line.ProductId,
                line.UnitId,
                line.Quantity,
                line.DraftSerials,
                line.ProductSerials.Select(serial => serial.SerialNumber),
                value => line.BaseQuantity = value)).ToArray(),
            cancellationToken);

    private static async Task ValidateAsync(
        AppDbContext db,
        IReadOnlyCollection<DraftLine> lines,
        CancellationToken cancellationToken)
    {
        if (lines.Count == 0)
            throw new InventoryDomainException("Chứng từ phải có ít nhất một dòng hàng.");

        var productIds = lines.Select(line => line.ProductId).Distinct().ToArray();
        var products = await db.Products
            .AsNoTracking()
            .Where(product => productIds.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, cancellationToken);
        var units = await db.ProductUnits
            .AsNoTracking()
            .Where(unit => productIds.Contains(unit.ProductId))
            .ToDictionaryAsync(unit => (unit.ProductId, unit.UnitId), cancellationToken);
        var documentSerials = new List<string>();

        foreach (var line in lines)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
                throw new InventoryDomainException($"Sản phẩm mã {line.ProductId} không còn tồn tại.");
            var hasMapping = units.TryGetValue((line.ProductId, line.UnitId), out var productUnit);
            if (!hasMapping && line.UnitId != product.DefaultUnitId)
                throw new InventoryDomainException($"Đơn vị đã chọn không hợp lệ cho sản phẩm {product.DisplayName}.");
            var conversionFactor = hasMapping ? productUnit!.ConversionFactor : 1m;
            if (line.Quantity <= 0m || conversionFactor <= 0m)
                throw new InventoryDomainException($"Số lượng của sản phẩm {product.DisplayName} phải lớn hơn 0.");

            var baseQuantity = line.Quantity * conversionFactor;
            line.SetBaseQuantity(baseQuantity);
            var serials = ParseSerials(line);

            if (!product.IsSerialTracked)
            {
                if (serials.Count > 0)
                    throw new InventoryDomainException($"Sản phẩm {product.DisplayName} không quản lý serial.");
                continue;
            }

            if (baseQuantity != decimal.Truncate(baseQuantity))
                throw new InventoryDomainException($"Số lượng quy đổi của sản phẩm {product.DisplayName} phải là số nguyên.");
            if (serials.Count != (int)baseQuantity)
                throw new InventoryDomainException($"Sản phẩm {product.DisplayName} cần đúng {(int)baseQuantity} số serial.");

            documentSerials.AddRange(serials);
        }

        var duplicates = documentSerials
            .GroupBy(serial => serial, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(serial => serial, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (duplicates.Length > 0)
            throw new InventoryDomainException($"Các số serial sau bị trùng lặp trong phiếu: [{string.Join(", ", duplicates)}]. Vui lòng kiểm tra lại trước khi duyệt.");
    }

    private static List<string> ParseSerials(DraftLine line)
    {
        var source = string.IsNullOrWhiteSpace(line.DraftSerials)
            ? line.LegacySerials
            : line.DraftSerials.Split(',', StringSplitOptions.RemoveEmptyEntries);
        return SerialNumberNormalizer.NormalizeAll(source);
    }

    private sealed record DraftLine(
        int ProductId,
        int UnitId,
        decimal Quantity,
        string? DraftSerials,
        IEnumerable<string?> LegacySerials,
        Action<decimal> SetBaseQuantity);
}
