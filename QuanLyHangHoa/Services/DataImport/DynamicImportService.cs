using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        private readonly Func<AppDbContext> _contextFactory;

        public DynamicImportService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

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
                    new() { Key = "PaymentStatus", DisplayName = "Trạng thái thanh toán (Paid/Partial/Unpaid)", IsRequired = false },
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
                    new() { Key = "PaymentStatus", DisplayName = "Trạng thái thanh toán (Paid/Partial/Unpaid)", IsRequired = false },
                    new() { Key = "Notes", DisplayName = "Ghi chú hóa đơn", IsRequired = false },
                    new() { Key = "ProductCode", DisplayName = "Mã sản phẩm", IsRequired = true },
                    new() { Key = "Quantity", DisplayName = "Số lượng", IsRequired = true, DataType = "decimal" },
                    new() { Key = "UnitPrice", DisplayName = "Đơn giá bán", IsRequired = true, DataType = "decimal" },
                    new() { Key = "TaxRate", DisplayName = "Thuế suất dòng hàng (ví dụ: 0.1)", IsRequired = false, DataType = "decimal" }
                },
                _ => new()
            };
        }

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

        public DynamicImportResult ExecuteImport(
            List<Dictionary<string, string>> rawRows,
            ImportFileType type,
            Dictionary<string, string> mappings, // Maps Db Field Key -> Excel Header Name
            int userId,
            bool autoCreateReferences = true)
        {
            var result = new DynamicImportResult();
            var fields = GetFieldDefinitions(type);

            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();

            try
            {
                int rowIdx = 1;
                switch (type)
                {
                    case ImportFileType.Category:
                        ImportCategories(rawRows, mappings, db, result, ref rowIdx);
                        break;
                    case ImportFileType.Product:
                        ImportProducts(rawRows, mappings, db, result, autoCreateReferences, ref rowIdx);
                        break;
                    case ImportFileType.ProductSerial:
                        ImportProductSerials(rawRows, mappings, db, result, autoCreateReferences, ref rowIdx);
                        break;
                    case ImportFileType.StockIn:
                        ImportStockInDocuments(rawRows, mappings, db, result, userId, autoCreateReferences, ref rowIdx);
                        break;
                    case ImportFileType.StockOut:
                        ImportStockOutDocuments(rawRows, mappings, db, result, userId, autoCreateReferences, ref rowIdx);
                        break;
                    case ImportFileType.PurchaseInvoice:
                        ImportPurchaseInvoices(rawRows, mappings, db, result, userId, autoCreateReferences, ref rowIdx);
                        break;
                    case ImportFileType.SalesInvoice:
                        ImportSalesInvoices(rawRows, mappings, db, result, userId, autoCreateReferences, ref rowIdx);
                        break;
                }

                db.SaveChanges();
                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                result.Errors.Add(new RowError { RowNumber = 0, ErrorMessage = $"Lỗi hệ thống trong quá trình import: {ex.Message}" });
            }

            return result;
        }

        #region Entity Import Methods

        private void ImportCategories(
            List<Dictionary<string, string>> rawRows,
            Dictionary<string, string> mappings,
            AppDbContext db,
            DynamicImportResult result,
            ref int rowIdx)
        {
            foreach (var rawRow in rawRows)
            {
                rowIdx++;
                try
                {
                    string categoryCode = GetMappedString(rawRow, mappings, "CategoryCode", required: true);
                    string displayName = GetMappedString(rawRow, mappings, "DisplayName", required: true);

                    var existing = db.Categories.FirstOrDefault(c => c.CategoryCode == categoryCode);
                    if (existing != null)
                    {
                        existing.DisplayName = displayName;
                    }
                    else
                    {
                        db.Categories.Add(new Category
                        {
                            CategoryCode = categoryCode,
                            DisplayName = displayName,
                            IsActive = true
                        });
                    }
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new RowError { RowNumber = rowIdx, ErrorMessage = ex.Message });
                }
            }
        }

        private void ImportProducts(
            List<Dictionary<string, string>> rawRows,
            Dictionary<string, string> mappings,
            AppDbContext db,
            DynamicImportResult result,
            bool autoCreateReferences,
            ref int rowIdx)
        {
            foreach (var rawRow in rawRows)
            {
                rowIdx++;
                try
                {
                    string productCode = GetMappedString(rawRow, mappings, "ProductCode", required: true);
                    string displayName = GetMappedString(rawRow, mappings, "DisplayName", required: true);
                    string? description = GetMappedString(rawRow, mappings, "Description", required: false);
                    decimal? costPrice = GetMappedDecimalNull(rawRow, mappings, "CostPrice");
                    decimal defaultPrice = GetMappedDecimal(rawRow, mappings, "DefaultPrice", required: true);
                    string? origin = GetMappedString(rawRow, mappings, "OriginCountry", required: false);
                    int warranty = GetMappedInt(rawRow, mappings, "WarrantyPeriodMonths") ?? 0;
                    bool isSerial = GetMappedBool(rawRow, mappings, "IsSerialTracked") ?? false;

                    string categoryName = GetMappedString(rawRow, mappings, "CategoryName", required: true);
                    string brandName = GetMappedString(rawRow, mappings, "BrandName", required: true);
                    string unitName = GetMappedString(rawRow, mappings, "DefaultUnitName", required: true);

                    // Resolve category
                    var category = db.Categories.FirstOrDefault(c => c.DisplayName == categoryName);
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
                            db.SaveChanges();
                        }
                        else
                        {
                            throw new Exception($"Không tìm thấy nhóm sản phẩm '{categoryName}' và tính năng tự động tạo mới đang tắt.");
                        }
                    }

                    // Resolve brand
                    var brand = db.Brands.FirstOrDefault(b => b.DisplayName == brandName);
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
                            db.SaveChanges();
                        }
                        else
                        {
                            throw new Exception($"Không tìm thấy thương hiệu '{brandName}' và tính năng tự động tạo mới đang tắt.");
                        }
                    }

                    // Resolve unit
                    var unit = db.Units.FirstOrDefault(u => u.DisplayName == unitName);
                    if (unit == null)
                    {
                        if (autoCreateReferences)
                        {
                            unit = new Unit
                            {
                                DisplayName = unitName,
                                IsActive = true
                            };
                            db.Units.Add(unit);
                            db.SaveChanges();
                        }
                        else
                        {
                            throw new Exception($"Không tìm thấy đơn vị tính '{unitName}' và tính năng tự động tạo mới đang tắt.");
                        }
                    }

                    var existing = db.Products.FirstOrDefault(p => p.ProductCode == productCode);
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
                        db.Products.Add(new Product
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
                        });
                    }
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new RowError { RowNumber = rowIdx, ErrorMessage = ex.Message });
                }
            }
        }

        private void ImportProductSerials(
            List<Dictionary<string, string>> rawRows,
            Dictionary<string, string> mappings,
            AppDbContext db,
            DynamicImportResult result,
            bool autoCreateReferences,
            ref int rowIdx)
        {
            var warehouse = db.Warehouses.FirstOrDefault(w => w.IsDefault && w.IsActive) 
                            ?? db.Warehouses.FirstOrDefault(w => w.IsActive)
                            ?? db.Warehouses.First();

            foreach (var rawRow in rawRows)
            {
                rowIdx++;
                try
                {
                    string serialNumber = GetMappedString(rawRow, mappings, "SerialNumber", required: true);
                    string productCode = GetMappedString(rawRow, mappings, "ProductCode", required: true);
                    string? warehouseName = GetMappedString(rawRow, mappings, "WarehouseName", required: false);
                    string? note = GetMappedString(rawRow, mappings, "Note", required: false);

                    var product = db.Products.FirstOrDefault(p => p.ProductCode == productCode);
                    if (product == null)
                    {
                        throw new Exception($"Không tìm thấy sản phẩm có mã '{productCode}'.");
                    }

                    int whId = warehouse.Id;
                    if (!string.IsNullOrEmpty(warehouseName))
                    {
                        var wh = db.Warehouses.FirstOrDefault(w => w.DisplayName == warehouseName);
                        if (wh != null)
                        {
                            whId = wh.Id;
                        }
                        else if (!autoCreateReferences)
                        {
                            throw new Exception($"Không tìm thấy kho '{warehouseName}'.");
                        }
                    }

                    var existing = db.ProductSerials.FirstOrDefault(s => s.SerialNumber == serialNumber);
                    if (existing != null)
                    {
                        existing.ProductId = product.Id;
                        existing.CurrentWarehouseId = whId;
                        existing.Note = note;
                        existing.CurrentStatus = "InStock";
                    }
                    else
                    {
                        db.ProductSerials.Add(new ProductSerial
                        {
                            SerialNumber = serialNumber,
                            ProductId = product.Id,
                            CurrentWarehouseId = whId,
                            Note = note,
                            CurrentStatus = "InStock",
                            LastStockInLineId = 1 // Placeholder for legacy schema compatibility if nulls not allowed
                        });
                    }
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new RowError { RowNumber = rowIdx, ErrorMessage = ex.Message });
                }
            }
        }

        private void ImportStockInDocuments(
            List<Dictionary<string, string>> rawRows,
            Dictionary<string, string> mappings,
            AppDbContext db,
            DynamicImportResult result,
            int userId,
            bool autoCreateReferences,
            ref int rowIdx)
        {
            var warehouse = db.Warehouses.FirstOrDefault(w => w.IsDefault && w.IsActive) 
                            ?? db.Warehouses.FirstOrDefault(w => w.IsActive)
                            ?? db.Warehouses.First();

            // Group rows by DocumentCode or a virtual fallback code to allow batching
            var grouped = rawRows.GroupBy(r => {
                string code = GetMappedString(r, mappings, "DocumentCode", required: false);
                return string.IsNullOrWhiteSpace(code) ? "AUTO-GEN" : code;
            });

            foreach (var group in grouped)
            {
                try
                {
                    var firstRow = group.First();
                    DateTime importDate = GetMappedDateTime(firstRow, mappings, "ImportDate", required: true);
                    string? supplierName = GetMappedString(firstRow, mappings, "SupplierName", required: false);
                    string? notes = GetMappedString(firstRow, mappings, "Notes", required: false);
                    string? warehouseName = GetMappedString(firstRow, mappings, "WarehouseName", required: false);

                    int whId = warehouse.Id;
                    if (!string.IsNullOrEmpty(warehouseName))
                    {
                        var wh = db.Warehouses.FirstOrDefault(w => w.DisplayName == warehouseName);
                        if (wh != null)
                        {
                            whId = wh.Id;
                        }
                        else if (!autoCreateReferences)
                        {
                            throw new Exception($"Không tìm thấy kho '{warehouseName}'.");
                        }
                    }

                    int? supplierId = null;
                    if (!string.IsNullOrEmpty(supplierName))
                    {
                        var supplier = db.Suppliers.FirstOrDefault(s => s.DisplayName == supplierName);
                        if (supplier == null)
                        {
                            if (autoCreateReferences)
                            {
                                supplier = new Supplier { DisplayName = supplierName, SupplierCode = $"SUP-{Guid.NewGuid().ToString().Substring(0, 8).ToUpperInvariant()}", IsActive = true };
                                db.Suppliers.Add(supplier);
                                db.SaveChanges();
                            }
                            else
                            {
                                throw new Exception($"Không tìm thấy nhà cung cấp '{supplierName}'.");
                            }
                        }
                        supplierId = supplier.Id;
                    }

                    var stockIn = new StockIn
                    {
                        DocumentCode = group.Key == "AUTO-GEN" ? $"SI-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpperInvariant()}" : group.Key,
                        ImportDate = importDate,
                        WarehouseId = whId,
                        SupplierId = supplierId,
                        PurposeCode = "Purchase",
                        Notes = notes,
                        Status = DocumentStatus.Draft,
                        CreatedBy = userId,
                        CreatedAt = DateTime.Now
                    };

                    db.StockIns.Add(stockIn);
                    db.SaveChanges(); // Get StockIn.Id

                    var lines = new List<StockInLine>();
                    foreach (var itemRow in group)
                    {
                        rowIdx++;
                        string productCode = GetMappedString(itemRow, mappings, "ProductCode", required: true);
                        decimal qty = GetMappedDecimal(itemRow, mappings, "Quantity", required: true);
                        string? serialsStr = GetMappedString(itemRow, mappings, "SerialNumbers", required: false);

                        var product = db.Products.FirstOrDefault(p => p.ProductCode == productCode);
                        if (product == null)
                            throw new Exception($"Không tìm thấy sản phẩm '{productCode}' khi import dòng.");

                        var line = new StockInLine
                        {
                            StockInId = stockIn.Id,
                            ProductId = product.Id,
                            UnitId = product.DefaultUnitId,
                            Quantity = qty,
                            BaseQuantity = qty,
                            UnitPrice = product.CostPrice ?? product.DefaultPrice
                        };

                        if (!string.IsNullOrWhiteSpace(serialsStr))
                        {
                            var listSerials = StockInService.ParseSerialRange(serialsStr);
                            foreach (var s in listSerials)
                            {
                                line.ProductSerials.Add(new ProductSerial
                                {
                                    SerialNumber = s,
                                    ProductId = product.Id,
                                    CurrentWarehouseId = whId,
                                    CurrentStatus = "InStock",
                                    LastStockInLineId = 0 // Will link to line below
                                });
                            }
                        }

                        db.StockInLines.Add(line);
                        lines.Add(line);
                    }

                    db.SaveChanges();

                    // Resolve backward reference for newly created serials to their LastStockInLineId
                    foreach (var line in lines)
                    {
                        foreach (var s in line.ProductSerials)
                        {
                            s.LastStockInLineId = line.Id;
                        }
                    }
                    db.SaveChanges();

                    // Auto Post the Document for Stock Level computation
                    // For stock-in we simulate posting so inventory increases
                    var postingService = new InventoryPostingService(
                        new EfInventoryUnitOfWork(db),
                        new DbDefaultWarehouseProvider(db),
                        new SystemClock());

                    stockIn.Status = DocumentStatus.Posted;
                    stockIn.PostedBy = userId;
                    stockIn.PostedAt = DateTime.Now;

                    foreach (var line in lines)
                    {
                        var serials = line.ProductSerials.Select(s => s.SerialNumber).ToArray();
                        postingService.PostStockIn(new PostStockInCommand(
                            stockIn.Id,
                            stockIn.WarehouseId,
                            StockInKind.Purchase,
                            StockDocumentStatus.Posted,
                            line.ProductId,
                            (int)line.Quantity,
                            serials,
                            userId));
                    }

                    result.SuccessCount += group.Count();
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new RowError { RowNumber = rowIdx, ErrorMessage = $"Lỗi tại nhóm hóa đơn {group.Key}: {ex.Message}" });
                }
            }
        }

        private void ImportStockOutDocuments(
            List<Dictionary<string, string>> rawRows,
            Dictionary<string, string> mappings,
            AppDbContext db,
            DynamicImportResult result,
            int userId,
            bool autoCreateReferences,
            ref int rowIdx)
        {
            var warehouse = db.Warehouses.FirstOrDefault(w => w.IsDefault && w.IsActive) 
                            ?? db.Warehouses.FirstOrDefault(w => w.IsActive)
                            ?? db.Warehouses.First();

            var grouped = rawRows.GroupBy(r => {
                string code = GetMappedString(r, mappings, "DocumentCode", required: false);
                return string.IsNullOrWhiteSpace(code) ? "AUTO-GEN" : code;
            });

            foreach (var group in grouped)
            {
                try
                {
                    var firstRow = group.First();
                    DateTime exportDate = GetMappedDateTime(firstRow, mappings, "ExportDate", required: true);
                    string? customerName = GetMappedString(firstRow, mappings, "CustomerName", required: false);
                    string? notes = GetMappedString(firstRow, mappings, "Notes", required: false);
                    string? warehouseName = GetMappedString(firstRow, mappings, "WarehouseName", required: false);

                    int whId = warehouse.Id;
                    if (!string.IsNullOrEmpty(warehouseName))
                    {
                        var wh = db.Warehouses.FirstOrDefault(w => w.DisplayName == warehouseName);
                        if (wh != null)
                        {
                            whId = wh.Id;
                        }
                        else if (!autoCreateReferences)
                        {
                            throw new Exception($"Không tìm thấy kho '{warehouseName}'.");
                        }
                    }

                    int customerId = 0;
                    if (!string.IsNullOrEmpty(customerName))
                    {
                        var customer = db.Customers.FirstOrDefault(c => c.DisplayName == customerName);
                        if (customer == null)
                        {
                            if (autoCreateReferences)
                            {
                                customer = new Customer { DisplayName = customerName, CustomerCode = $"CUS-{Guid.NewGuid().ToString().Substring(0, 8).ToUpperInvariant()}", IsActive = true };
                                db.Customers.Add(customer);
                                db.SaveChanges();
                            }
                            else
                            {
                                throw new Exception($"Không tìm thấy khách hàng '{customerName}'.");
                            }
                        }
                        customerId = customer.Id;
                    }
                    else
                    {
                        var defaultCust = db.Customers.FirstOrDefault(c => c.CustomerCode == "CUS-LE")
                                       ?? db.Customers.FirstOrDefault(c => c.IsActive)
                                       ?? db.Customers.FirstOrDefault();
                        if (defaultCust == null)
                        {
                            if (autoCreateReferences)
                            {
                                defaultCust = new Customer { DisplayName = "Khách bán lẻ", CustomerCode = "CUS-LE", IsActive = true };
                                db.Customers.Add(defaultCust);
                                db.SaveChanges();
                            }
                            else
                            {
                                throw new Exception("Không tìm thấy khách hàng mặc định 'Khách bán lẻ' (CUS-LE) và tự động tạo đang tắt.");
                            }
                        }
                        customerId = defaultCust.Id;
                    }

                    var stockOut = new StockOut
                    {
                        DocumentCode = group.Key == "AUTO-GEN" ? $"SO-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpperInvariant()}" : group.Key,
                        ExportDate = exportDate,
                        WarehouseId = whId,
                        CustomerId = customerId,
                        PurposeCode = "Sale",
                        Notes = notes,
                        Status = DocumentStatus.Draft,
                        CreatedBy = userId,
                        CreatedAt = DateTime.Now
                    };

                    db.StockOuts.Add(stockOut);
                    db.SaveChanges();

                    var lines = new List<StockOutLine>();
                    foreach (var itemRow in group)
                    {
                        rowIdx++;
                        string productCode = GetMappedString(itemRow, mappings, "ProductCode", required: true);
                        decimal qty = GetMappedDecimal(itemRow, mappings, "Quantity", required: true);
                        string? serialsStr = GetMappedString(itemRow, mappings, "SerialNumbers", required: false);

                        var product = db.Products.FirstOrDefault(p => p.ProductCode == productCode);
                        if (product == null)
                            throw new Exception($"Không tìm thấy sản phẩm '{productCode}' khi import dòng.");

                        var line = new StockOutLine
                        {
                            StockOutId = stockOut.Id,
                            ProductId = product.Id,
                            UnitId = product.DefaultUnitId,
                            Quantity = qty,
                            BaseQuantity = qty,
                            UnitPrice = product.DefaultPrice
                        };

                        if (!string.IsNullOrWhiteSpace(serialsStr))
                        {
                            var listSerials = StockInService.ParseSerialRange(serialsStr);
                            foreach (var s in listSerials)
                            {
                                var serial = db.ProductSerials.FirstOrDefault(ps => ps.SerialNumber == s && ps.ProductId == product.Id);
                                if (serial != null)
                                {
                                    line.ProductSerials.Add(serial);
                                }
                            }
                        }

                        db.StockOutLines.Add(line);
                        lines.Add(line);
                    }
                    db.SaveChanges();

                    // Auto Post StockOut
                    var postingService = new InventoryPostingService(
                        new EfInventoryUnitOfWork(db),
                        new DbDefaultWarehouseProvider(db),
                        new SystemClock());

                    stockOut.Status = DocumentStatus.Posted;
                    stockOut.PostedBy = userId;
                    stockOut.PostedAt = DateTime.Now;

                    foreach (var line in lines)
                    {
                        var serials = line.ProductSerials.Select(s => s.SerialNumber).ToArray();
                        postingService.PostStockOut(new PostStockOutCommand(
                            stockOut.Id,
                            stockOut.WarehouseId,
                            StockOutKind.Sale,
                            StockDocumentStatus.Posted,
                            line.ProductId,
                            (int)line.Quantity,
                            serials,
                            userId));
                    }

                    result.SuccessCount += group.Count();
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new RowError { RowNumber = rowIdx, ErrorMessage = $"Lỗi tại nhóm phiếu xuất {group.Key}: {ex.Message}" });
                }
            }
        }

        private void ImportPurchaseInvoices(
            List<Dictionary<string, string>> rawRows,
            Dictionary<string, string> mappings,
            AppDbContext db,
            DynamicImportResult result,
            int userId,
            bool autoCreateReferences,
            ref int rowIdx)
        {
            var grouped = rawRows.GroupBy(r => GetMappedString(r, mappings, "InvoiceCode", required: true));

            foreach (var group in grouped)
            {
                try
                {
                    var firstRow = group.First();
                    DateTime invoiceDate = GetMappedDateTime(firstRow, mappings, "InvoiceDate", required: true);
                    string supplierName = GetMappedString(firstRow, mappings, "SupplierName", required: true);
                    decimal totalAmount = GetMappedDecimal(firstRow, mappings, "TotalAmount", required: true);
                    decimal discount = GetMappedDecimalNull(firstRow, mappings, "DiscountAmount") ?? 0;
                    decimal taxAmount = GetMappedDecimalNull(firstRow, mappings, "TaxAmount") ?? 0;
                    string? status = GetMappedString(firstRow, mappings, "PaymentStatus", required: false) ?? "Paid";
                    string? notes = GetMappedString(firstRow, mappings, "Notes", required: false);

                    var supplier = db.Suppliers.FirstOrDefault(s => s.DisplayName == supplierName);
                    if (supplier == null)
                    {
                        if (autoCreateReferences)
                        {
                            supplier = new Supplier { DisplayName = supplierName, SupplierCode = $"SUP-{Guid.NewGuid().ToString().Substring(0, 8).ToUpperInvariant()}", IsActive = true };
                            db.Suppliers.Add(supplier);
                            db.SaveChanges();
                        }
                        else
                        {
                            throw new Exception($"Không tìm thấy nhà cung cấp '{supplierName}'.");
                        }
                    }

                    var invoice = new PurchaseInvoice
                    {
                        InvoiceCode = group.Key,
                        InvoiceDate = invoiceDate,
                        SupplierId = supplier.Id,
                        SubTotal = totalAmount - taxAmount + discount,
                        TaxAmount = taxAmount,
                        GrandTotal = totalAmount,
                        PaymentStatus = status,
                        Notes = notes,
                        CreatedBy = userId,
                        CreatedAt = DateTime.Now
                    };

                    db.PurchaseInvoices.Add(invoice);
                    db.SaveChanges();

                    foreach (var itemRow in group)
                    {
                        rowIdx++;
                        string productCode = GetMappedString(itemRow, mappings, "ProductCode", required: true);
                        decimal qty = GetMappedDecimal(itemRow, mappings, "Quantity", required: true);
                        decimal unitPrice = GetMappedDecimal(itemRow, mappings, "UnitPrice", required: true);
                        decimal taxRate = GetMappedDecimalNull(itemRow, mappings, "TaxRate") ?? 0;

                        var product = db.Products.FirstOrDefault(p => p.ProductCode == productCode);
                        if (product == null)
                            throw new Exception($"Không tìm thấy sản phẩm '{productCode}'.");

                        var line = new PurchaseInvoiceLine
                        {
                            PurchaseInvoiceId = invoice.Id,
                            ProductId = product.Id,
                            UnitId = product.DefaultUnitId,
                            Quantity = qty,
                            UnitPrice = unitPrice,
                            TaxRate = taxRate,
                            SubTotal = qty * unitPrice,
                            TaxAmount = qty * unitPrice * taxRate,
                            GrandTotal = (qty * unitPrice) * (1 + taxRate)
                        };

                        db.PurchaseInvoiceLines.Add(line);
                    }
                    result.SuccessCount += group.Count();
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new RowError { RowNumber = rowIdx, ErrorMessage = $"Lỗi hóa đơn mua {group.Key}: {ex.Message}" });
                }
            }
        }

        private void ImportSalesInvoices(
            List<Dictionary<string, string>> rawRows,
            Dictionary<string, string> mappings,
            AppDbContext db,
            DynamicImportResult result,
            int userId,
            bool autoCreateReferences,
            ref int rowIdx)
        {
            var grouped = rawRows.GroupBy(r => GetMappedString(r, mappings, "InvoiceCode", required: true));

            foreach (var group in grouped)
            {
                try
                {
                    var firstRow = group.First();
                    DateTime invoiceDate = GetMappedDateTime(firstRow, mappings, "InvoiceDate", required: true);
                    string customerName = GetMappedString(firstRow, mappings, "CustomerName", required: true);
                    decimal totalAmount = GetMappedDecimal(firstRow, mappings, "TotalAmount", required: true);
                    decimal discount = GetMappedDecimalNull(firstRow, mappings, "DiscountAmount") ?? 0;
                    decimal taxAmount = GetMappedDecimalNull(firstRow, mappings, "TaxAmount") ?? 0;
                    string? status = GetMappedString(firstRow, mappings, "PaymentStatus", required: false) ?? "Paid";
                    string? notes = GetMappedString(firstRow, mappings, "Notes", required: false);

                    var customer = db.Customers.FirstOrDefault(c => c.DisplayName == customerName);
                    if (customer == null)
                    {
                        if (autoCreateReferences)
                        {
                            customer = new Customer { DisplayName = customerName, CustomerCode = $"CUS-{Guid.NewGuid().ToString().Substring(0, 8).ToUpperInvariant()}", IsActive = true };
                            db.Customers.Add(customer);
                            db.SaveChanges();
                        }
                        else
                        {
                            throw new Exception($"Không tìm thấy khách hàng '{customerName}'.");
                        }
                    }

                    var invoice = new SalesInvoice
                    {
                        InvoiceCode = group.Key,
                        InvoiceDate = invoiceDate,
                        CustomerId = customer.Id,
                        SubTotal = totalAmount - taxAmount + discount,
                        TaxAmount = taxAmount,
                        GrandTotal = totalAmount,
                        PaymentStatus = status,
                        Notes = notes,
                        CreatedBy = userId,
                        CreatedAt = DateTime.Now
                    };

                    db.SalesInvoices.Add(invoice);
                    db.SaveChanges();

                    foreach (var itemRow in group)
                    {
                        rowIdx++;
                        string productCode = GetMappedString(itemRow, mappings, "ProductCode", required: true);
                        decimal qty = GetMappedDecimal(itemRow, mappings, "Quantity", required: true);
                        decimal unitPrice = GetMappedDecimal(itemRow, mappings, "UnitPrice", required: true);
                        decimal taxRate = GetMappedDecimalNull(itemRow, mappings, "TaxRate") ?? 0;

                        var product = db.Products.FirstOrDefault(p => p.ProductCode == productCode);
                        if (product == null)
                            throw new Exception($"Không tìm thấy sản phẩm '{productCode}'.");

                        var line = new SalesInvoiceLine
                        {
                            SalesInvoiceId = invoice.Id,
                            ProductId = product.Id,
                            UnitId = product.DefaultUnitId,
                            Quantity = qty,
                            UnitPrice = unitPrice,
                            TaxRate = taxRate,
                            SubTotal = qty * unitPrice,
                            TaxAmount = qty * unitPrice * taxRate,
                            GrandTotal = (qty * unitPrice) * (1 + taxRate)
                        };

                        db.SalesInvoiceLines.Add(line);
                    }
                    result.SuccessCount += group.Count();
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new RowError { RowNumber = rowIdx, ErrorMessage = $"Lỗi hóa đơn bán {group.Key}: {ex.Message}" });
                }
            }
        }

        #endregion

        #region Safe Value Parsers

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

        private static bool? GetMappedBool(Dictionary<string, string> row, Dictionary<string, string> mappings, string dbKey)
        {
            string val = GetMappedString(row, mappings, dbKey, required: false);
            if (string.IsNullOrWhiteSpace(val)) return null;

            val = val.ToLowerInvariant();
            if (val == "1" || val == "true" || val == "có" || val == "yes" || val == "x") return true;
            if (val == "0" || val == "false" || val == "không" || val == "no") return false;

            return false;
        }

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
