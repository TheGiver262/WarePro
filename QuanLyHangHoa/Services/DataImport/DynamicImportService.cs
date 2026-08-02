using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Inventory;

namespace QuanLyHangHoa.Services.DataImport
{
    public class ImportFieldDefinition
    {
        public string Key { get; set; } = null!;
        public string DisplayName { get; set; } = null!;
        public bool IsRequired { get; set; }
        public string DataType { get; set; } = "string"; // string, decimal, int, datetime, bool
        public string Description { get; set; } = "";
    }

    public class DynamicImportResult
    {
        public int SuccessCount { get; set; }
        public List<RowError> Errors { get; set; } = new();
    }

    public class DynamicImportService
    {
        private const int MaxPreparedSerialsPerRow = 10_000;
        private const int MaxSerialNumberLength = 150;
        private readonly DatabaseWriteExecutor _writeExecutor;

        public DynamicImportService(Func<AppDbContext> contextFactory)
        {
            ArgumentNullException.ThrowIfNull(contextFactory);
            _writeExecutor = new DatabaseWriteExecutor(contextFactory);
        }

        // danh sách này là hợp đồng giữa màn hình ánh xạ cột và từng luồng import
        public static List<ImportFieldDefinition> GetFieldDefinitions(ImportFileType type)
        {
            return type switch
            {
                ImportFileType.Product => new()
                {
                    new() { Key = "ProductCode", DisplayName = "Mã sản phẩm", IsRequired = true },
                    new() { Key = "DisplayName", DisplayName = "Tên sản phẩm", IsRequired = true },
                    new() { Key = "Description", DisplayName = "Mô tả", IsRequired = false },
                    new() { Key = "CostPrice", DisplayName = "Giá vốn", IsRequired = false, DataType = "decimal" },
                    new() { Key = "DefaultPrice", DisplayName = "Giá bán mặc định", IsRequired = true, DataType = "decimal" },
                    new() { Key = "OriginCountry", DisplayName = "Xuất xứ", IsRequired = false },
                    new() { Key = "WarrantyPeriodMonths", DisplayName = "Thời hạn bảo hành (tháng)", IsRequired = false, DataType = "int" },
                    new() { Key = "IsSerialTracked", DisplayName = "Quản lý bằng số Serial (Có/Không)", IsRequired = false, DataType = "bool" },
                    new() { Key = "CategoryName", DisplayName = "Tên nhóm sản phẩm", IsRequired = true },
                    new() { Key = "BrandName", DisplayName = "Tên thương hiệu", IsRequired = true },
                    new() { Key = "DefaultUnitName", DisplayName = "Đơn vị tính mặc định", IsRequired = true }
                },
                ImportFileType.Category => new()
                {
                    new() { Key = "CategoryCode", DisplayName = "Mã nhóm sản phẩm", IsRequired = true },
                    new() { Key = "DisplayName", DisplayName = "Tên nhóm sản phẩm", IsRequired = true }
                },
                ImportFileType.ProductSerial => new()
                {
                    new() { Key = "SerialNumber", DisplayName = "Số Serial", IsRequired = true },
                    new() { Key = "ProductCode", DisplayName = "Mã sản phẩm", IsRequired = true },
                    new() { Key = "WarehouseName", DisplayName = "Tên kho hiện tại", IsRequired = false },
                    new() { Key = "Note", DisplayName = "Ghi chú", IsRequired = false }
                },
                ImportFileType.StockIn => new()
                {
                    new() { Key = "DocumentCode", DisplayName = "Mã phiếu nhập", IsRequired = false },
                    new() { Key = "ImportDate", DisplayName = "Ngày nhập", IsRequired = true, DataType = "datetime" },
                    new() { Key = "SupplierName", DisplayName = "Tên nhà cung cấp", IsRequired = false },
                    new() { Key = "WarehouseName", DisplayName = "Tên kho nhập", IsRequired = false },
                    new() { Key = "Notes", DisplayName = "Ghi chú phiếu nhập", IsRequired = false },
                    new() { Key = "ProductCode", DisplayName = "Mã sản phẩm", IsRequired = true },
                    new() { Key = "Quantity", DisplayName = "Số lượng nhập", IsRequired = true, DataType = "decimal" },
                    new() { Key = "SerialNumbers", DisplayName = "Danh sách số Serial (phân tách dấu phẩy)", IsRequired = false }
                },
                ImportFileType.StockOut => new()
                {
                    new() { Key = "DocumentCode", DisplayName = "Mã phiếu xuất", IsRequired = false },
                    new() { Key = "ExportDate", DisplayName = "Ngày xuất", IsRequired = true, DataType = "datetime" },
                    new() { Key = "CustomerName", DisplayName = "Tên khách hàng", IsRequired = false },
                    new() { Key = "WarehouseName", DisplayName = "Tên kho xuất", IsRequired = false },
                    new() { Key = "Notes", DisplayName = "Ghi chú phiếu xuất", IsRequired = false },
                    new() { Key = "ProductCode", DisplayName = "Mã sản phẩm", IsRequired = true },
                    new() { Key = "Quantity", DisplayName = "Số lượng xuất", IsRequired = true, DataType = "decimal" },
                    new() { Key = "SerialNumbers", DisplayName = "Danh sách số Serial (phân tách dấu phẩy)", IsRequired = false }
                },
                ImportFileType.PurchaseInvoice => new()
                {
                    new() { Key = "InvoiceCode", DisplayName = "Mã hóa đơn mua", IsRequired = true },
                    new() { Key = "InvoiceDate", DisplayName = "Ngày hóa đơn", IsRequired = true, DataType = "datetime" },
                    new() { Key = "SupplierName", DisplayName = "Tên nhà cung cấp", IsRequired = true },
                    new() { Key = "TotalAmount", DisplayName = "Tổng tiền hóa đơn", IsRequired = true, DataType = "decimal" },
                    new() { Key = "DiscountAmount", DisplayName = "Tiền giảm giá", IsRequired = false, DataType = "decimal" },
                    new() { Key = "TaxAmount", DisplayName = "Tiền thuế VAT", IsRequired = false, DataType = "decimal" },
                    new() { Key = "PaymentStatus", DisplayName = "Trạng thái thanh toán (Paid/PartiallyPaid/Unpaid/Overdue)", IsRequired = false },
                    new() { Key = "PaidAmount", DisplayName = "Số tiền đã thanh toán", IsRequired = false, DataType = "decimal" },
                    new() { Key = "Notes", DisplayName = "Ghi chú hóa đơn", IsRequired = false },
                    new() { Key = "ProductCode", DisplayName = "Mã sản phẩm", IsRequired = true },
                    new() { Key = "Quantity", DisplayName = "Số lượng", IsRequired = true, DataType = "decimal" },
                    new() { Key = "UnitPrice", DisplayName = "Đơn giá mua", IsRequired = true, DataType = "decimal" },
                    new() { Key = "TaxRate", DisplayName = "Thuế suất dòng hàng (ví dụ: 0.1)", IsRequired = false, DataType = "decimal" }
                },
                ImportFileType.SalesInvoice => new()
                {
                    new() { Key = "InvoiceCode", DisplayName = "Mã hóa đơn bán", IsRequired = true },
                    new() { Key = "InvoiceDate", DisplayName = "Ngày hóa đơn", IsRequired = true, DataType = "datetime" },
                    new() { Key = "CustomerName", DisplayName = "Tên khách hàng", IsRequired = true },
                    new() { Key = "TotalAmount", DisplayName = "Tổng tiền hóa đơn", IsRequired = true, DataType = "decimal" },
                    new() { Key = "DiscountAmount", DisplayName = "Tiền giảm giá", IsRequired = false, DataType = "decimal" },
                    new() { Key = "TaxAmount", DisplayName = "Tiền thuế VAT", IsRequired = false, DataType = "decimal" },
                    new() { Key = "PaymentStatus", DisplayName = "Trạng thái thanh toán (Paid/PartiallyPaid/Unpaid/Overdue)", IsRequired = false },
                    new() { Key = "PaidAmount", DisplayName = "Số tiền đã thanh toán", IsRequired = false, DataType = "decimal" },
                    new() { Key = "Notes", DisplayName = "Ghi chú hóa đơn", IsRequired = false },
                    new() { Key = "ProductCode", DisplayName = "Mã sản phẩm", IsRequired = true },
                    new() { Key = "Quantity", DisplayName = "Số lượng", IsRequired = true, DataType = "decimal" },
                    new() { Key = "UnitPrice", DisplayName = "Đơn giá bán", IsRequired = true, DataType = "decimal" },
                    new() { Key = "TaxRate", DisplayName = "Thuế suất dòng hàng (ví dụ: 0.1)", IsRequired = false, DataType = "decimal" }
                },
                _ => new()
            };
        }

        // đưa excel và csv về cùng một cấu trúc để phần xử lý phía sau không phụ thuộc định dạng file
        public static (List<string> headers, List<Dictionary<string, string>> rows) ReadFile(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (extension == ".xlsx" || extension == ".xls")
            {
                return ReadExcel(filePath);
            }
            else if (extension == ".csv")
            {
                return ReadCsv(filePath);
            }
            else
            {
                throw new NotSupportedException("Định dạng file không được hỗ trợ. Vui lòng chọn file Excel hoặc CSV.");
            }
        }

        private static (List<string> headers, List<Dictionary<string, string>> rows) ReadExcel(string filePath)
        {
            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1);
            var usedRange = worksheet.RangeUsed();
            if (usedRange == null)
            {
                return (new(), new());
            }

            var firstRow = usedRange.Row(1);
            var headers = firstRow.CellsUsed().Select(c => c.Value.ToString().Trim()).ToList();
            var headerMap = firstRow.CellsUsed().ToDictionary(c => c.Address.ColumnNumber, c => c.Value.ToString().Trim());

            var rows = new List<Dictionary<string, string>>();
            foreach (var row in usedRange.RowsUsed().Skip(1))
            {
                var rowData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var h in headers) rowData[h] = "";

                foreach (var cell in row.CellsUsed())
                {
                    if (headerMap.TryGetValue(cell.Address.ColumnNumber, out string? header))
                    {
                        rowData[header] = cell.Value.ToString().Trim();
                    }
                }
                rows.Add(rowData);
            }

            return (headers, rows);
        }

        private static (List<string> headers, List<Dictionary<string, string>> rows) ReadCsv(string filePath)
        {
            using var reader = new StreamReader(filePath);
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null
            };
            using var csv = new CsvReader(reader, config);
            csv.Read();
            csv.ReadHeader();
            var headers = csv.HeaderRecord?.Select(h => h.Trim()).ToList() ?? new List<string>();

            var rows = new List<Dictionary<string, string>>();
            while (csv.Read())
            {
                var rowData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var header in headers)
                {
                    rowData[header] = csv.GetField(header)?.Trim() ?? "";
                }
                rows.Add(rowData);
            }

            return (headers, rows);
        }

        // executor giữ transaction toàn operation; chứng từ kho dùng savepoint để một nhóm lỗi không làm mất nhóm hợp lệ
        internal DynamicImportResult ExecuteImport(
            List<Dictionary<string, string>> rawRows,
            ImportFileType type,
            Dictionary<string, string> mappings,
            int userId,
            bool autoCreateReferences = true) =>
            ExecuteImportAsync(
                    rawRows,
                    type,
                    mappings,
                    userId,
                    autoCreateReferences,
                    Guid.NewGuid())
                .GetAwaiter()
                .GetResult();

        public async Task<DynamicImportResult> ExecuteImportAsync(
            List<Dictionary<string, string>> rawRows,
            ImportFileType type,
            Dictionary<string, string> mappings,
            int userId,
            bool autoCreateReferences,
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(rawRows);
            ArgumentNullException.ThrowIfNull(mappings);
            // batch là snapshot scalar đã chuẩn hóa; callback retry không đọc lại rawRows hoặc mappings có thể bị bên gọi sửa.
            var batch = PrepareImportBatch(
                rawRows,
                type,
                mappings,
                userId,
                autoCreateReferences,
                operationId);
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await _writeExecutor.ExecuteAsync(
                    new DatabaseWriteRequest(
                        $"dynamic-import.{batch.Type.ToString().ToLowerInvariant()}",
                        operationId,
                        IsolationLevel.Serializable),
                    async (db, token) =>
                    {
                        RequireImportPermission(db, userId, batch.Type);
                        token.ThrowIfCancellationRequested();
                        // mỗi attempt dựng result và entity mới; không mang tracked state từ transaction đã rollback sang lần retry.
                        var result = new DynamicImportResult();
                        var rowIdx = 1;

                        switch (batch.Type)
                        {
                            case ImportFileType.Category:
                                ImportCategories(batch.Rows, db, result, ref rowIdx);
                                break;
                            case ImportFileType.Product:
                                rowIdx = await ImportProductsAsync(
                                    batch.Rows, db, result, batch.AutoCreateReferences, rowIdx, token);
                                break;
                            case ImportFileType.ProductSerial:
                                rowIdx = await ImportProductSerialsAsync(
                                    batch.Rows, db, result, userId, batch.AutoCreateReferences,
                                    operationId, rowIdx, token);
                                break;
                            case ImportFileType.StockIn:
                                rowIdx = await ImportStockInDocumentsAsync(
                                    batch.Rows, db, result, userId, batch.AutoCreateReferences,
                                    operationId, rowIdx, token);
                                break;
                            case ImportFileType.StockOut:
                                rowIdx = await ImportStockOutDocumentsAsync(
                                    batch.Rows, db, result, userId, batch.AutoCreateReferences,
                                    operationId, rowIdx, token);
                                break;
                            case ImportFileType.PurchaseInvoice:
                                rowIdx = await ImportPurchaseInvoicesAsync(
                                    batch.Rows, db, result, userId, batch.AutoCreateReferences,
                                    operationId, rowIdx, token);
                                break;
                            case ImportFileType.SalesInvoice:
                                rowIdx = await ImportSalesInvoicesAsync(
                                    batch.Rows, db, result, userId, batch.AutoCreateReferences,
                                    operationId, rowIdx, token);
                                break;
                        }

                        token.ThrowIfCancellationRequested();
                        var operationKey = operationId.ToString("N");
                        if (result.SuccessCount > 0 && !await db.AuditLogs.AnyAsync(log =>
                                log.EntityName == "DynamicImport" &&
                                log.AfterJson != null &&
                                log.AfterJson.Contains(operationKey), token))
                        {
                            db.AuditLogs.Add(new AuditLog
                            {
                                EntityName = "DynamicImport",
                                EntityId = 0,
                                ActionCode = "IMPORT",
                                PerformedBy = userId,
                                PerformedAt = DateTime.UtcNow,
                                AfterJson = JsonSerializer.Serialize(new
                                {
                                    OperationId = operationKey,
                                    ImportType = batch.Type.ToString(),
                                    result.SuccessCount,
                                    ErrorCount = result.Errors.Count
                                })
                            });
                        }

                        return result;
                    },
                    (db, token) => VerifyImportAppliedAsync(db, batch, operationId, token),
                    entityKey: operationId.ToString("N"),
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (InvalidOperationException ex) when (
                string.Equals(
                    ex.Message,
                    "The current user is not authorized for this action.",
                    StringComparison.Ordinal))
            {
                throw;
            }
        }

        private static PreparedImportBatch PrepareImportBatch(
            IReadOnlyCollection<Dictionary<string, string>> rawRows,
            ImportFileType type,
            IReadOnlyDictionary<string, string> mappings,
            int userId,
            bool autoCreateReferences,
            Guid operationId)
        {
            if (type == ImportFileType.Unknown || !Enum.IsDefined(type))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(type), type, "Import type must be a defined non-Unknown value.");
            }

            if (userId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(userId), userId, "User ID must be positive.");
            }

            if (operationId == Guid.Empty)
            {
                throw new ArgumentException("Operation ID cannot be empty.", nameof(operationId));
            }

            var sourceRows = rawRows
                .Select(row => row is null
                    ? throw new ArgumentException(
                        "Import rows cannot contain null values.", nameof(rawRows))
                    : new Dictionary<string, string>(row, StringComparer.OrdinalIgnoreCase))
                .ToArray();
            var sourceMappings = new Dictionary<string, string>(
                mappings, StringComparer.OrdinalIgnoreCase);
            var rows = sourceRows
                .Select(row => PrepareImportRow(row, sourceMappings, type))
                .ToArray();

            ValidatePreparedGroups(rows, type);
            AttachPreparedPayloadMarkers(
                rows, sourceRows, sourceMappings, type, operationId);
            return new PreparedImportBatch(type, rows, autoCreateReferences);
        }

        private static PreparedImportRow PrepareImportRow(
            Dictionary<string, string> row,
            Dictionary<string, string> mappings,
            ImportFileType type) =>
            type switch
            {
                ImportFileType.Category => new PreparedImportRow
                {
                    CategoryCode = GetMappedString(row, mappings, "CategoryCode", required: true),
                    DisplayName = GetMappedString(row, mappings, "DisplayName", required: true)
                },
                ImportFileType.Product => PrepareProductRow(row, mappings),
                ImportFileType.ProductSerial => new PreparedImportRow
                {
                    SerialNumber = GetMappedString(row, mappings, "SerialNumber", required: true),
                    ProductCode = GetMappedString(row, mappings, "ProductCode", required: true),
                    WarehouseName = GetMappedString(row, mappings, "WarehouseName", required: false),
                    Note = GetMappedString(row, mappings, "Note", required: false)
                },
                ImportFileType.StockIn => PrepareStockRow(row, mappings, isStockIn: true),
                ImportFileType.StockOut => PrepareStockRow(row, mappings, isStockIn: false),
                ImportFileType.PurchaseInvoice => PrepareInvoiceRow(row, mappings, isPurchase: true),
                ImportFileType.SalesInvoice => PrepareInvoiceRow(row, mappings, isPurchase: false),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };

        private static PreparedImportRow PrepareProductRow(
            Dictionary<string, string> row,
            Dictionary<string, string> mappings)
        {
            var costPrice = GetMappedDecimalNull(row, mappings, "CostPrice");
            var defaultPrice = GetMappedDecimal(row, mappings, "DefaultPrice", required: true);
            var warranty = GetMappedInt(row, mappings, "WarrantyPeriodMonths") ?? 0;
            if (costPrice < 0 || defaultPrice < 0 || warranty < 0)
            {
                throw new ArgumentException("Product prices and warranty period cannot be negative.");
            }

            return new PreparedImportRow
            {
                ProductCode = GetMappedString(row, mappings, "ProductCode", required: true),
                DisplayName = GetMappedString(row, mappings, "DisplayName", required: true),
                Description = GetMappedString(row, mappings, "Description", required: false),
                CostPrice = costPrice,
                DefaultPrice = defaultPrice,
                OriginCountry = GetMappedString(row, mappings, "OriginCountry", required: false),
                WarrantyPeriodMonths = warranty,
                IsSerialTracked = GetMappedBool(row, mappings, "IsSerialTracked") ?? false,
                CategoryName = GetMappedString(row, mappings, "CategoryName", required: true),
                BrandName = GetMappedString(row, mappings, "BrandName", required: true),
                DefaultUnitName = GetMappedString(row, mappings, "DefaultUnitName", required: true)
            };
        }

        private static PreparedImportRow PrepareStockRow(
            Dictionary<string, string> row,
            Dictionary<string, string> mappings,
            bool isStockIn)
        {
            var quantity = GetMappedDecimal(row, mappings, "Quantity", required: true);
            if (quantity <= 0)
            {
                throw new ArgumentException("Stock quantity must be greater than zero.");
            }

            var serialInput = GetMappedString(row, mappings, "SerialNumbers", required: false);
            return new PreparedImportRow
            {
                DocumentCode = GetMappedString(row, mappings, "DocumentCode", required: false),
                ImportDate = isStockIn
                    ? GetMappedDateTime(row, mappings, "ImportDate", required: true)
                    : null,
                ExportDate = isStockIn
                    ? null
                    : GetMappedDateTime(row, mappings, "ExportDate", required: true),
                SupplierName = isStockIn
                    ? GetMappedString(row, mappings, "SupplierName", required: false)
                    : null,
                CustomerName = isStockIn
                    ? null
                    : GetMappedString(row, mappings, "CustomerName", required: false),
                WarehouseName = GetMappedString(row, mappings, "WarehouseName", required: false),
                Notes = GetMappedString(row, mappings, "Notes", required: false),
                ProductCode = GetMappedString(row, mappings, "ProductCode", required: true),
                Quantity = quantity,
                Serials = PrepareSerialNumbers(serialInput)
            };
        }

        private static IReadOnlyList<string> PrepareSerialNumbers(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return Array.Empty<string>();
            }

            var serials = new List<string>();
            foreach (var part in input.Split(
                         new[] { ',', '\n', '\r' },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                var value = part.Trim();
                var range = Regex.Match(value, @"^(.+?)(\d+)-[^\d]*(\d+)$");
                if (!range.Success)
                {
                    serials.Add(value);
                    continue;
                }

                if (!long.TryParse(range.Groups[2].Value, out var start) ||
                    !long.TryParse(range.Groups[3].Value, out var end) ||
                    end < start)
                {
                    throw new ArgumentException($"Invalid serial range '{value}'.");
                }

                if (end == long.MaxValue)
                {
                    throw new ArgumentException(
                        $"Serial range '{value}' cannot end at the maximum 64-bit value.");
                }

                var count = end - start + 1;
                if (count > MaxPreparedSerialsPerRow - serials.Count)
                {
                    throw new ArgumentException(
                        $"Serial range '{value}' exceeds {MaxPreparedSerialsPerRow} values per row.");
                }

                var prefix = range.Groups[1].Value;
                var padLength = range.Groups[2].Value.Length;
                for (long offset = 0; offset < count; offset++)
                {
                    serials.Add(prefix + (start + offset).ToString().PadLeft(padLength, '0'));
                }
            }

            if (serials.Count > MaxPreparedSerialsPerRow)
            {
                throw new ArgumentException(
                    $"A stock row cannot contain more than {MaxPreparedSerialsPerRow} serials.");
            }

            if (serials.Any(serial => serial.Length > MaxSerialNumberLength))
            {
                throw new ArgumentException(
                    $"Serial numbers cannot exceed {MaxSerialNumberLength} characters.");
            }

            return Array.AsReadOnly(serials.ToArray());
        }
        private static PreparedImportRow PrepareInvoiceRow(
            Dictionary<string, string> row,
            Dictionary<string, string> mappings,
            bool isPurchase)
        {
            var totalAmount = GetMappedDecimal(row, mappings, "TotalAmount", required: true);
            var discountAmount = GetMappedDecimalNull(row, mappings, "DiscountAmount") ?? 0;
            var paidAmountInput = GetMappedDecimalNull(row, mappings, "PaidAmount");
            var taxAmount = GetMappedDecimalNull(row, mappings, "TaxAmount") ?? 0;
            var quantity = GetMappedDecimal(row, mappings, "Quantity", required: true);
            var unitPrice = GetMappedDecimal(row, mappings, "UnitPrice", required: true);
            var taxRate = GetMappedDecimalNull(row, mappings, "TaxRate") ?? 0;
            if (totalAmount < 0 || discountAmount < 0 || taxAmount < 0 ||
                quantity <= 0 || unitPrice < 0 || taxRate < 0 || taxRate > 1)
            {
                throw new ArgumentException(
                    "Invoice amounts must be non-negative, quantity positive, and tax rate between 0 and 1.");
            }

            var paymentStatus = PaymentStatus.Normalize(
                GetMappedString(row, mappings, "PaymentStatus", required: false) ??
                PaymentStatus.Unpaid);
            var paidAmount = ResolveImportedPaidAmount(totalAmount, paymentStatus, paidAmountInput);
            return new PreparedImportRow
            {
                InvoiceCode = GetMappedString(row, mappings, "InvoiceCode", required: true),
                InvoiceDate = GetMappedDateTime(row, mappings, "InvoiceDate", required: true),
                SupplierName = isPurchase
                    ? GetMappedString(row, mappings, "SupplierName", required: true)
                    : null,
                CustomerName = isPurchase
                    ? null
                    : GetMappedString(row, mappings, "CustomerName", required: true),
                TotalAmount = totalAmount,
                DiscountAmount = discountAmount,
                TaxAmount = taxAmount,
                PaidAmount = paidAmount,
                PaymentStatus = paymentStatus,
                Notes = GetMappedString(row, mappings, "Notes", required: false),
                ProductCode = GetMappedString(row, mappings, "ProductCode", required: true),
                Quantity = quantity,
                UnitPrice = unitPrice,
                TaxRate = taxRate
            };
        }

        private static decimal ResolveImportedPaidAmount(
            decimal totalAmount,
            string paymentStatus,
            decimal? paidAmountInput)
        {
            var paidAmount = paidAmountInput ??
                (paymentStatus == PaymentStatus.Paid ? totalAmount : 0m);
            if (paidAmount < 0 || paidAmount > totalAmount)
                throw new ArgumentException("Paid amount must be between zero and invoice total.");

            var isConsistent = paymentStatus switch
            {
                PaymentStatus.Paid => totalAmount > 0 && paidAmount == totalAmount,
                PaymentStatus.PartiallyPaid => paidAmount > 0 && paidAmount < totalAmount,
                PaymentStatus.Unpaid => paidAmount == 0,
                PaymentStatus.Overdue => paidAmount < totalAmount,
                _ => false
            };
            if (!isConsistent)
                throw new ArgumentException("Paid amount does not match payment status.");

            return paidAmount;
        }

        private static void ValidatePreparedGroups(
            IReadOnlyCollection<PreparedImportRow> rows,
            ImportFileType type)
        {
            if (type is ImportFileType.StockIn or ImportFileType.StockOut)
            {
                foreach (var group in GroupStockRows(rows))
                {
                    var first = group.First();
                    var consistent = group.All(row =>
                        row.ImportDate == first.ImportDate &&
                        row.ExportDate == first.ExportDate &&
                        row.SupplierName == first.SupplierName &&
                        row.CustomerName == first.CustomerName &&
                        row.WarehouseName == first.WarehouseName &&
                        row.Notes == first.Notes);
                    if (!consistent)
                    {
                        throw new ArgumentException(
                            $"Document group '{group.Key}' has inconsistent header values.");
                    }

                    var serials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (group.SelectMany(row => row.Serials).Any(serial => !serials.Add(serial)))
                    {
                        throw new ArgumentException(
                            $"Duplicate serials are not allowed in document group '{group.Key}'.");
                    }
                }
            }

            if (type is ImportFileType.PurchaseInvoice or ImportFileType.SalesInvoice)
            {
                foreach (var group in rows.GroupBy(row => row.InvoiceCode!))
                {
                    var first = group.First();
                    var consistent = group.All(row =>
                        row.InvoiceDate == first.InvoiceDate &&
                        row.SupplierName == first.SupplierName &&
                        row.CustomerName == first.CustomerName &&
                        row.TotalAmount == first.TotalAmount &&
                        row.DiscountAmount == first.DiscountAmount &&
                        row.TaxAmount == first.TaxAmount &&
                        row.PaidAmount == first.PaidAmount &&
                        row.PaymentStatus == first.PaymentStatus &&
                        row.Notes == first.Notes);
                    if (!consistent)
                    {
                        throw new ArgumentException(
                            $"Invoice group '{group.Key}' has inconsistent header values.");
                    }
                }
            }
        }

        private static void AttachPreparedPayloadMarkers(
            PreparedImportRow[] rows,
            IReadOnlyList<Dictionary<string, string>> sourceRows,
            Dictionary<string, string> mappings,
            ImportFileType type,
            Guid operationId)
        {
            if (type is not (ImportFileType.StockIn or ImportFileType.StockOut or
                ImportFileType.PurchaseInvoice or ImportFileType.SalesInvoice or
                ImportFileType.ProductSerial))
            {
                return;
            }

            var indexedGroups = rows
                .Select((row, index) => new { Row = row, Index = index })
                .GroupBy(item => type switch
                {
                    ImportFileType.ProductSerial =>
                        item.Row.WarehouseName?.Trim().ToUpperInvariant() ?? string.Empty,
                    ImportFileType.StockIn or ImportFileType.StockOut => GetStockGroupKey(item.Row),
                    _ => item.Row.InvoiceCode!
                });
            foreach (var group in indexedGroups)
            {
                // mỗi chứng từ có marker riêng từ dữ liệu nguồn chuẩn hóa để phát hiện replay cùng mã nhưng khác nội dung.
                var marker = CreatePayloadMarker(
                    operationId,
                    type,
                    group.Select(item => sourceRows[item.Index]),
                    mappings);
                var persistedNotes = AttachPayloadMarker(
                    type == ImportFileType.ProductSerial ? group.First().Row.Note : group.First().Row.Notes,
                    marker);
                foreach (var item in group)
                {
                    rows[item.Index] = item.Row with
                    {
                        PayloadMarker = marker,
                        PersistedNotes = persistedNotes
                    };
                }
            }
        }
        private static void RequireImportPermission(
            AppDbContext db,
            int userId,
            ImportFileType type)
        {
            var action = type switch
            {
                ImportFileType.StockIn or ImportFileType.ProductSerial =>
                    PermissionAction.PostStockIn,
                ImportFileType.StockOut => PermissionAction.PostStockOut,
                ImportFileType.PurchaseInvoice => PermissionAction.CreatePurchaseInvoice,
                ImportFileType.SalesInvoice => PermissionAction.CreateSalesInvoice,
                _ => PermissionAction.ManageMasterData
            };
            AuthorizationService.RequireFreshActor(db, userId, action);
        }

        // xác minh theo trạng thái nghiệp vụ đã ghi; nhóm chứng từ còn đối chiếu marker và số dòng, nhóm tự sinh dựng mã từ operation id.
        private static async Task<bool> VerifyImportAppliedAsync(
            AppDbContext db,
            PreparedImportBatch batch,
            Guid operationId,
            CancellationToken cancellationToken)
        {
            switch (batch.Type)
            {
                case ImportFileType.Category:
                    var categoryCodes = batch.Rows.Select(row => row.CategoryCode!).Distinct().ToList();
                    var persistedCategories = (await db.Categories
                            .Where(category => categoryCodes.Contains(category.CategoryCode))
                            .Select(category => new { category.CategoryCode, category.DisplayName })
                            .ToListAsync(cancellationToken))
                        .Select(category => (category.CategoryCode, category.DisplayName))
                        .ToHashSet();
                    return batch.Rows.All(row =>
                        persistedCategories.Contains((row.CategoryCode!, row.DisplayName!)));
                case ImportFileType.Product:
                    var productCodes = batch.Rows.Select(row => row.ProductCode!).Distinct().ToList();
                    var persistedProducts = (await db.Products
                            .Where(product => productCodes.Contains(product.ProductCode))
                            .Select(product => new { product.ProductCode, product.DisplayName })
                            .ToListAsync(cancellationToken))
                        .Select(product => (product.ProductCode, product.DisplayName))
                        .ToHashSet();
                    return batch.Rows.All(row =>
                        persistedProducts.Contains((row.ProductCode!, row.DisplayName!)));
                case ImportFileType.ProductSerial:
                    var serialDocumentPrefix = $"SI-{operationId:N}-";
                    var serialNumbers = batch.Rows.Select(row => row.SerialNumber!).Distinct().ToList();
                    var postedSerials = await db.ProductSerials
                            .Where(serial =>
                                serialNumbers.Contains(serial.SerialNumber) &&
                                serial.LastStockInLine.StockIn.DocumentCode.StartsWith(serialDocumentPrefix))
                            .Select(serial => new
                            {
                                serial.SerialNumber,
                                serial.ProductId,
                                serial.CurrentWarehouseId,
                                StockInId = serial.LastStockInLine.StockInId
                            })
                            .ToListAsync(cancellationToken);
                    var postedBySerial = postedSerials
                        .GroupBy(serial => serial.SerialNumber, StringComparer.Ordinal)
                        .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
                    var stockInIds = postedSerials.Select(serial => serial.StockInId).Distinct().ToList();
                    var ledgerKeys = (await db.StockLedgers
                            .Where(ledger =>
                                ledger.SourceDocumentType == "StockIn" &&
                                stockInIds.Contains(ledger.SourceDocumentId))
                            .Select(ledger => new
                            {
                                ledger.SourceDocumentId,
                                ledger.ProductId,
                                WarehouseId = (int?)ledger.WarehouseId
                            })
                            .ToListAsync(cancellationToken))
                        .Select(ledger => (ledger.SourceDocumentId, ledger.ProductId, ledger.WarehouseId))
                        .ToHashSet();
                    return batch.Rows.All(row =>
                        postedBySerial.TryGetValue(row.SerialNumber!, out var serial) &&
                        ledgerKeys.Contains((serial.StockInId, serial.ProductId, serial.CurrentWarehouseId)));
                case ImportFileType.StockIn:
                    foreach (var group in GroupStockRows(batch.Rows))
                    {
                        var documentCode = GetStockDocumentCode(group.Key, operationId, isStockIn: true);
                        var document = await db.StockIns.AsNoTracking().SingleOrDefaultAsync(
                            item => item.DocumentCode == documentCode, cancellationToken);
                        if (document is null ||
                            !PayloadMatches(document.Notes, group.First().PayloadMarker!) ||
                            await db.StockInLines.CountAsync(
                                line => line.StockInId == document.Id, cancellationToken) != group.Count())
                        {
                            return false;
                        }
                    }
                    return true;
                case ImportFileType.StockOut:
                    foreach (var group in GroupStockRows(batch.Rows))
                    {
                        var documentCode = GetStockDocumentCode(group.Key, operationId, isStockIn: false);
                        var document = await db.StockOuts.AsNoTracking().SingleOrDefaultAsync(
                            item => item.DocumentCode == documentCode, cancellationToken);
                        if (document is null ||
                            !PayloadMatches(document.Notes, group.First().PayloadMarker!) ||
                            await db.StockOutLines.CountAsync(
                                line => line.StockOutId == document.Id, cancellationToken) != group.Count())
                        {
                            return false;
                        }
                    }
                    return true;
                case ImportFileType.PurchaseInvoice:
                    foreach (var group in batch.Rows.GroupBy(row => row.InvoiceCode!))
                    {
                        var invoice = await db.PurchaseInvoices.AsNoTracking().SingleOrDefaultAsync(
                            item => item.InvoiceCode == group.Key, cancellationToken);
                        if (invoice is null ||
                            !PayloadMatches(invoice.Notes, group.First().PayloadMarker!) ||
                            await db.PurchaseInvoiceLines.CountAsync(
                                line => line.PurchaseInvoiceId == invoice.Id, cancellationToken) != group.Count())
                        {
                            return false;
                        }
                    }
                    return true;
                case ImportFileType.SalesInvoice:
                    foreach (var group in batch.Rows.GroupBy(row => row.InvoiceCode!))
                    {
                        var invoice = await db.SalesInvoices.AsNoTracking().SingleOrDefaultAsync(
                            item => item.InvoiceCode == group.Key, cancellationToken);
                        if (invoice is null ||
                            !PayloadMatches(invoice.Notes, group.First().PayloadMarker!) ||
                            await db.SalesInvoiceLines.CountAsync(
                                line => line.SalesInvoiceId == invoice.Id, cancellationToken) != group.Count())
                        {
                            return false;
                        }
                    }
                    return true;
                default:
                    return false;
            }
        }

        private static IEnumerable<IGrouping<string, PreparedImportRow>> GroupStockRows(
            IEnumerable<PreparedImportRow> rows) =>
            rows.GroupBy(GetStockGroupKey);

        private static string GetStockGroupKey(PreparedImportRow row) =>
            string.IsNullOrWhiteSpace(row.DocumentCode) ? "AUTO-GEN" : row.DocumentCode;
        private static string GetStockDocumentCode(
            string sourceCode,
            Guid operationId,
            bool isStockIn) =>
            sourceCode == "AUTO-GEN"
                ? $"{(isStockIn ? "SI" : "SO")}-{operationId:N}"
                : sourceCode;

        private static string CreatePayloadMarker(
            Guid operationId,
            ImportFileType type,
            IEnumerable<Dictionary<string, string>> rows,
            Dictionary<string, string> mappings)
        {
            var fieldKeys = GetFieldDefinitions(type)
                .Select(field => field.Key)
                .ToArray();
            var canonicalRows = rows
                .Select(row => fieldKeys
                    .Select(field => GetMappedString(row, mappings, field, required: false))
                    .ToArray())
                .OrderBy(row => string.Join('\u001F', row), StringComparer.Ordinal)
                .ToArray();
            var canonical = new
            {
                OperationId = operationId.ToString("N"),
                ImportType = type.ToString(),
                Fields = fieldKeys,
                Rows = canonicalRows
            };
            var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(canonical));
            return $"[import-payload-sha256:{Convert.ToHexString(hash)}]";
        }

        private static string AttachPayloadMarker(string? notes, string payloadMarker)
        {
            var persistedNotes = string.IsNullOrWhiteSpace(notes)
                ? payloadMarker
                : $"{notes.Trim()} {payloadMarker}";
            if (persistedNotes.Length > 500)
            {
                throw new ArgumentException("Ghi chú quá dài sau khi thêm mã xác thực payload.");
            }

            return persistedNotes;
        }

        private static bool PayloadMatches(string? notes, string payloadMarker) =>
            notes?.Contains(payloadMarker, StringComparison.Ordinal) == true;

        private static void EnsurePayloadMatches(string? notes, string payloadMarker)
        {
            if (!PayloadMatches(notes, payloadMarker))
            {
                throw new InvalidOperationException(
                    "Import payload does not match the existing document.");
            }
        }

        private sealed record PreparedImportBatch(
            ImportFileType Type,
            IReadOnlyList<PreparedImportRow> Rows,
            bool AutoCreateReferences);

        private sealed record PreparedImportRow
        {
            public string? CategoryCode { get; init; }
            public string? DisplayName { get; init; }
            public string? ProductCode { get; init; }
            public string? Description { get; init; }
            public decimal? CostPrice { get; init; }
            public decimal DefaultPrice { get; init; }
            public string? OriginCountry { get; init; }
            public int WarrantyPeriodMonths { get; init; }
            public bool IsSerialTracked { get; init; }
            public string? CategoryName { get; init; }
            public string? BrandName { get; init; }
            public string? DefaultUnitName { get; init; }
            public string? SerialNumber { get; init; }
            public string? WarehouseName { get; init; }
            public string? Note { get; init; }
            public string? DocumentCode { get; init; }
            public DateTime? ImportDate { get; init; }
            public DateTime? ExportDate { get; init; }
            public string? SupplierName { get; init; }
            public string? CustomerName { get; init; }
            public string? Notes { get; init; }
            public decimal Quantity { get; init; }
            public IReadOnlyList<string> Serials { get; init; } = Array.Empty<string>();
            public string? InvoiceCode { get; init; }
            public DateTime? InvoiceDate { get; init; }
            public decimal TotalAmount { get; init; }
            public decimal DiscountAmount { get; init; }
            public decimal TaxAmount { get; init; }
            public decimal PaidAmount { get; init; }
            public string? PaymentStatus { get; init; }
            public decimal UnitPrice { get; init; }
            public decimal TaxRate { get; init; }
            public string? PayloadMarker { get; init; }
            public string? PersistedNotes { get; init; }
        };

        #region Entity Import Methods

        // category dùng mã làm khóa upsert: trùng mã thì cập nhật tên, chưa có thì tạo mới
        private void ImportCategories(
            IReadOnlyList<PreparedImportRow> rows,
            AppDbContext db,
            DynamicImportResult result,
            ref int rowIdx)
        {
            var categoryCodes = rows
                .Select(row => row.CategoryCode!)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var categoriesByCode = db.Categories
                .Where(category => categoryCodes.Contains(category.CategoryCode))
                .ToDictionary(category => category.CategoryCode, StringComparer.Ordinal);
            foreach (var row in rows)
            {
                rowIdx++;
                try
                {
                    string categoryCode = row.CategoryCode!;
                    string displayName = row.DisplayName!;

                    categoriesByCode.TryGetValue(categoryCode, out var existing);
                    if (existing != null)
                    {
                        existing.DisplayName = displayName;
                    }
                    else
                    {
                        var category = new Category
                        {
                            CategoryCode = categoryCode,
                            DisplayName = displayName,
                            IsActive = true
                        };
                        db.Categories.Add(category);
                        categoriesByCode[categoryCode] = category;
                    }
                    result.SuccessCount++;
                }
                catch (ArgumentException ex)
                {
                    result.Errors.Add(new RowError { RowNumber = rowIdx, ErrorMessage = ex.Message });
                }
            }
        }

        private async Task<int> ImportProductsAsync(
            IReadOnlyList<PreparedImportRow> rows,
            AppDbContext db,
            DynamicImportResult result,
            bool autoCreateReferences,
            int rowIdx,
            CancellationToken cancellationToken)
        {
            var categoryNames = rows.Select(row => row.CategoryName!).Distinct(StringComparer.Ordinal).ToList();
            var brandNames = rows.Select(row => row.BrandName!).Distinct(StringComparer.Ordinal).ToList();
            var unitNames = rows.Select(row => row.DefaultUnitName!).Distinct(StringComparer.Ordinal).ToList();
            var productCodes = rows.Select(row => row.ProductCode!).Distinct(StringComparer.Ordinal).ToList();
            var categoriesByName = db.Categories
                .Where(category => categoryNames.Contains(category.DisplayName))
                .ToList()
                .GroupBy(category => category.DisplayName, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var brandsByName = db.Brands
                .Where(brand => brandNames.Contains(brand.DisplayName))
                .ToList()
                .GroupBy(brand => brand.DisplayName, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var unitsByName = db.Units
                .Where(unit => unitNames.Contains(unit.DisplayName))
                .ToList()
                .GroupBy(unit => unit.DisplayName, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var productsByCode = db.Products
                .Where(product => productCodes.Contains(product.ProductCode))
                .ToList()
                .GroupBy(product => product.ProductCode, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                rowIdx++;
                try
                {
                    string productCode = row.ProductCode!;
                    string displayName = row.DisplayName!;
                    string? description = row.Description;
                    decimal? costPrice = row.CostPrice;
                    decimal defaultPrice = row.DefaultPrice;
                    string? origin = row.OriginCountry;
                    int warranty = row.WarrantyPeriodMonths;
                    bool isSerial = row.IsSerialTracked;

                    string categoryName = row.CategoryName!;
                    string brandName = row.BrandName!;
                    string unitName = row.DefaultUnitName!;

                    // các bảng tham chiếu phải có id trước khi gán vào product; tùy chọn auto-create quyết định tạo mới hay báo lỗi
                    // Resolve category
                    categoriesByName.TryGetValue(categoryName, out var category);
                    if (category == null)
                    {
                        if (autoCreateReferences)
                        {
                            category = new Category
                            {
                                DisplayName = categoryName,
                                CategoryCode = $"CAT-{Guid.NewGuid().ToString().Substring(0, 8).ToUpperInvariant()}",
                                IsActive = true
                            };
                            db.Categories.Add(category);
                            await db.SaveChangesAsync(cancellationToken);
                            categoriesByName[categoryName] = category;
                        }
                        else
                        {
                            throw new ArgumentException($"Không tìm thấy nhóm sản phẩm '{categoryName}' và tính năng tự động tạo mới đang tắt.");
                        }
                    }

                    // Resolve brand
                    brandsByName.TryGetValue(brandName, out var brand);
                    if (brand == null)
                    {
                        if (autoCreateReferences)
                        {
                            brand = new Brand
                            {
                                DisplayName = brandName,
                                BrandCode = $"BRD-{Guid.NewGuid().ToString().Substring(0, 8).ToUpperInvariant()}",
                                IsActive = true
                            };
                            db.Brands.Add(brand);
                            await db.SaveChangesAsync(cancellationToken);
                            brandsByName[brandName] = brand;
                        }
                        else
                        {
                            throw new ArgumentException($"Không tìm thấy thương hiệu '{brandName}' và tính năng tự động tạo mới đang tắt.");
                        }
                    }

                    // Resolve unit
                    unitsByName.TryGetValue(unitName, out var unit);
                    if (unit == null)
                    {
                        if (autoCreateReferences)
                        {
                            unit = new Unit
                            {
                                DisplayName = unitName,
                                UnitCode = $"UNT-{Guid.NewGuid().ToString().Substring(0, 8).ToUpperInvariant()}",
                                IsActive = true
                            };
                            db.Units.Add(unit);
                            await db.SaveChangesAsync(cancellationToken);
                            unitsByName[unitName] = unit;
                        }
                        else
                        {
                            throw new ArgumentException($"Không tìm thấy đơn vị tính '{unitName}' và tính năng tự động tạo mới đang tắt.");
                        }
                    }

                    productsByCode.TryGetValue(productCode, out var existing);
                    if (existing != null)
                    {
                        existing.DisplayName = displayName;
                        existing.Description = description;
                        existing.CostPrice = costPrice;
                        existing.DefaultPrice = defaultPrice;
                        existing.OriginCountry = origin;
                        existing.WarrantyPeriodMonths = warranty;
                        existing.IsSerialTracked = isSerial;
                        existing.CategoryId = category.Id;
                        existing.BrandId = brand.Id;
                        existing.DefaultUnitId = unit.Id;
                    }
                    else
                    {
                        var product = new Product
                        {
                            ProductCode = productCode,
                            DisplayName = displayName,
                            Description = description,
                            CostPrice = costPrice,
                            DefaultPrice = defaultPrice,
                            OriginCountry = origin,
                            WarrantyPeriodMonths = warranty,
                            IsSerialTracked = isSerial,
                            CategoryId = category.Id,
                            BrandId = brand.Id,
                            DefaultUnitId = unit.Id,
                            IsActive = true
                        };
                        db.Products.Add(product);
                        productsByCode[productCode] = product;
                    }
                    result.SuccessCount++;
                }
                catch (ArgumentException ex)
                {
                    result.Errors.Add(new RowError { RowNumber = rowIdx, ErrorMessage = ex.Message });
                }
            }

            return rowIdx;
        }

        // import serial là nhập tồn đầu kỳ: chỉ tạo serial mới qua chứng từ và posting service, không sửa vòng đời serial cũ
        private async Task<int> ImportProductSerialsAsync(
            IReadOnlyList<PreparedImportRow> rows,
            AppDbContext db,
            DynamicImportResult result,
            int userId,
            bool autoCreateReferences,
            Guid operationId,
            int rowIdx,
            CancellationToken cancellationToken)
        {
            var importedAt = DateTime.UtcNow;
            var warehouseKeys = rows
                .Select(row => row.WarehouseName?.Trim().ToUpperInvariant() ?? string.Empty)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();
            var documentCodes = warehouseKeys
                .Select((key, index) => new { key, code = $"SI-{operationId:N}-{index + 1}" })
                .ToDictionary(item => item.key, item => item.code, StringComparer.Ordinal);

            var stockRows = rows.Select(row => row with
            {
                DocumentCode = documentCodes[
                    row.WarehouseName?.Trim().ToUpperInvariant() ?? string.Empty],
                ImportDate = importedAt,
                Quantity = 1m,
                Serials = new[] { row.SerialNumber! },
                Notes = row.Note
            }).ToArray();

            var nextRow = await ImportStockInDocumentsAsync(
                stockRows, db, result, userId, autoCreateReferences,
                operationId, rowIdx, cancellationToken, openingBalance: true);

            var documentPrefix = $"SI-{operationId:N}-";
            var noteSerialNumbers = rows.Select(row => row.SerialNumber!).Distinct().ToList();
            var importedSerials = await db.ProductSerials
                .Where(item =>
                    noteSerialNumbers.Contains(item.SerialNumber) &&
                    item.LastStockInLine.StockIn.DocumentCode.StartsWith(documentPrefix))
                .ToListAsync(cancellationToken);
            var importedBySerial = importedSerials
                .GroupBy(serial => serial.SerialNumber, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            foreach (var row in rows)
            {
                if (importedBySerial.TryGetValue(row.SerialNumber!, out var serial))
                    serial.Note = row.Note;
            }

            return nextRow;
        }

        // bản ghi tạm giữ dữ liệu đã kiểm tra; chỉ sau khi cả chứng từ hợp lệ mới bắt đầu ghi xuống database
        private sealed record PreparedStockInImportLine(
            Product Product,
            decimal Quantity,
            decimal BaseQuantity,
            string[] SerialNumbers);

        private sealed record PreparedStockOutImportLine(
            Product Product,
            decimal Quantity,
            decimal BaseQuantity,
            string[] SerialNumbers);

        private async Task<int> ImportStockInDocumentsAsync(
            IReadOnlyList<PreparedImportRow> rows,
            AppDbContext db,
            DynamicImportResult result,
            int userId,
            bool autoCreateReferences,
            Guid operationId,
            int rowIdx,
            CancellationToken cancellationToken,
            bool openingBalance = false)
        {
            // mọi dòng cùng mã được xem là một chứng từ; mã trống được gom vào một phiếu tự sinh
            var grouped = GroupStockRows(rows);

            foreach (var group in grouped)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var groupRows = group.ToList();
                var firstRowNumber = rowIdx + 1;
                rowIdx += groupRows.Count;
                var savepointName = $"dynamic_import_{firstRowNumber}";
                await db.Database.CurrentTransaction!.CreateSavepointAsync(
                    savepointName, cancellationToken);

                try
                {
                    var defaultWarehouse = db.Warehouses
                        .Where(item => item.IsActive)
                        .OrderByDescending(item => item.IsDefault)
                        .ThenBy(item => item.Id)
                        .FirstOrDefault()
                        ?? throw new InventoryDomainException("Không tìm thấy kho đang hoạt động.");
                    var firstRow = groupRows[0];
                    var importDate = firstRow.ImportDate!.Value;
                    var supplierName = firstRow.SupplierName;
                    var payloadMarker = firstRow.PayloadMarker!;
                    var persistedNotes = firstRow.PersistedNotes!;
                    var warehouseName = firstRow.WarehouseName;
                    var warehouse = string.IsNullOrWhiteSpace(warehouseName)
                        ? defaultWarehouse
                        : db.Warehouses.SingleOrDefault(item => item.DisplayName == warehouseName && item.IsActive)
                          ?? throw new InventoryDomainException($"Không tìm thấy kho '{warehouseName}'.");

                    Supplier? supplier = null;
                    if (!string.IsNullOrWhiteSpace(supplierName))
                    {
                        supplier = db.Suppliers.SingleOrDefault(item => item.DisplayName == supplierName);
                        if (supplier is null)
                        {
                            if (!autoCreateReferences)
                            {
                                throw new InventoryDomainException($"Không tìm thấy nhà cung cấp '{supplierName}'.");
                            }

                            supplier = new Supplier
                            {
                                DisplayName = supplierName,
                                SupplierCode = $"SUP-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
                                IsActive = true
                            };
                        }
                    }

                    var documentCode = GetStockDocumentCode(group.Key, operationId, isStockIn: true);
                    var existingDocument = db.StockIns
                        .AsNoTracking()
                        .SingleOrDefault(item => item.DocumentCode == documentCode);
                    if (existingDocument is not null)
                    {
                        var existingLineCount = db.StockInLines.Count(line => line.StockInId == existingDocument.Id);
                        EnsurePayloadMatches(existingDocument.Notes, payloadMarker);
                        if (existingLineCount == groupRows.Count)
                        {
                            result.SuccessCount += groupRows.Count;
                            await db.Database.CurrentTransaction!.ReleaseSavepointAsync(
                                savepointName, cancellationToken);
                            continue;
                        }

                        throw new InventoryDomainException($"Mã phiếu nhập '{documentCode}' đã tồn tại nhưng số dòng không khớp.");
                    }

                    // preparedLines tách bước kiểm tra khỏi bước ghi; documentSerials chặn serial lặp giữa nhiều dòng của cùng phiếu
                    // baseQuantity là số lượng sau quy đổi về đơn vị cơ sở, dùng cho tồn kho và số lượng serial
                    var preparedLines = new List<PreparedStockInImportLine>();

                    foreach (var itemRow in groupRows)
                    {
                        var productCode = itemRow.ProductCode!;
                        var quantity = itemRow.Quantity;
                        if (quantity <= 0)
                        {
                            throw new InventoryDomainException("Stock-in quantity must be greater than zero.");
                        }

                        var product = db.Products.SingleOrDefault(item => item.ProductCode == productCode && item.IsActive)
                            ?? throw new InventoryDomainException($"Không tìm thấy sản phẩm '{productCode}' khi import dòng.");
                        var conversionFactor = db.ProductUnits
                            .Where(unit => unit.ProductId == product.Id && unit.UnitId == product.DefaultUnitId)
                            .Select(unit => unit.ConversionFactor)
                            .FirstOrDefault();
                        if (conversionFactor <= 0)
                        {
                            conversionFactor = 1m;
                        }

                        var baseQuantity = quantity * conversionFactor;
                        var serialNumbers = itemRow.Serials.ToArray();

                        if (product.IsSerialTracked &&
                            (baseQuantity != decimal.Truncate(baseQuantity) || serialNumbers.Length != (int)baseQuantity))
                        {
                            throw new InventoryDomainException("Serial count must match stock-in quantity.");
                        }

                        if (!product.IsSerialTracked && serialNumbers.Length > 0)
                        {
                            throw new InventoryDomainException("Non-serial products cannot receive serial numbers.");
                        }

                        if (serialNumbers.Length > 0 &&
                            db.ProductSerials.Any(serial => serialNumbers.Contains(serial.SerialNumber)))
                        {
                            throw new InventoryDomainException("One or more serial numbers already exist.");
                        }

                        preparedLines.Add(new PreparedStockInImportLine(
                            product,
                            quantity,
                            baseQuantity,
                            serialNumbers));
                    }

                    // import tạo phiếu ở trạng thái đã duyệt và đã ghi sổ để dữ liệu chứng từ khớp ngay với số dư kho
                    var now = DateTime.UtcNow;
                    var stockIn = new StockIn
                    {
                        DocumentCode = documentCode,
                        ImportDate = importDate,
                        WarehouseId = warehouse.Id,
                        Supplier = supplier,
                        PurposeCode = openingBalance ? "OpeningBalance" : "Purchase",
                        Notes = persistedNotes,
                        Status = StockDocumentStatus.Approved.ToString(),
                        CreatedBy = userId,
                        CreatedAt = now,
                        ApprovedBy = userId,
                        ApprovedAt = now,
                        PostedBy = userId,
                        PostedAt = now
                    };
                    db.StockIns.Add(stockIn);

                    var persistedLines = new List<(PreparedStockInImportLine Prepared, StockInLine Line)>();
                    foreach (var prepared in preparedLines)
                    {
                        var line = new StockInLine
                        {
                            StockIn = stockIn,
                            ProductId = prepared.Product.Id,
                            UnitId = prepared.Product.DefaultUnitId,
                            Quantity = prepared.Quantity,
                            BaseQuantity = prepared.BaseQuantity,
                            UnitPrice = prepared.Product.CostPrice ?? prepared.Product.DefaultPrice,
                            DraftSerials = prepared.SerialNumbers.Length == 0
                                ? null
                                : string.Join(",", prepared.SerialNumbers)
                        };
                        db.StockInLines.Add(line);
                        persistedLines.Add((prepared, line));
                    }

                    // cần lưu phiếu và các dòng trước để lấy id thật cho sổ kho và liên kết LastStockInLineId
                    await db.SaveChangesAsync(cancellationToken);
                    var postingService = new InventoryPostingService(
                        new EfInventoryUnitOfWork(db),
                        new DbDefaultWarehouseProvider(db),
                        new SystemClock());

                    foreach (var item in persistedLines)
                    {
                        postingService.PostStockIn(new PostStockInCommand(
                            stockIn.Id,
                            stockIn.WarehouseId,
                            openingBalance ? StockInKind.OpeningBalance : StockInKind.Purchase,
                            StockDocumentStatus.Approved,
                            item.Prepared.Product.Id,
                            item.Prepared.BaseQuantity,
                            item.Prepared.SerialNumbers,
                            userId));

                        if (item.Prepared.SerialNumbers.Length > 0)
                        {
                            var serials = db.ProductSerials
                                .Where(serial => item.Prepared.SerialNumbers.Contains(serial.SerialNumber))
                                .ToList();
                            foreach (var serial in serials)
                            {
                                serial.LastStockInLineId = item.Line.Id;
                            }
                        }
                    }

                    await db.SaveChangesAsync(cancellationToken);
                    await db.Database.CurrentTransaction!.ReleaseSavepointAsync(
                        savepointName, cancellationToken);
                    result.SuccessCount += groupRows.Count;
                }
                catch (ArgumentException ex)
                {
                    await RecordStockGroupErrorAsync(
                        db,
                        savepointName,
                        result,
                        firstRowNumber,
                        $"Lỗi tại nhóm phiếu nhập {group.Key}: {ex.Message}",
                        cancellationToken);
                }
                catch (InventoryDomainException ex)
                {
                    await RecordStockGroupErrorAsync(
                        db,
                        savepointName,
                        result,
                        firstRowNumber,
                        $"Lỗi tại nhóm phiếu nhập {group.Key}: {ex.Message}",
                        cancellationToken);
                }
            }

            return rowIdx;
        }

        // mỗi phiếu xuất dùng một savepoint trong transaction chung để rollback đúng nhóm lỗi
        private async Task<int> ImportStockOutDocumentsAsync(
            IReadOnlyList<PreparedImportRow> rows,
            AppDbContext db,
            DynamicImportResult result,
            int userId,
            bool autoCreateReferences,
            Guid operationId,
            int rowIdx,
            CancellationToken cancellationToken)
        {
            var grouped = GroupStockRows(rows);

            foreach (var group in grouped)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var groupRows = group.ToList();
                var firstRowNumber = rowIdx + 1;
                rowIdx += groupRows.Count;
                var savepointName = $"dynamic_import_{firstRowNumber}";
                await db.Database.CurrentTransaction!.CreateSavepointAsync(
                    savepointName, cancellationToken);

                try
                {
                    var defaultWarehouse = db.Warehouses
                        .Where(item => item.IsActive)
                        .OrderByDescending(item => item.IsDefault)
                        .ThenBy(item => item.Id)
                        .FirstOrDefault()
                        ?? throw new InventoryDomainException("Không tìm thấy kho đang hoạt động.");
                    var firstRow = groupRows[0];
                    var exportDate = firstRow.ExportDate!.Value;
                    var customerName = firstRow.CustomerName;
                    var payloadMarker = firstRow.PayloadMarker!;
                    var persistedNotes = firstRow.PersistedNotes!;
                    var warehouseName = firstRow.WarehouseName;
                    var warehouse = string.IsNullOrWhiteSpace(warehouseName)
                        ? defaultWarehouse
                        : db.Warehouses.SingleOrDefault(item => item.DisplayName == warehouseName && item.IsActive)
                          ?? throw new InventoryDomainException($"Không tìm thấy kho '{warehouseName}'.");

                    Customer? customer;
                    if (string.IsNullOrWhiteSpace(customerName))
                    {
                        customer = db.Customers.FirstOrDefault(item => item.CustomerCode == "CUS-LE")
                                   ?? db.Customers.FirstOrDefault(item => item.IsActive);
                    }
                    else
                    {
                        customer = db.Customers.SingleOrDefault(item => item.DisplayName == customerName);
                    }

                    if (customer is null)
                    {
                        if (!autoCreateReferences)
                        {
                            throw new InventoryDomainException("Không tìm thấy khách hàng phù hợp.");
                        }

                        customer = new Customer
                        {
                            DisplayName = string.IsNullOrWhiteSpace(customerName) ? "Khách bán lẻ" : customerName,
                            CustomerCode = $"CUS-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
                            IsActive = true
                        };
                    }

                    var documentCode = GetStockDocumentCode(group.Key, operationId, isStockIn: false);
                    var existingDocument = db.StockOuts
                        .AsNoTracking()
                        .SingleOrDefault(item => item.DocumentCode == documentCode);
                    if (existingDocument is not null)
                    {
                        var existingLineCount = db.StockOutLines.Count(line => line.StockOutId == existingDocument.Id);
                        EnsurePayloadMatches(existingDocument.Notes, payloadMarker);
                        if (existingLineCount == groupRows.Count)
                        {
                            result.SuccessCount += groupRows.Count;
                            await db.Database.CurrentTransaction!.ReleaseSavepointAsync(
                                savepointName, cancellationToken);
                            continue;
                        }

                        throw new InventoryDomainException($"Mã phiếu xuất '{documentCode}' đã tồn tại nhưng số dòng không khớp.");
                    }

                    // kiểm tra đủ tồn và trạng thái từng serial trước khi tạo phiếu, tránh ghi dở dang rồi mới phát hiện thiếu hàng
                    var preparedLines = new List<PreparedStockOutImportLine>();

                    foreach (var itemRow in groupRows)
                    {
                        var productCode = itemRow.ProductCode!;
                        var quantity = itemRow.Quantity;
                        if (quantity <= 0)
                        {
                            throw new InventoryDomainException("Stock-out quantity must be greater than zero.");
                        }

                        var product = db.Products.SingleOrDefault(item => item.ProductCode == productCode && item.IsActive)
                            ?? throw new InventoryDomainException($"Không tìm thấy sản phẩm '{productCode}' khi import dòng.");
                        var conversionFactor = db.ProductUnits
                            .Where(unit => unit.ProductId == product.Id && unit.UnitId == product.DefaultUnitId)
                            .Select(unit => unit.ConversionFactor)
                            .FirstOrDefault();
                        if (conversionFactor <= 0)
                        {
                            conversionFactor = 1m;
                        }

                        var baseQuantity = quantity * conversionFactor;
                        var serialNumbers = itemRow.Serials.ToArray();

                        if (product.IsSerialTracked &&
                            (baseQuantity != decimal.Truncate(baseQuantity) || serialNumbers.Length != (int)baseQuantity))
                        {
                            throw new InventoryDomainException("Serial count must match stock-out quantity.");
                        }

                        if (!product.IsSerialTracked && serialNumbers.Length > 0)
                        {
                            throw new InventoryDomainException("Non-serial products cannot be issued with serial numbers.");
                        }

                        foreach (var serialNumber in serialNumbers)
                        {
                            var serial = db.ProductSerials.SingleOrDefault(item => item.SerialNumber == serialNumber)
                                ?? throw new InventoryDomainException($"Serial {serialNumber} does not exist.");
                            if (serial.ProductId != product.Id ||
                                serial.CurrentWarehouseId != warehouse.Id ||
                                serial.CurrentStatus != "InStock")
                            {
                                throw new InventoryDomainException($"Serial {serialNumber} is not available in the selected warehouse.");
                            }
                        }

                        preparedLines.Add(new PreparedStockOutImportLine(
                            product,
                            quantity,
                            baseQuantity,
                            serialNumbers));
                    }

                    // cộng nhu cầu theo sản phẩm vì một sản phẩm có thể xuất ở nhiều dòng trong cùng chứng từ
                    foreach (var productGroup in preparedLines.GroupBy(item => item.Product.Id))
                    {
                        var requiredQuantity = productGroup.Sum(item => item.BaseQuantity);
                        var balance = db.StockBalances.SingleOrDefault(item =>
                            item.ProductId == productGroup.Key && item.WarehouseId == warehouse.Id);
                        if (balance is null || balance.AvailableQuantity < requiredQuantity)
                        {
                            throw new InventoryDomainException(
                                $"Không đủ tồn khả dụng cho sản phẩm {productGroup.First().Product.ProductCode}.");
                        }
                    }

                    var now = DateTime.UtcNow;
                    var stockOut = new StockOut
                    {
                        DocumentCode = documentCode,
                        ExportDate = exportDate,
                        WarehouseId = warehouse.Id,
                        Customer = customer,
                        PurposeCode = "Sale",
                        Notes = persistedNotes,
                        Status = StockDocumentStatus.Approved.ToString(),
                        CreatedBy = userId,
                        CreatedAt = now,
                        ApprovedBy = userId,
                        ApprovedAt = now,
                        PostedBy = userId,
                        PostedAt = now
                    };
                    db.StockOuts.Add(stockOut);

                    // giữ dữ liệu đã kiểm tra cùng entity mới để posting dùng đúng sản phẩm, số lượng và serial gắn đúng line id sau flush.
                    var persistedLines = new List<(PreparedStockOutImportLine Prepared, StockOutLine Line)>();
                    foreach (var prepared in preparedLines)
                    {
                        var line = new StockOutLine
                        {
                            StockOut = stockOut,
                            ProductId = prepared.Product.Id,
                            UnitId = prepared.Product.DefaultUnitId,
                            Quantity = prepared.Quantity,
                            BaseQuantity = prepared.BaseQuantity,
                            UnitPrice = prepared.Product.DefaultPrice,
                            DraftSerials = prepared.SerialNumbers.Length == 0
                                ? null
                                : string.Join(",", prepared.SerialNumbers)
                        };
                        db.StockOutLines.Add(line);
                        persistedLines.Add((prepared, line));
                    }

                    await db.SaveChangesAsync(cancellationToken);
                    var postingService = new InventoryPostingService(
                        new EfInventoryUnitOfWork(db),
                        new DbDefaultWarehouseProvider(db),
                        new SystemClock());

                    foreach (var item in persistedLines)
                    {
                        postingService.PostStockOut(new PostStockOutCommand(
                            stockOut.Id,
                            stockOut.WarehouseId,
                            StockOutKind.Sale,
                            StockDocumentStatus.Approved,
                            item.Prepared.Product.Id,
                            item.Prepared.BaseQuantity,
                            item.Prepared.SerialNumbers,
                            userId));

                        if (item.Prepared.SerialNumbers.Length > 0)
                        {
                            var serials = db.ProductSerials
                                .Where(serial => item.Prepared.SerialNumbers.Contains(serial.SerialNumber))
                                .ToList();
                            foreach (var serial in serials)
                            {
                                serial.LastStockOutLineId = item.Line.Id;
                            }
                        }
                    }

                    await db.SaveChangesAsync(cancellationToken);
                    await db.Database.CurrentTransaction!.ReleaseSavepointAsync(
                        savepointName, cancellationToken);
                    result.SuccessCount += groupRows.Count;
                }
                catch (ArgumentException ex)
                {
                    await RecordStockGroupErrorAsync(
                        db,
                        savepointName,
                        result,
                        firstRowNumber,
                        $"Lỗi tại nhóm phiếu xuất {group.Key}: {ex.Message}",
                        cancellationToken);
                }
                catch (InventoryDomainException ex)
                {
                    await RecordStockGroupErrorAsync(
                        db,
                        savepointName,
                        result,
                        firstRowNumber,
                        $"Lỗi tại nhóm phiếu xuất {group.Key}: {ex.Message}",
                        cancellationToken);
                }
            }

            return rowIdx;
        }

        private static async Task RecordStockGroupErrorAsync(
            AppDbContext db,
            string savepointName,
            DynamicImportResult result,
            int rowNumber,
            string message,
            CancellationToken cancellationToken)
        {
            await db.Database.CurrentTransaction!.RollbackToSavepointAsync(
                savepointName,
                cancellationToken);
            db.ChangeTracker.Clear();
            result.Errors.Add(new RowError
            {
                RowNumber = rowNumber,
                ErrorMessage = message
            });
        }

        // các dòng cùng InvoiceCode tạo một phần đầu hóa đơn và nhiều dòng chi tiết
        private async Task<int> ImportPurchaseInvoicesAsync(
            IReadOnlyList<PreparedImportRow> rows,
            AppDbContext db,
            DynamicImportResult result,
            int userId,
            bool autoCreateReferences,
            Guid operationId,
            int rowIdx,
            CancellationToken cancellationToken)
        {
            var grouped = rows.GroupBy(row => row.InvoiceCode!);

            foreach (var group in grouped)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var groupRows = group.ToList();
                    var firstRow = groupRows[0];
                    DateTime invoiceDate = firstRow.InvoiceDate!.Value;
                    string supplierName = firstRow.SupplierName!;
                    decimal totalAmount = firstRow.TotalAmount;
                    decimal discount = firstRow.DiscountAmount;
                    decimal taxAmount = firstRow.TaxAmount;
                    string status = firstRow.PaymentStatus!;
                    decimal paidAmount = firstRow.PaidAmount;


                    var preparedLines = new List<(Product Product, decimal Quantity, decimal UnitPrice, decimal TaxRate)>();
                    foreach (var itemRow in groupRows)
                    {
                        rowIdx++;
                        string productCode = itemRow.ProductCode!;
                        decimal qty = itemRow.Quantity;
                        decimal unitPrice = itemRow.UnitPrice;
                        decimal taxRate = itemRow.TaxRate;
                        var product = db.Products.FirstOrDefault(p => p.ProductCode == productCode)
                            ?? throw new ArgumentException($"Không tìm thấy sản phẩm '{productCode}'.");
                        preparedLines.Add((product, qty, unitPrice, taxRate));
                    }
                    var payloadMarker = firstRow.PayloadMarker!;
                    var existingInvoice = await db.PurchaseInvoices
                        .AsNoTracking()
                        .SingleOrDefaultAsync(item => item.InvoiceCode == group.Key, cancellationToken);
                    if (existingInvoice is not null)
                    {
                        EnsurePayloadMatches(existingInvoice.Notes, payloadMarker);
                        var existingLineCount = await db.PurchaseInvoiceLines.CountAsync(
                            line => line.PurchaseInvoiceId == existingInvoice.Id,
                            cancellationToken);
                        if (existingLineCount != groupRows.Count)
                        {
                            throw new InvalidOperationException(
                                "Import replay does not match the existing invoice lines.");
                        }

                        result.SuccessCount += groupRows.Count;
                        continue;
                    }
                    var calculatedSubTotal = ValidateImportedInvoiceTotals(
                        preparedLines, totalAmount, discount, taxAmount);
                    var supplier = db.Suppliers.FirstOrDefault(s => s.DisplayName == supplierName);
                    if (supplier == null)
                    {
                        if (autoCreateReferences)
                        {
                            supplier = new Supplier { DisplayName = supplierName, SupplierCode = $"SUP-{Guid.NewGuid().ToString().Substring(0, 8).ToUpperInvariant()}", IsActive = true };
                            db.Suppliers.Add(supplier);
                            await db.SaveChangesAsync(cancellationToken);
                        }
                        else
                        {
                            throw new ArgumentException($"Không tìm thấy nhà cung cấp '{supplierName}'.");
                        }
                    }

                    var invoice = new PurchaseInvoice
                    {
                        InvoiceCode = group.Key,
                        InvoiceDate = invoiceDate,
                        SupplierId = supplier.Id,
                        SubTotal = calculatedSubTotal,
                        TaxAmount = taxAmount,
                        GrandTotal = totalAmount,
                        PaymentStatus = status,
                        Notes = firstRow.PersistedNotes,
                        PaidAmount = paidAmount,
                        CreatedBy = userId,
                        CreatedAt = DateTime.Now
                    };

                    // flush phần đầu trước để lấy invoice.Id; các dòng chi tiết chỉ giữ khóa ngoại số, không dùng navigation.
                    db.PurchaseInvoices.Add(invoice);
                    await db.SaveChangesAsync(cancellationToken);

                    foreach (var item in preparedLines)
                    {
                        var line = new PurchaseInvoiceLine
                        {
                            PurchaseInvoiceId = invoice.Id,
                            ProductId = item.Product.Id,
                            UnitId = item.Product.DefaultUnitId,
                            Quantity = item.Quantity,
                            UnitPrice = item.UnitPrice,
                            TaxRate = item.TaxRate,
                            SubTotal = item.Quantity * item.UnitPrice,
                            TaxAmount = item.Quantity * item.UnitPrice * item.TaxRate,
                            GrandTotal = (item.Quantity * item.UnitPrice) * (1 + item.TaxRate)
                        };

                        db.PurchaseInvoiceLines.Add(line);
                    }
                    result.SuccessCount += group.Count();
                }
                catch (ArgumentException ex)
                {
                    result.Errors.Add(new RowError { RowNumber = rowIdx, ErrorMessage = $"Lỗi hóa đơn mua {group.Key}: {ex.Message}" });
                }
            }

            return rowIdx;
        }

        // cách nhóm giống hóa đơn mua nhưng tham chiếu khách hàng và giá bán
        private async Task<int> ImportSalesInvoicesAsync(
            IReadOnlyList<PreparedImportRow> rows,
            AppDbContext db,
            DynamicImportResult result,
            int userId,
            bool autoCreateReferences,
            Guid operationId,
            int rowIdx,
            CancellationToken cancellationToken)
        {
            var grouped = rows.GroupBy(row => row.InvoiceCode!);

            foreach (var group in grouped)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var groupRows = group.ToList();
                    var firstRow = groupRows[0];
                    DateTime invoiceDate = firstRow.InvoiceDate!.Value;
                    string customerName = firstRow.CustomerName!;
                    decimal totalAmount = firstRow.TotalAmount;
                    decimal discount = firstRow.DiscountAmount;
                    decimal taxAmount = firstRow.TaxAmount;
                    string status = firstRow.PaymentStatus!;

                    decimal paidAmount = firstRow.PaidAmount;

                    var preparedLines = new List<(Product Product, decimal Quantity, decimal UnitPrice, decimal TaxRate)>();
                    foreach (var itemRow in groupRows)
                    {
                        rowIdx++;
                        string productCode = itemRow.ProductCode!;
                        decimal qty = itemRow.Quantity;
                        decimal unitPrice = itemRow.UnitPrice;
                        decimal taxRate = itemRow.TaxRate;
                        var product = db.Products.FirstOrDefault(p => p.ProductCode == productCode)
                            ?? throw new ArgumentException($"Không tìm thấy sản phẩm '{productCode}'.");
                        preparedLines.Add((product, qty, unitPrice, taxRate));
                    }
                    var payloadMarker = firstRow.PayloadMarker!;
                    var existingInvoice = await db.SalesInvoices
                        .AsNoTracking()
                        .SingleOrDefaultAsync(item => item.InvoiceCode == group.Key, cancellationToken);
                    if (existingInvoice is not null)
                    {
                        EnsurePayloadMatches(existingInvoice.Notes, payloadMarker);
                        var existingLineCount = await db.SalesInvoiceLines.CountAsync(
                            line => line.SalesInvoiceId == existingInvoice.Id,
                            cancellationToken);
                        if (existingLineCount != groupRows.Count)
                        {
                            throw new InvalidOperationException(
                                "Import replay does not match the existing invoice lines.");
                        }

                        result.SuccessCount += groupRows.Count;
                        continue;
                    }
                    var customer = db.Customers.FirstOrDefault(c => c.DisplayName == customerName);
                    var calculatedSubTotal = ValidateImportedInvoiceTotals(
                        preparedLines, totalAmount, discount, taxAmount);
                    if (customer == null)
                    {
                        if (autoCreateReferences)
                        {
                            customer = new Customer { DisplayName = customerName, CustomerCode = $"CUS-{Guid.NewGuid().ToString().Substring(0, 8).ToUpperInvariant()}", IsActive = true };
                            db.Customers.Add(customer);
                            await db.SaveChangesAsync(cancellationToken);
                        }
                        else
                        {
                            throw new ArgumentException($"Không tìm thấy khách hàng '{customerName}'.");
                        }
                    }

                    var invoice = new SalesInvoice
                    {
                        InvoiceCode = group.Key,
                        InvoiceDate = invoiceDate,
                        CustomerId = customer.Id,
                        SubTotal = calculatedSubTotal,
                        TaxAmount = taxAmount,
                        GrandTotal = totalAmount,
                        PaymentStatus = status,
                        Notes = firstRow.PersistedNotes,
                        CreatedBy = userId,
                        PaidAmount = paidAmount,
                        CreatedAt = DateTime.Now
                    };

                    // flush phần đầu trước để lấy invoice.Id; các dòng chi tiết chỉ giữ khóa ngoại số, không dùng navigation.
                    db.SalesInvoices.Add(invoice);
                    await db.SaveChangesAsync(cancellationToken);

                    foreach (var item in preparedLines)
                    {
                        var line = new SalesInvoiceLine
                        {
                            SalesInvoiceId = invoice.Id,
                            ProductId = item.Product.Id,
                            UnitId = item.Product.DefaultUnitId,
                            Quantity = item.Quantity,
                            UnitPrice = item.UnitPrice,
                            TaxRate = item.TaxRate,
                            SubTotal = item.Quantity * item.UnitPrice,
                            TaxAmount = item.Quantity * item.UnitPrice * item.TaxRate,
                            GrandTotal = (item.Quantity * item.UnitPrice) * (1 + item.TaxRate)
                        };

                        db.SalesInvoiceLines.Add(line);
                    }
                    result.SuccessCount += group.Count();
                }
                catch (ArgumentException ex)
                {
                    result.Errors.Add(new RowError { RowNumber = rowIdx, ErrorMessage = $"Lỗi hóa đơn bán {group.Key}: {ex.Message}" });
                }
            }

            return rowIdx;
        }

        private static decimal ValidateImportedInvoiceTotals(
            IReadOnlyCollection<(Product Product, decimal Quantity, decimal UnitPrice, decimal TaxRate)> lines,
            decimal declaredTotal,
            decimal discount,
            decimal declaredTax)
        {
            if (discount != 0)
                throw new ArgumentException("Invoice discounts are not supported by the current invoice model.");

            var calculatedSubTotal = lines.Sum(line => line.Quantity * line.UnitPrice);
            var calculatedTax = lines.Sum(line => line.Quantity * line.UnitPrice * line.TaxRate);
            if (calculatedTax != declaredTax)
                throw new ArgumentException("Invoice tax total does not match its detail lines.");
            if (calculatedSubTotal + calculatedTax != declaredTotal)
                throw new ArgumentException("Invoice total does not match its detail lines.");

            return calculatedSubTotal;
        }

        #endregion

        #region Safe Value Parsers

        // mappings nối tên trường hệ thống với tiêu đề thật trong file; lỗi bắt buộc được báo ngay tại dòng đang đọc
        private static string GetMappedString(Dictionary<string, string> row, Dictionary<string, string> mappings, string dbKey, bool required)
        {
            if (!mappings.TryGetValue(dbKey, out string? excelHeader) || string.IsNullOrWhiteSpace(excelHeader))
            {
                if (required) throw new ArgumentException($"Cột ánh xạ bắt buộc '{dbKey}' chưa được cấu hình.");
                return "";
            }

            if (!row.TryGetValue(excelHeader, out string? val))
            {
                if (required) throw new ArgumentException($"Không tìm thấy cột '{excelHeader}' trong dữ liệu tải lên.");
                return "";
            }

            if (required && string.IsNullOrWhiteSpace(val))
            {
                throw new ArgumentException($"Giá trị của cột '{excelHeader}' không được để trống.");
            }

            return val.Trim();
        }

        // chuẩn hóa dấu phân cách trước rồi parse bằng invariant culture để máy có vùng miền khác nhau vẫn cho cùng kết quả
        private static decimal GetMappedDecimal(Dictionary<string, string> row, Dictionary<string, string> mappings, string dbKey, bool required)
        {
            string val = GetMappedString(row, mappings, dbKey, required);
            if (string.IsNullOrWhiteSpace(val)) return 0;

            // Handle decimal separation differences (e.g. dot vs comma)
            val = NormalizeNumberString(val);
            if (decimal.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal res))
            {
                return res;
            }

            throw new ArgumentException($"Giá trị '{val}' không đúng định dạng số.");
        }

        private static decimal? GetMappedDecimalNull(Dictionary<string, string> row, Dictionary<string, string> mappings, string dbKey)
        {
            string val = GetMappedString(row, mappings, dbKey, required: false);
            if (string.IsNullOrWhiteSpace(val)) return null;

            val = NormalizeNumberString(val);
            if (decimal.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal res))
            {
                return res;
            }

            throw new ArgumentException($"Giá trị '{val}' không đúng định dạng số.");
        }

        private static int? GetMappedInt(Dictionary<string, string> row, Dictionary<string, string> mappings, string dbKey)
        {
            string val = GetMappedString(row, mappings, dbKey, required: false);
            if (string.IsNullOrWhiteSpace(val)) return null;

            val = NormalizeNumberString(val);
            // Parse as double first in case it's represented as "10.0" in Excel
            if (double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out double dres))
            {
                return (int)dres;
            }

            throw new ArgumentException($"Giá trị '{val}' không đúng định dạng số nguyên.");
        }

        // ưu tiên các định dạng được công bố; current culture chỉ là đường lui cho file do người dùng nhập thủ công
        private static DateTime GetMappedDateTime(Dictionary<string, string> row, Dictionary<string, string> mappings, string dbKey, bool required)
        {
            string val = GetMappedString(row, mappings, dbKey, required);
            if (string.IsNullOrWhiteSpace(val)) return DateTime.Now;

            string[] formats = { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "MM/dd/yyyy", "yyyy/MM/dd", "dd-MM-yyyy" };
            if (DateTime.TryParseExact(val, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime res))
            {
                return res;
            }

            if (DateTime.TryParse(val, CultureInfo.CurrentCulture, DateTimeStyles.None, out res))
            {
                return res;
            }

            throw new ArgumentException($"Giá trị ngày tháng '{val}' không đúng định dạng (VD: dd/MM/yyyy hoặc yyyy-MM-dd).");
        }

        // chấp nhận các cách ghi phổ biến trong excel; ô trống trả null để bên gọi tự chọn giá trị mặc định
        private static bool? GetMappedBool(Dictionary<string, string> row, Dictionary<string, string> mappings, string dbKey)
        {
            string val = GetMappedString(row, mappings, dbKey, required: false);
            if (string.IsNullOrWhiteSpace(val)) return null;

            val = val.ToLowerInvariant();
            if (val == "1" || val == "true" || val == "có" || val == "yes" || val == "x") return true;
            if (val == "0" || val == "false" || val == "không" || val == "no") return false;

            return false;
        }

        // mục tiêu cuối là chuỗi dùng dấu chấm thập phân và không còn dấu phân nhóm hàng nghìn
        private static string NormalizeNumberString(string val)
        {
            // Remove group separators if comma is used as thousand separator, and dot as decimal
            // E.g., "1,234.56" -> "1234.56"
            // If comma is decimal and dot is thousand separator: "1.234,56" -> "1234.56"
            if (val.Contains(",") && val.Contains("."))
            {
                if (val.IndexOf(",") < val.IndexOf("."))
                {
                    // Comma is thousand separator
                    val = val.Replace(",", "");
                }
                else
                {
                    // Dot is thousand separator
                    val = val.Replace(".", "").Replace(",", ".");
                }
            }
            else if (val.Contains(","))
            {
                // Single comma might be decimal or thousand separator. Check position or default to decimal separator.
                val = val.Replace(",", ".");
            }

            return val;
        }

        #endregion

        // adapter nhỏ này cho phép dùng chung InventoryPostingService mà không để dịch vụ import tự sửa số dư kho
        private sealed class DbDefaultWarehouseProvider : IDefaultWarehouseProvider
        {
            private readonly AppDbContext _context;

            public DbDefaultWarehouseProvider(AppDbContext context)
            {
                _context = context;
            }

            public int GetDefaultWarehouseId()
            {
                var warehouse = _context.Warehouses
                    .FirstOrDefault(warehouse => warehouse.IsDefault && warehouse.IsActive);

                return warehouse?.Id ?? 1;
            }
        }

        private sealed class SystemClock : IClock
        {
            public DateTime Now => DateTime.Now;
        }
    }
}
