using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    /// <summary>
    /// hợp đồng import workbook serial cũ và trả số dòng thực sự được tạo.
    /// </summary>
    public interface IProductSerialImportService
    {
        Task<(int SuccessCount, string Message)> ImportFromExcelAsync(string filePath, int actorId);
    }

    /// <summary>
    /// chuyển workbook sản phẩm/serial thành một phiếu nhập opening balance trong transaction Serializable.
    /// </summary>
    public class ProductSerialImportService : IProductSerialImportService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public ProductSerialImportService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<(int SuccessCount, string Message)> ImportFromExcelAsync(string filePath, int actorId)
        {
            if (!File.Exists(filePath))
                return (0, $"Lỗi: Không tìm thấy file Excel tại {filePath}");

            List<(string SerialNumber, string ProductCode)> rows;
            int skipCount;
            try
            {
                (rows, skipCount) = ParseWorkbook(filePath);
            }
            catch (Exception ex)
            {
                return (0, FormatError(ex));
            }

            using var context = _contextFactory();
            // Serializable ngăn batch khác chèn cùng serial trong khoảng kiểm tra tồn tại và SaveChanges.
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            AuthorizationService.RequireFreshActor(context, actorId, PermissionAction.ManageMasterData);

            try
            {
                // cache theo ProductCode để map row không phát sinh một query cho mỗi serial.
                var products = (await context.Products.AsNoTracking().ToListAsync())
                    .ToDictionary(product => product.ProductCode, StringComparer.OrdinalIgnoreCase);
                var defaultWarehouse = await context.Warehouses
                    .FirstOrDefaultAsync(warehouse => warehouse.IsDefault && warehouse.IsActive)
                    ?? await context.Warehouses.FirstOrDefaultAsync();

                if (defaultWarehouse == null)
                {
                    defaultWarehouse = new Warehouse
                    {
                        WarehouseCode = "WH001",
                        DisplayName = "Kho chính",
                        IsActive = true,
                        IsDefault = true
                    };
                }

                // row thiếu product được đếm skip; chỉ row map được mới đi vào kiểm tra duplicate toàn batch/database.
                var mappedRows = new List<(string SerialNumber, Product Product)>();
                foreach (var row in rows)
                {
                    if (products.TryGetValue(row.ProductCode, out var product))
                        mappedRows.Add((row.SerialNumber, product));
                    else
                        skipCount++;
                }

                // Distinct giảm tập query; HashSet existingSerials đồng thời loại duplicate trong file khi Add trả false.
                var requestedSerials = mappedRows
                    .Select(row => row.SerialNumber)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var existingSerials = requestedSerials.Count == 0
                    ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(
                        await context.ProductSerials
                            .Where(serial => requestedSerials.Contains(serial.SerialNumber))
                            .Select(serial => serial.SerialNumber)
                            .ToListAsync(),
                        StringComparer.OrdinalIgnoreCase);

                // một StockIn giữ nguồn gốc của toàn batch và nhóm line theo sản phẩm.
                var stockIn = new StockIn
                {
                    DocumentCode = $"IMPORT_SR_{DateTime.Now:yyyyMMdd_HHmm}",
                    Status = "Posted",
                    PurposeCode = "OpeningBalance",
                    Warehouse = defaultWarehouse,
                    CreatedAt = DateTime.Now,
                    PostedAt = DateTime.Now,
                    CreatedBy = actorId,
                    PostedBy = actorId
                };

                int successCount = 0;
                foreach (var group in mappedRows.GroupBy(row => row.Product.Id))
                {
                    var product = group.First().Product;
                    var serialNumbers = group
                        .Select(row => row.SerialNumber)
                        .Where(serialNumber => existingSerials.Add(serialNumber))
                        .ToList();
                    if (serialNumbers.Count == 0)
                        continue;

                    var line = new StockInLine
                    {
                        ProductId = product.Id,
                        Quantity = serialNumbers.Count,
                        BaseQuantity = serialNumbers.Count,
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
                            CurrentWarehouse = defaultWarehouse
                        });
                        successCount++;
                    }

                    stockIn.Lines.Add(line);
                }

                context.StockIns.Add(stockIn);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                string message = $"Đã nạp thành công {successCount} số serial.";
                if (skipCount > 0)
                    message += $" (Bỏ qua {skipCount} dòng không hợp lệ hoặc thiếu sản phẩm).";
                return (successCount, message);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (0, FormatError(ex));
            }
        }

        // sheet Sản phẩm đổi id nguồn sang ProductCode; sheet Serial dùng id đó để nối hai tập dữ liệu.
        private static (List<(string SerialNumber, string ProductCode)> Rows, int SkipCount) ParseWorkbook(
            string filePath)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var workbook = new XLWorkbook(stream);

            if (!workbook.TryGetWorksheet("Sản phẩm", out var productSheet))
                throw new InvalidDataException("Không tìm thấy sheet 'Sản phẩm'");
            if (!workbook.TryGetWorksheet("Serial", out var serialSheet))
                throw new InvalidDataException("Không tìm thấy sheet 'Serial'");

            var productHeaders = productSheet.Row(1).CellsUsed().ToDictionary(
                cell => cell.Value.ToString(),
                cell => cell.Address.ColumnNumber,
                StringComparer.OrdinalIgnoreCase);
            // dictionary là bảng tra cứu in-memory, tránh phụ thuộc thứ tự hai sheet.
            var mongoToCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in productSheet.RangeUsed()!.RowsUsed().Skip(1))
            {
                string mongoId = productHeaders.TryGetValue("id", out int idColumn)
                    ? row.Cell(idColumn).GetString()
                    : string.Empty;
                string productCode = productHeaders.TryGetValue("ProductCode", out int codeColumn)
                    ? row.Cell(codeColumn).GetString()
                    : string.Empty;
                if (!string.IsNullOrWhiteSpace(mongoId) && !string.IsNullOrWhiteSpace(productCode))
                    mongoToCode[mongoId] = productCode;
            }

            var serialHeaders = serialSheet.Row(1).CellsUsed().ToDictionary(
                cell => cell.Value.ToString(),
                cell => cell.Address.ColumnNumber,
                StringComparer.OrdinalIgnoreCase);
            var rows = new List<(string SerialNumber, string ProductCode)>();
            int skipCount = 0;
            foreach (var row in serialSheet.RangeUsed()!.RowsUsed().Skip(1))
            {
                string serialNumber = serialHeaders.TryGetValue("SerialCode", out int serialColumn)
                    ? row.Cell(serialColumn).GetString()
                    : string.Empty;
                string mongoProductId = serialHeaders.TryGetValue("ProductId", out int productColumn)
                    ? row.Cell(productColumn).GetString()
                    : string.Empty;
                if (string.IsNullOrWhiteSpace(serialNumber)
                    || string.IsNullOrWhiteSpace(mongoProductId)
                    || !mongoToCode.TryGetValue(mongoProductId, out var productCode))
                {
                    skipCount++;
                    continue;
                }

                rows.Add((serialNumber, productCode));
            }

            return (rows, skipCount);
        }

        private static string FormatError(Exception exception)
        {
            var message = exception.Message;
            if (exception.InnerException != null)
                message += $" Inner: {exception.InnerException.Message}";
            return $"Lỗi Import: {message}";
        }
    }
}
