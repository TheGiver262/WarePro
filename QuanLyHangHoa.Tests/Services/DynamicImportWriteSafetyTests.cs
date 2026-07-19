using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Services.DataImport;
using QuanLyHangHoa.Tests.Helpers;

namespace QuanLyHangHoa.Tests.Services;

public sealed class DynamicImportWriteSafetyTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public async Task Dynamic_import_rejects_unknown_type_before_executor()
    {
        var service = new DynamicImportService(
            () => throw new InvalidOperationException("executor should not open a context"));

        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.ExecuteImportAsync(
                [],
                ImportFileType.Unknown,
                [],
                1,
                false,
                Guid.NewGuid()));

        Assert.Equal("type", error.ParamName);
    }

    [Fact]
    public async Task Dynamic_import_rejects_nonpositive_user_before_executor()
    {
        var service = new DynamicImportService(
            () => throw new InvalidOperationException("executor should not open a context"));

        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.ExecuteImportAsync(
                [],
                ImportFileType.Category,
                [],
                0,
                false,
                Guid.NewGuid()));

        Assert.Equal("userId", error.ParamName);
    }

    [Fact]
    public async Task Dynamic_import_rejects_empty_operation_before_executor()
    {
        var service = new DynamicImportService(
            () => throw new InvalidOperationException("executor should not open a context"));

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExecuteImportAsync(
                [],
                ImportFileType.Category,
                [],
                1,
                false,
                Guid.Empty));

        Assert.Equal("operationId", error.ParamName);
    }

    [Fact]
    public async Task Invalid_later_invoice_row_is_rejected_before_executor_context_is_created()
    {
        var contextCount = 0;
        var service = new DynamicImportService(() =>
        {
            contextCount++;
            throw new InvalidOperationException("executor should not open a context");
        });
        var rows = ValidInvoiceRows(
            "SupplierName",
            "General Supplier",
            "PI-DYN-PREPARE",
            quantity: "1");
        rows.Add(InvoiceRow(
            "SupplierName",
            "General Supplier",
            "PI-DYN-PREPARE",
            "DYN-PRODUCT-001",
            quantity: "not-a-number"));

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExecuteImportAsync(
                rows,
                ImportFileType.PurchaseInvoice,
                InvoiceMappings("SupplierName"),
                1,
                false,
                Guid.NewGuid()));

        Assert.Contains("not-a-number", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, contextCount);
    }

    [Fact]
    public async Task Invalid_stock_serial_range_is_rejected_before_executor_context_is_created()
    {
        var (service, getContextCount) = ServiceThatRejectsExecutorEntry();

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteImportAsync(
            [StockRow("SI-DYN-RANGE", "SER-003-SER-001", "short notes")],
            ImportFileType.StockIn,
            StockMappings(),
            1,
            false,
            Guid.NewGuid()));

        Assert.Contains("range", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, getContextCount());
    }

    [Fact]
    public async Task Serial_range_ending_at_long_max_is_rejected_before_executor_context_is_created()
    {
        var (service, getContextCount) = ServiceThatRejectsExecutorEntry();

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteImportAsync(
            [StockRow(
                "SI-DYN-LONG-MAX",
                "SER-9223372036854775806-SER-9223372036854775807",
                "short notes")],
            ImportFileType.StockIn,
            StockMappings(),
            1,
            false,
            Guid.NewGuid()));

        Assert.Contains("maximum", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, getContextCount());
    }

    [Fact]
    public async Task Serial_range_over_limit_is_rejected_before_executor_context_is_created()
    {
        var (service, getContextCount) = ServiceThatRejectsExecutorEntry();

        await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteImportAsync(
            [StockRow("SI-DYN-RANGE-LIMIT", "SER-00000-SER-10000", "short notes")],
            ImportFileType.StockIn,
            StockMappings(),
            1,
            false,
            Guid.NewGuid()));

        Assert.Equal(0, getContextCount());
    }

    [Fact]
    public async Task Serial_over_schema_length_is_rejected_before_executor_context_is_created()
    {
        var (service, getContextCount) = ServiceThatRejectsExecutorEntry();

        await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteImportAsync(
            [StockRow("SI-DYN-SERIAL-LENGTH", new string('S', 151), "short notes")],
            ImportFileType.StockIn,
            StockMappings(),
            1,
            false,
            Guid.NewGuid()));

        Assert.Equal(0, getContextCount());
    }
    [Fact]
    public async Task Duplicate_stock_group_serial_is_rejected_before_executor_context_is_created()
    {
        var (service, getContextCount) = ServiceThatRejectsExecutorEntry();

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteImportAsync(
            [
                StockRow("SI-DYN-DUP", "DUP-001", "short notes", "DYN-PRODUCT-001"),
                StockRow("SI-DYN-DUP", "DUP-001", "short notes", "DYN-PRODUCT-002")
            ],
            ImportFileType.StockIn,
            StockMappings(),
            1,
            false,
            Guid.NewGuid()));

        Assert.Contains("Duplicate serial", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, getContextCount());
    }

    [Fact]
    public async Task Stock_notes_with_payload_are_validated_before_executor_context_is_created()
    {
        var (service, getContextCount) = ServiceThatRejectsExecutorEntry();

        await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteImportAsync(
            [StockRow("SI-DYN-NOTES", string.Empty, new string('N', 480))],
            ImportFileType.StockIn,
            StockMappings(),
            1,
            false,
            Guid.NewGuid()));

        Assert.Equal(0, getContextCount());
    }

    [Fact]
    public async Task Invoice_notes_with_payload_are_validated_before_executor_context_is_created()
    {
        var (service, getContextCount) = ServiceThatRejectsExecutorEntry();
        var rows = ValidInvoiceRows(
            "SupplierName", "General Supplier", "PI-DYN-NOTES", quantity: "1");
        rows[0]["Notes"] = new string('N', 480);

        await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteImportAsync(
            rows,
            ImportFileType.PurchaseInvoice,
            InvoiceMappings("SupplierName"),
            1,
            false,
            Guid.NewGuid()));

        Assert.Equal(0, getContextCount());
    }
    [Fact]
    public async Task Category_import_replay_is_an_upsert_with_exact_row_count()
    {
        using var connection = OpenDatabase();
        var operationId = Guid.NewGuid();
        var service = new DynamicImportService(() => DatabaseHelper.CreateContext(connection));
        var rows = CategoryRows(("DYN-SAFE-001", "Nhóm an toàn"));

        var first = await service.ExecuteImportAsync(
            rows, ImportFileType.Category, CategoryMappings(), 1, false, operationId);
        var replay = await service.ExecuteImportAsync(
            rows, ImportFileType.Category, CategoryMappings(), 1, false, operationId);

        Assert.Equal(1, first.SuccessCount);
        Assert.Equal(1, replay.SuccessCount);
        Assert.Empty(first.Errors);
        Assert.Empty(replay.Errors);
        using var db = DatabaseHelper.CreateContext(connection);
        Assert.Single(db.Categories.Where(category => category.CategoryCode == "DYN-SAFE-001"));
    }

    [Fact]
    public async Task Purchase_invoice_invalid_second_line_writes_no_header_or_lines()
    {
        using var connection = OpenDatabase();
        var service = new DynamicImportService(() => DatabaseHelper.CreateContext(connection));

        var result = await service.ExecuteImportAsync(
            InvoiceRows("SupplierName", "General Supplier", "PI-DYN-ATOMIC"),
            ImportFileType.PurchaseInvoice,
            InvoiceMappings("SupplierName"),
            1,
            false,
            Guid.NewGuid());

        Assert.Equal(0, result.SuccessCount);
        Assert.NotEmpty(result.Errors);
        Assert.Contains(
            result.Errors,
            error => error.ErrorMessage.Contains("DYN-MISSING-PRODUCT", StringComparison.Ordinal));
        using var db = DatabaseHelper.CreateContext(connection);
        Assert.DoesNotContain(db.PurchaseInvoices, invoice => invoice.InvoiceCode == "PI-DYN-ATOMIC");
        Assert.Empty(db.PurchaseInvoiceLines);
    }

    [Fact]
    public async Task Sales_invoice_invalid_second_line_writes_no_header_or_lines()
    {
        using var connection = OpenDatabase();
        var service = new DynamicImportService(() => DatabaseHelper.CreateContext(connection));

        var result = await service.ExecuteImportAsync(
            InvoiceRows("CustomerName", "General Customer", "SI-DYN-ATOMIC"),
            ImportFileType.SalesInvoice,
            InvoiceMappings("CustomerName"),
            1,
            false,
            Guid.NewGuid());

        Assert.Equal(0, result.SuccessCount);
        Assert.NotEmpty(result.Errors);
        Assert.Contains(
            result.Errors,
            error => error.ErrorMessage.Contains("DYN-MISSING-PRODUCT", StringComparison.Ordinal));
        using var db = DatabaseHelper.CreateContext(connection);
        Assert.DoesNotContain(db.SalesInvoices, invoice => invoice.InvoiceCode == "SI-DYN-ATOMIC");
        Assert.Empty(db.SalesInvoiceLines);
    }

    [Theory]
    [InlineData(ImportFileType.PurchaseInvoice, "SupplierName", "General Supplier", "PI-DYN-REPLAY")]
    [InlineData(ImportFileType.SalesInvoice, "CustomerName", "General Customer", "SI-DYN-REPLAY")]
    public async Task Invoice_import_replays_exact_payload_and_rejects_different_same_count_payload(
        ImportFileType type,
        string partyKey,
        string partyName,
        string invoiceCode)
    {
        using var connection = OpenDatabase();
        var operationId = Guid.NewGuid();
        var service = new DynamicImportService(() => DatabaseHelper.CreateContext(connection));
        var rows = ValidInvoiceRows(partyKey, partyName, invoiceCode, quantity: "1");
        var mappings = InvoiceMappings(partyKey);

        var first = await service.ExecuteImportAsync(
            rows, type, mappings, 1, false, operationId);
        var replay = await service.ExecuteImportAsync(
            rows, type, mappings, 1, false, operationId);
        var differentOperation = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExecuteImportAsync(
                rows, type, mappings, 1, false, Guid.NewGuid()));
        var changedRows = ValidInvoiceRows(partyKey, partyName, invoiceCode, quantity: "2");

        var mismatch = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExecuteImportAsync(
                changedRows, type, mappings, 1, false, operationId));

        Assert.Equal(1, first.SuccessCount);
        Assert.Equal(1, replay.SuccessCount);
        Assert.Empty(first.Errors);
        Assert.Empty(replay.Errors);
        Assert.Contains("payload", differentOperation.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("payload", mismatch.Message, StringComparison.OrdinalIgnoreCase);
        using var db = DatabaseHelper.CreateContext(connection);
        if (type == ImportFileType.PurchaseInvoice)
        {
            var invoice = Assert.Single(db.PurchaseInvoices.Where(item => item.InvoiceCode == invoiceCode));
            Assert.Single(db.PurchaseInvoiceLines.Where(line => line.PurchaseInvoiceId == invoice.Id));
        }
        else
        {
            var invoice = Assert.Single(db.SalesInvoices.Where(item => item.InvoiceCode == invoiceCode));
            Assert.Single(db.SalesInvoiceLines.Where(line => line.SalesInvoiceId == invoice.Id));
        }
    }

    [Fact]
    public async Task Dynamic_import_honors_pre_cancelled_token_without_writes()
    {
        using var connection = OpenDatabase();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = new DynamicImportService(() => DatabaseHelper.CreateContext(connection));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ExecuteImportAsync(
            CategoryRows(("DYN-CANCEL-001", "Không được ghi")),
            ImportFileType.Category,
            CategoryMappings(),
            1,
            false,
            Guid.NewGuid(),
            cancellation.Token));

        using var db = DatabaseHelper.CreateContext(connection);
        Assert.DoesNotContain(db.Categories, category => category.CategoryCode == "DYN-CANCEL-001");
    }

    [Fact]
    public async Task Dynamic_import_rolls_back_all_rows_when_final_database_flush_fails()
    {
        using var connection = OpenDatabase();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TRIGGER FailSecondDynamicCategory
                BEFORE INSERT ON Category
                WHEN NEW.CategoryCode = 'DYN-LATE-002'
                BEGIN
                    SELECT RAISE(ABORT, 'forced dynamic import failure');
                END;
                """;
            command.ExecuteNonQuery();
        }

        var service = new DynamicImportService(() => DatabaseHelper.CreateContext(connection));

        var error = await Assert.ThrowsAnyAsync<Exception>(() => service.ExecuteImportAsync(
            CategoryRows(
                ("DYN-LATE-001", "Dòng đầu"),
                ("DYN-LATE-002", "Dòng lỗi")),
            ImportFileType.Category,
            CategoryMappings(),
            1,
            false,
            Guid.NewGuid()));

        Assert.Contains("forced dynamic import failure", error.ToString(), StringComparison.Ordinal);
        using var db = DatabaseHelper.CreateContext(connection);
        Assert.DoesNotContain(db.Categories, category => category.CategoryCode.StartsWith("DYN-LATE-"));
    }

    [Fact]
    public async Task Product_import_internal_flush_uses_async_save_pipeline()
    {
        using var connection = OpenDatabase();
        var interceptor = new AsyncSaveOnlyInterceptor();
        var service = new DynamicImportService(() => CreateContext(connection, interceptor));
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductCode"] = "DYN-ASYNC-PRODUCT",
            ["DisplayName"] = "Async product",
            ["DefaultPrice"] = "50",
            ["CategoryName"] = "DYN-ASYNC-CATEGORY",
            ["BrandName"] = "DYN-ASYNC-BRAND",
            ["DefaultUnitName"] = "DYN-ASYNC-UNIT"
        };

        var result = await service.ExecuteImportAsync(
            [row],
            ImportFileType.Product,
            ProductMappings(),
            1,
            true,
            Guid.NewGuid());

        Assert.Equal(1, result.SuccessCount);
        Assert.Empty(result.Errors);
        Assert.Equal(0, interceptor.SyncCalls);
        Assert.True(interceptor.AsyncCalls > 0);
        using var db = DatabaseHelper.CreateContext(connection);
        Assert.Contains(db.Products, product => product.ProductCode == "DYN-ASYNC-PRODUCT");
    }

    [Fact]
    public void Invoice_commit_verification_checks_header_and_expected_line_count()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot,
            "QuanLyHangHoa",
            "Services",
            "DataImport",
            "DynamicImportService.cs"));

        Assert.Contains("PurchaseInvoiceLines.CountAsync", source, StringComparison.Ordinal);
        Assert.Contains("SalesInvoiceLines.CountAsync", source, StringComparison.Ordinal);
    }

    private static List<Dictionary<string, string>> InvoiceRows(
        string partyKey,
        string partyName,
        string invoiceCode) =>
    [
        InvoiceRow(partyKey, partyName, invoiceCode, "DYN-PRODUCT-001"),
        InvoiceRow(partyKey, partyName, invoiceCode, "DYN-MISSING-PRODUCT")
    ];

    private static List<Dictionary<string, string>> ValidInvoiceRows(
        string partyKey,
        string partyName,
        string invoiceCode,
        string quantity) =>
    [
        InvoiceRow(partyKey, partyName, invoiceCode, "DYN-PRODUCT-001", quantity)
    ];

    private static Dictionary<string, string> InvoiceRow(
        string partyKey,
        string partyName,
        string invoiceCode,
        string productCode,
        string quantity = "1") =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["InvoiceCode"] = invoiceCode,
            ["InvoiceDate"] = "2026-07-18",
            [partyKey] = partyName,
            ["TotalAmount"] = "100",
            ["PaymentStatus"] = "Paid",
            ["Notes"] = "dynamic replay",
            ["ProductCode"] = productCode,
            ["Quantity"] = quantity,
            ["UnitPrice"] = "50"
        };

    private static Dictionary<string, string> InvoiceMappings(string partyKey) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["InvoiceCode"] = "InvoiceCode",
            ["InvoiceDate"] = "InvoiceDate",
            [partyKey] = partyKey,
            ["TotalAmount"] = "TotalAmount",
            ["PaymentStatus"] = "PaymentStatus",
            ["Notes"] = "Notes",
            ["ProductCode"] = "ProductCode",
            ["Quantity"] = "Quantity",
            ["UnitPrice"] = "UnitPrice"
        };
    private static Dictionary<string, string> StockRow(
        string documentCode,
        string serialNumbers,
        string notes,
        string productCode = "DYN-PRODUCT-001") =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["DocumentCode"] = documentCode,
            ["ImportDate"] = "2026-07-18",
            ["SupplierName"] = string.Empty,
            ["WarehouseName"] = string.Empty,
            ["Notes"] = notes,
            ["ProductCode"] = productCode,
            ["Quantity"] = "1",
            ["SerialNumbers"] = serialNumbers
        };

    private static Dictionary<string, string> StockMappings() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["DocumentCode"] = "DocumentCode",
            ["ImportDate"] = "ImportDate",
            ["SupplierName"] = "SupplierName",
            ["WarehouseName"] = "WarehouseName",
            ["Notes"] = "Notes",
            ["ProductCode"] = "ProductCode",
            ["Quantity"] = "Quantity",
            ["SerialNumbers"] = "SerialNumbers"
        };

    private static (DynamicImportService Service, Func<int> GetContextCount)
        ServiceThatRejectsExecutorEntry()
    {
        var contextCount = 0;
        var service = new DynamicImportService(() =>
        {
            contextCount++;
            throw new InvalidOperationException("executor should not open a context");
        });
        return (service, () => contextCount);
    }
    private static SqliteConnection OpenDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = DatabaseHelper.CreateContext(connection);
        DatabaseHelper.SeedBasicData(db);
        db.Products.Add(new QuanLyHangHoa.Models.Product
        {
            Id = 1700,
            ProductCode = "DYN-PRODUCT-001",
            DisplayName = "Dynamic import product",
            CategoryId = 1,
            BrandId = 1,
            DefaultUnitId = 1,
            DefaultPrice = 50m,
            IsActive = true
        });
        db.SaveChanges();
        return connection;
    }

    private static List<Dictionary<string, string>> CategoryRows(
        params (string Code, string Name)[] values) =>
        values.Select(value => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CategoryCode"] = value.Code,
            ["DisplayName"] = value.Name
        }).ToList();

    private static Dictionary<string, string> CategoryMappings() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["CategoryCode"] = "CategoryCode",
            ["DisplayName"] = "DisplayName"
        };

    private static Dictionary<string, string> ProductMappings() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductCode"] = "ProductCode",
            ["DisplayName"] = "DisplayName",
            ["DefaultPrice"] = "DefaultPrice",
            ["CategoryName"] = "CategoryName",
            ["BrandName"] = "BrandName",
            ["DefaultUnitName"] = "DefaultUnitName"
        };

    private static AppDbContext CreateContext(
        SqliteConnection connection,
        params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptors)
            .Options;
        return new AppDbContext(options);
    }

    private sealed class AsyncSaveOnlyInterceptor : SaveChangesInterceptor
    {
        public int SyncCalls { get; private set; }
        public int AsyncCalls { get; private set; }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            SyncCalls++;
            throw new InvalidOperationException("synchronous SaveChanges is forbidden");
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            AsyncCalls++;
            return ValueTask.FromResult(result);
        }
    }
}
