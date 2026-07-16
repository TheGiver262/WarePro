using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Services.DataImport;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyHangHoa.Services;

/// <summary>
/// chuẩn bị database theo một luồng có khóa: kiểm tra compatibility, backup, nâng schema rồi seed khi cần.
/// </summary>
public sealed class DatabaseInitializer
{
    private const int CurrentSchemaVersion = DatabaseCompatibilityService.CurrentSchemaVersion;

    // bảng metadata là nguồn quyết định schema hiện tại và client tối thiểu được phép mở database.
    private const string SchemaMetadataSql = """
        IF OBJECT_ID(N'[dbo].[__WareProSchemaVersion]', N'U') IS NULL
        BEGIN
            CREATE TABLE [dbo].[__WareProSchemaVersion]
            (
                [Id] INT NOT NULL CONSTRAINT [PK___WareProSchemaVersion] PRIMARY KEY,
                [Version] INT NOT NULL,
                [MinimumClientVersion] NVARCHAR(32) NOT NULL,
                [AppliedByAppVersion] NVARCHAR(64) NOT NULL,
                [UpdatedAt] DATETIME2 NOT NULL
            );
            INSERT INTO [dbo].[__WareProSchemaVersion]
                ([Id], [Version], [MinimumClientVersion], [AppliedByAppVersion], [UpdatedAt])
            VALUES (1, 0, N'1.0.0', N'1.0.0', SYSUTCDATETIME());
        END;
        IF COL_LENGTH('__WareProSchemaVersion', 'MinimumClientVersion') IS NULL
            ALTER TABLE [dbo].[__WareProSchemaVersion] ADD [MinimumClientVersion] NVARCHAR(32) NULL;
        IF COL_LENGTH('__WareProSchemaVersion', 'AppliedByAppVersion') IS NULL
            ALTER TABLE [dbo].[__WareProSchemaVersion] ADD [AppliedByAppVersion] NVARCHAR(64) NULL;
        """;

    // phiên bản 1 bổ sung các cột nghiệp vụ còn thiếu và có thể chạy lặp an toàn.
    private const string SchemaVersion1Sql = """
        IF COL_LENGTH('Product', 'Description') IS NULL ALTER TABLE Product ADD Description NVARCHAR(MAX);
        IF COL_LENGTH('Product', 'CostPrice') IS NULL ALTER TABLE Product ADD CostPrice DECIMAL(18,2);
        IF COL_LENGTH('ProductSerial', 'Note') IS NULL ALTER TABLE ProductSerial ADD Note NVARCHAR(MAX);
        IF COL_LENGTH('ProductSerial', 'StockTransferLineId') IS NULL ALTER TABLE ProductSerial ADD StockTransferLineId INT;
        IF COL_LENGTH('SalesInvoice', 'CreatedAt') IS NULL ALTER TABLE SalesInvoice ADD CreatedAt DATETIME;
        IF COL_LENGTH('SalesInvoice', 'Notes') IS NULL ALTER TABLE SalesInvoice ADD Notes NVARCHAR(MAX);
        IF COL_LENGTH('SalesInvoice', 'PaidAmount') IS NULL ALTER TABLE SalesInvoice ADD PaidAmount DECIMAL(18,2);
        IF COL_LENGTH('SalesInvoice', 'PaymentStatus') IS NULL ALTER TABLE SalesInvoice ADD PaymentStatus NVARCHAR(50);
        IF COL_LENGTH('SalesInvoice', 'DueDate') IS NULL ALTER TABLE SalesInvoice ADD DueDate DATETIME;
        IF COL_LENGTH('PurchaseInvoice', 'CreatedAt') IS NULL ALTER TABLE PurchaseInvoice ADD CreatedAt DATETIME;
        IF COL_LENGTH('PurchaseInvoice', 'Notes') IS NULL ALTER TABLE PurchaseInvoice ADD Notes NVARCHAR(MAX);
        IF COL_LENGTH('PurchaseInvoice', 'PaidAmount') IS NULL ALTER TABLE PurchaseInvoice ADD PaidAmount DECIMAL(18,2);
        IF COL_LENGTH('PurchaseInvoice', 'PaymentStatus') IS NULL ALTER TABLE PurchaseInvoice ADD PaymentStatus NVARCHAR(50);
        IF COL_LENGTH('PurchaseInvoice', 'DueDate') IS NULL ALTER TABLE PurchaseInvoice ADD DueDate DATETIME;
        IF COL_LENGTH('StockIn', 'ImportDate') IS NULL ALTER TABLE StockIn ADD ImportDate DATETIME;
        IF COL_LENGTH('StockIn', 'Notes') IS NULL ALTER TABLE StockIn ADD Notes NVARCHAR(MAX);
        IF COL_LENGTH('StockIn', 'UpdatedAt') IS NULL ALTER TABLE StockIn ADD UpdatedAt DATETIME;
        IF COL_LENGTH('StockIn', 'UpdatedBy') IS NULL ALTER TABLE StockIn ADD UpdatedBy INT;
        IF COL_LENGTH('StockOut', 'ExportDate') IS NULL ALTER TABLE StockOut ADD ExportDate DATETIME;
        IF COL_LENGTH('StockOut', 'Notes') IS NULL ALTER TABLE StockOut ADD Notes NVARCHAR(MAX);
        IF COL_LENGTH('StockOut', 'UpdatedAt') IS NULL ALTER TABLE StockOut ADD UpdatedAt DATETIME;
        IF COL_LENGTH('StockOut', 'UpdatedBy') IS NULL ALTER TABLE StockOut ADD UpdatedBy INT;
        IF COL_LENGTH('StockOutLine', 'DraftSerials') IS NULL ALTER TABLE StockOutLine ADD DraftSerials NVARCHAR(MAX);
        IF COL_LENGTH('StockInLine', 'DraftSerials') IS NULL ALTER TABLE StockInLine ADD DraftSerials NVARCHAR(MAX);
        IF COL_LENGTH('StockAdjustment', 'Notes') IS NULL ALTER TABLE StockAdjustment ADD Notes NVARCHAR(MAX);
        IF COL_LENGTH('StockCountSession', 'Notes') IS NULL ALTER TABLE StockCountSession ADD Notes NVARCHAR(MAX);
        IF COL_LENGTH('StockTransfer', 'Notes') IS NULL ALTER TABLE StockTransfer ADD Notes NVARCHAR(MAX);
        IF COL_LENGTH('StockTransfer', 'UpdatedAt') IS NULL ALTER TABLE StockTransfer ADD UpdatedAt DATETIME;
        IF COL_LENGTH('StockTransfer', 'UpdatedBy') IS NULL ALTER TABLE StockTransfer ADD UpdatedBy INT;
        """;

    // phiên bản 2 thêm index cho các truy vấn kho, công nợ và bảo hành thường dùng.
    private const string SchemaVersion2Sql = """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProductSerial_Product_Warehouse_Status' AND object_id = OBJECT_ID('ProductSerial'))
            CREATE INDEX IX_ProductSerial_Product_Warehouse_Status ON ProductSerial (ProductId, CurrentWarehouseId, CurrentStatus);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PurchaseInvoice_PaymentStatus_InvoiceDate' AND object_id = OBJECT_ID('PurchaseInvoice'))
            CREATE INDEX IX_PurchaseInvoice_PaymentStatus_InvoiceDate ON PurchaseInvoice (PaymentStatus, InvoiceDate);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SalesInvoice_PaymentStatus_InvoiceDate' AND object_id = OBJECT_ID('SalesInvoice'))
            CREATE INDEX IX_SalesInvoice_PaymentStatus_InvoiceDate ON SalesInvoice (PaymentStatus, InvoiceDate);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StockIn_CreatedAt' AND object_id = OBJECT_ID('StockIn'))
            CREATE INDEX IX_StockIn_CreatedAt ON StockIn (CreatedAt);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StockIn_Status_ImportDate' AND object_id = OBJECT_ID('StockIn'))
            CREATE INDEX IX_StockIn_Status_ImportDate ON StockIn (Status, ImportDate);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StockOut_CreatedAt' AND object_id = OBJECT_ID('StockOut'))
            CREATE INDEX IX_StockOut_CreatedAt ON StockOut (CreatedAt);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StockOut_Status_ExportDate' AND object_id = OBJECT_ID('StockOut'))
            CREATE INDEX IX_StockOut_Status_ExportDate ON StockOut (Status, ExportDate);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WarrantyClaim_Status' AND object_id = OBJECT_ID('WarrantyClaim'))
            CREATE INDEX IX_WarrantyClaim_Status ON WarrantyClaim (Status);
        """;

    // phiên bản 3 thay unique index cũ để quy tắc một claim mở được xử lý ở lớp nghiệp vụ.
    private const string SchemaVersion3Sql = """
        IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_WarrantyClaim_OpenClaim_PerSerial' AND object_id = OBJECT_ID('WarrantyClaim'))
            DROP INDEX UX_WarrantyClaim_OpenClaim_PerSerial ON WarrantyClaim;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WarrantyClaim_ProductSerialId' AND object_id = OBJECT_ID('WarrantyClaim'))
            CREATE INDEX IX_WarrantyClaim_ProductSerialId ON WarrantyClaim (ProductSerialId);
        """;
    // phiên bản 4 lưu riêng thời điểm phê duyệt và ghi sổ của chứng từ kho.
    private const string SchemaVersion4Sql = """
        IF COL_LENGTH('StockIn', 'ApprovedAt') IS NULL ALTER TABLE StockIn ADD ApprovedAt DATETIME2(0) NULL;
        IF COL_LENGTH('StockIn', 'PostedAt') IS NULL ALTER TABLE StockIn ADD PostedAt DATETIME2(0) NULL;
        IF COL_LENGTH('StockOut', 'ApprovedAt') IS NULL ALTER TABLE StockOut ADD ApprovedAt DATETIME2(0) NULL;
        IF COL_LENGTH('StockOut', 'PostedAt') IS NULL ALTER TABLE StockOut ADD PostedAt DATETIME2(0) NULL;
        IF COL_LENGTH('StockAdjustment', 'ApprovedAt') IS NULL ALTER TABLE StockAdjustment ADD ApprovedAt DATETIME2(0) NULL;
        IF COL_LENGTH('StockAdjustment', 'PostedAt') IS NULL ALTER TABLE StockAdjustment ADD PostedAt DATETIME2(0) NULL;
        IF COL_LENGTH('StockTransfer', 'ApprovedAt') IS NULL ALTER TABLE StockTransfer ADD ApprovedAt DATETIME2(0) NULL;
        IF COL_LENGTH('StockTransfer', 'PostedAt') IS NULL ALTER TABLE StockTransfer ADD PostedAt DATETIME2(0) NULL;
        """;
    // phiên bản 5 chuẩn hóa trạng thái thanh toán trước khi đặt check constraint mới.
    private const string SchemaVersion5Sql = """
        IF OBJECT_ID(N'[dbo].[SalesInvoice]', N'U') IS NOT NULL
        BEGIN
            UPDATE SalesInvoice SET PaymentStatus = 'Unpaid' WHERE UPPER(PaymentStatus) = 'UNPAID';
            UPDATE SalesInvoice SET PaymentStatus = 'PartiallyPaid' WHERE UPPER(PaymentStatus) IN ('PARTIAL', 'PARTIALLYPAID');
            UPDATE SalesInvoice SET PaymentStatus = 'Paid' WHERE UPPER(PaymentStatus) = 'PAID';
            UPDATE SalesInvoice SET PaymentStatus = 'Overdue' WHERE UPPER(PaymentStatus) = 'OVERDUE';
            UPDATE SalesInvoice
            SET PaymentStatus = CASE
                WHEN PaidAmount >= GrandTotal AND GrandTotal > 0 THEN 'Paid'
                WHEN PaidAmount > 0 THEN 'PartiallyPaid'
                ELSE 'Unpaid'
            END
            WHERE PaymentStatus IS NULL OR PaymentStatus NOT IN ('Unpaid', 'PartiallyPaid', 'Paid', 'Overdue');
            IF OBJECT_ID(N'[dbo].[CK_SalesInvoice_PaymentStatus]', N'C') IS NOT NULL
                ALTER TABLE SalesInvoice DROP CONSTRAINT CK_SalesInvoice_PaymentStatus;
            ALTER TABLE SalesInvoice WITH CHECK ADD CONSTRAINT CK_SalesInvoice_PaymentStatus
                CHECK (PaymentStatus IN ('Unpaid', 'PartiallyPaid', 'Paid', 'Overdue'));
        END;

        IF OBJECT_ID(N'[dbo].[PurchaseInvoice]', N'U') IS NOT NULL
        BEGIN
            UPDATE PurchaseInvoice SET PaymentStatus = 'Unpaid' WHERE UPPER(PaymentStatus) = 'UNPAID';
            UPDATE PurchaseInvoice SET PaymentStatus = 'PartiallyPaid' WHERE UPPER(PaymentStatus) IN ('PARTIAL', 'PARTIALLYPAID');
            UPDATE PurchaseInvoice SET PaymentStatus = 'Paid' WHERE UPPER(PaymentStatus) = 'PAID';
            UPDATE PurchaseInvoice SET PaymentStatus = 'Overdue' WHERE UPPER(PaymentStatus) = 'OVERDUE';
            UPDATE PurchaseInvoice
            SET PaymentStatus = CASE
                WHEN PaidAmount >= GrandTotal AND GrandTotal > 0 THEN 'Paid'
                WHEN PaidAmount > 0 THEN 'PartiallyPaid'
                ELSE 'Unpaid'
            END
            WHERE PaymentStatus IS NULL OR PaymentStatus NOT IN ('Unpaid', 'PartiallyPaid', 'Paid', 'Overdue');
            IF OBJECT_ID(N'[dbo].[CK_PurchaseInvoice_PaymentStatus]', N'C') IS NOT NULL
                ALTER TABLE PurchaseInvoice DROP CONSTRAINT CK_PurchaseInvoice_PaymentStatus;
            ALTER TABLE PurchaseInvoice WITH CHECK ADD CONSTRAINT CK_PurchaseInvoice_PaymentStatus
                CHECK (PaymentStatus IN ('Unpaid', 'PartiallyPaid', 'Paid', 'Overdue'));
        END;
        """;


    private readonly Func<AppDbContext> _contextFactory;
    private readonly string _baseDirectory;
    private readonly string _connectionString;

    public DatabaseInitializer(
        Func<AppDbContext> contextFactory,
        string baseDirectory,
        string? connectionString = null)
    {
        _contextFactory = contextFactory;
        _baseDirectory = baseDirectory;
        _connectionString = connectionString ?? AppDbContext.GetConnectionString();
    }

    /// <summary>
    /// dùng fast path khi database đã sẵn sàng; mọi thay đổi thật đều chạy sau khi giữ schema lock.
    /// </summary>
    public void Initialize(CancellationToken cancellationToken = default)
    {
        // lượt đọc đầu chỉ dùng để đi đường nhanh khi database đã sẵn sàng.
        // quyết định thay đổi schema sẽ được đọc lại sau khi giữ khóa vì máy khác
        // có thể vừa nâng database trong khoảng thời gian này.
        cancellationToken.ThrowIfCancellationRequested();
        // forceSeed đến từ thao tác hỗ trợ; appVersion dùng cho compatibility và tên backup.
        var stopwatch = Stopwatch.StartNew();
        var forceSeed = StartupSeedPolicy.IsForceSeedEnabled();
        var compatibilityService = new DatabaseCompatibilityService();
        var appVersion = GetAppVersion();
        // false thường là database chưa tồn tại hoặc chưa đọc được bảng metadata.
        // bốn giá trị là snapshot không khóa chỉ để quyết định đường nhanh; chúng sẽ được đọc lại trước khi thay đổi.
        var stateAvailable = TryGetDatabaseState(
            out var schemaVersion, out var minimumClientVersion,
            out var hasAnyUsers, out var hasExistingBusinessTables);

        // client quá cũ phải dừng ngay cả khi schema không cần nâng, tránh mở mô hình dữ liệu mới hơn.
        if (stateAvailable)
        {
            var initialCompatibility = compatibilityService.Evaluate(schemaVersion, minimumClientVersion, appVersion);
            if (initialCompatibility.Status == DatabaseCompatibilityStatus.ClientUpdateRequired)
            {
                throw new DatabaseCompatibilityException(schemaVersion, minimumClientVersion, appVersion);
            }

            if (StartupSeedPolicy.CanSkipInitialization(
                schemaVersion,
                CurrentSchemaVersion,
                hasAnyUsers,
                forceSeed))
            {
                Trace.WriteLine($"[STARTUP] Database fast path: {stopwatch.ElapsedMilliseconds} ms");
                return;
            }
        }

        // từ đây bắt đầu đường thay đổi: context, connection đích và connection khóa có vòng đời riêng.
        using var db = _contextFactory();
        var connection = (SqlConnection)db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        // target giữ tên database đích; lock connection chuyển sang master để khóa tồn tại trước cả EnsureCreated.
        var target = new SqlConnectionStringBuilder(connection.ConnectionString);
        var lockConnectionString = new SqlConnectionStringBuilder(connection.ConnectionString)
        {
            InitialCatalog = "master"
        }.ConnectionString;
        // session này phải sống suốt backup, migration và seed vì application lock thuộc session.
        using var lockConnection = new SqlConnection(lockConnectionString);
        lockConnection.Open();

        try
        {
            using var schemaLock = SchemaUpgradeLock.Acquire(lockConnection, target.InitialCatalog);
            // khóa nằm trên connection tới master nên tồn tại trước cả lúc database được tạo.
            // giữ khóa xuyên suốt EnsureCreated, backup, nâng schema và seed.
            db.Database.EnsureCreated();
            Trace.WriteLine($"[STARTUP] EnsureCreated: {stopwatch.ElapsedMilliseconds} ms");
            cancellationToken.ThrowIfCancellationRequested();
            if (shouldClose)
            {
                connection.Open();
            }
            // chỉ trạng thái đọc dưới khóa mới được phép quyết định backup và nâng phiên bản.

            // snapshot dưới khóa là nguồn quyết định cuối cùng vì tiến trình khác có thể vừa nâng schema xong.
            var lockedState = GetCurrentSchemaState(db);
            var lockedCompatibility = compatibilityService.Evaluate(
                lockedState.SchemaVersion, lockedState.MinimumClientVersion, appVersion);
            if (lockedCompatibility.Status == DatabaseCompatibilityStatus.ClientUpdateRequired)
            {
                throw new DatabaseCompatibilityException(
                    lockedState.SchemaVersion, lockedState.MinimumClientVersion, appVersion);
            }
            // chỉ backup database có bảng nghiệp vụ và sắp bị thay đổi;
            // database mới không cần tạo một file backup rỗng.

            // chỉ database có bảng nghiệp vụ và schema cũ mới cần backup trước khi thay đổi.
            if (compatibilityService.RequiresBackup(
                lockedState.SchemaVersion, stateAvailable && hasExistingBusinessTables))
            {
                var backup = new DatabaseBackupService(
                    new SqlDatabaseBackupExecutor(connection),
                    () => DateTimeOffset.UtcNow,
                    () => appVersion).CreateAndVerify(connection.Database);
                Trace.WriteLine($"[STARTUP] Verified database backup: {backup.BackupPath}");
            }

            cancellationToken.ThrowIfCancellationRequested();
            // migration commit xong mới được seed để workbook luôn ghi vào cấu trúc mới nhất.
            ApplySchemaUpdates(db);
            // seed chạy sau schema để workbook luôn ghi vào cấu trúc mới nhất.
            Trace.WriteLine($"[STARTUP] Schema ready: {stopwatch.ElapsedMilliseconds} ms");
            cancellationToken.ThrowIfCancellationRequested();
            SeedIfNeeded(db);
            Trace.WriteLine($"[STARTUP] Seed check complete: {stopwatch.ElapsedMilliseconds} ms");
        }
        finally
        {
            if (shouldClose)
            {
                connection.Close();
            }
        }
    }

    private static string GetAppVersion() =>
        typeof(DatabaseInitializer).Assembly.GetName().Version?.ToString() ?? "unknown";

    /// <summary>
    /// đọc snapshot best-effort cho fast path; lỗi SQL trả false để luồng có khóa xử lý đầy đủ.
    /// </summary>
    private bool TryGetDatabaseState(
        out int schemaVersion,
        out string minimumClientVersion,
        out bool hasAnyUsers,
        out bool hasExistingBusinessTables)
    {
        // giá trị mặc định đại diện database chưa có metadata, người dùng hoặc bảng nghiệp vụ.
        schemaVersion = 0;
        minimumClientVersion = "1.0.0";
        hasAnyUsers = false;
        hasExistingBusinessTables = false;

        try
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = """
                DECLARE @SchemaVersion INT = 0;
                DECLARE @MinimumClientVersion NVARCHAR(32) = N'1.0.0';
                DECLARE @HasAnyUsers BIT = 0;
                DECLARE @HasExistingBusinessTables BIT = 0;

                IF OBJECT_ID(N'[dbo].[__WareProSchemaVersion]', N'U') IS NOT NULL
                BEGIN
                    EXEC sys.sp_executesql
                        N'SELECT @value = ISNULL(MAX([Version]), 0) FROM [dbo].[__WareProSchemaVersion];',
                        N'@value INT OUTPUT',
                        @value = @SchemaVersion OUTPUT;
                    IF COL_LENGTH('__WareProSchemaVersion', 'MinimumClientVersion') IS NOT NULL
                        EXEC sys.sp_executesql
                            N'SELECT @value = COALESCE(MAX(NULLIF([MinimumClientVersion], N'''')), N''1.0.0'') FROM [dbo].[__WareProSchemaVersion];',
                            N'@value NVARCHAR(32) OUTPUT',
                            @value = @MinimumClientVersion OUTPUT;
                END;

                IF OBJECT_ID(N'[dbo].[AppUser]', N'U') IS NOT NULL
                    EXEC sys.sp_executesql
                        N'SELECT @value = CASE WHEN EXISTS (SELECT TOP (1) 1 FROM [dbo].[AppUser]) THEN 1 ELSE 0 END;',
                        N'@value BIT OUTPUT',
                        @value = @HasAnyUsers OUTPUT;

                IF EXISTS (SELECT 1 FROM sys.tables
                    WHERE is_ms_shipped = 0 AND [name] <> N'__WareProSchemaVersion')
                    SET @HasExistingBusinessTables = 1;

                SELECT @SchemaVersion, @MinimumClientVersion,
                    @HasAnyUsers, @HasExistingBusinessTables;
                """;

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return false;
            }

            schemaVersion = reader.GetInt32(0);
            minimumClientVersion = reader.GetString(1);
            hasAnyUsers = reader.GetBoolean(2);
            hasExistingBusinessTables = reader.GetBoolean(3);
            return true;
        }
        // không kết luận từ lỗi đọc sớm; initializer sẽ thử lại qua context và báo lỗi đúng ở bước chính.
        catch (SqlException)
        {
            return false;
        }
    }

    /// <summary>
    /// áp dụng tuần tự các phiên bản còn thiếu và cập nhật metadata trong cùng transaction.
    /// </summary>
    private static void ApplySchemaUpdates(AppDbContext db)
    {
        // mỗi đoạn SQL kiểm tra sự tồn tại trước khi sửa để có thể chạy lại an toàn
        // sau một lần khởi động bị ngắt giữa chừng.
        if (GetCurrentSchemaVersion(db) >= CurrentSchemaVersion)
        {
            return;
        }

        // SQL tự kiểm tra version và sự tồn tại của từng đối tượng nên có thể chạy lại sau lần khởi động gián đoạn.
        var sql = $$"""
            {{SchemaMetadataSql}}

            DECLARE @CurrentVersion INT = ISNULL(
                (SELECT TOP (1) [Version] FROM [dbo].[__WareProSchemaVersion] WHERE [Id] = 1), 0);

            IF @CurrentVersion < 1
            BEGIN
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StockTransfer')
                    CREATE TABLE StockTransfer (Id INT IDENTITY(1,1) PRIMARY KEY, DocumentCode NVARCHAR(50) NOT NULL, FromWarehouseId INT NOT NULL, ToWarehouseId INT NOT NULL, Status NVARCHAR(50) NOT NULL, TransferDate DATETIME NOT NULL, Notes NVARCHAR(500), CreatedBy INT NOT NULL, ApprovedBy INT, PostedBy INT, CreatedAt DATETIME DEFAULT GETUTCDATE(), UpdatedAt DATETIME, UpdatedBy INT);
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StockTransferLine')
                    CREATE TABLE StockTransferLine (Id INT IDENTITY(1,1) PRIMARY KEY, StockTransferId INT NOT NULL, ProductId INT NOT NULL, UnitId INT NOT NULL, Quantity DECIMAL(18,2) NOT NULL, BaseQuantity DECIMAL(18,2) NOT NULL);

                {{SchemaVersion1Sql}}
            END;

            IF @CurrentVersion < 2
            BEGIN
                {{SchemaVersion2Sql}}
            END;

            IF @CurrentVersion < 3
            BEGIN
                {{SchemaVersion3Sql}}
            END;
            IF @CurrentVersion < 4
            BEGIN
                {{SchemaVersion4Sql}}
            END;
            IF @CurrentVersion < 5
            BEGIN
                {{SchemaVersion5Sql}}
            END;
            UPDATE [dbo].[__WareProSchemaVersion]
            SET [Version] = {{CurrentSchemaVersion}},
                [MinimumClientVersion] = N'1.0.0',
                [AppliedByAppVersion] = N'1.0.0',
                [UpdatedAt] = SYSUTCDATETIME()
            WHERE [Id] = 1 AND [Version] < {{CurrentSchemaVersion}};
            """;

        // metadata phiên bản và thay đổi nghiệp vụ phải commit cùng nhau.
        // nếu một lệnh lỗi, phiên bản database không được ghi nhận sai.
        // metadata chỉ được tăng phiên bản khi toàn bộ thay đổi nghiệp vụ đã thực thi thành công.
        using var transaction = db.Database.BeginTransaction();
        try
        {
            db.Database.ExecuteSqlRaw(sql);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static int GetCurrentSchemaVersion(AppDbContext db)
    {
        return GetCurrentSchemaState(db).SchemaVersion;
    }

    // hàm này giữ nguyên trạng thái mở/đóng connection mà caller đã truyền vào.
    private static (int SchemaVersion, string MinimumClientVersion) GetCurrentSchemaState(AppDbContext db)
    {
        // shouldClose ghi lại quyền sở hữu: chỉ đóng connection nếu chính hàm này đã mở nó.
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        try
        {
            if (shouldClose)
            {
                connection.Open();
            }

            using var command = connection.CreateCommand();
            command.CommandText = """
                DECLARE @SchemaVersion INT = 0;
                DECLARE @MinimumClientVersion NVARCHAR(32) = N'1.0.0';

                IF OBJECT_ID(N'[dbo].[__WareProSchemaVersion]', N'U') IS NULL
                    SELECT @SchemaVersion, @MinimumClientVersion;
                ELSE
                BEGIN
                    EXEC sys.sp_executesql
                        N'SELECT @value = ISNULL(MAX([Version]), 0) FROM [dbo].[__WareProSchemaVersion];',
                        N'@value INT OUTPUT',
                        @value = @SchemaVersion OUTPUT;
                    IF COL_LENGTH('__WareProSchemaVersion', 'MinimumClientVersion') IS NOT NULL
                        EXEC sys.sp_executesql
                            N'SELECT @value = COALESCE(MAX(NULLIF([MinimumClientVersion], N'''')), N''1.0.0'') FROM [dbo].[__WareProSchemaVersion];',
                            N'@value NVARCHAR(32) OUTPUT',
                            @value = @MinimumClientVersion OUTPUT;
                    SELECT @SchemaVersion, @MinimumClientVersion;
                END;
                """;

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return (0, "1.0.0");
            }

            return (reader.GetInt32(0), reader.GetString(1));
        }
        finally
        {
            if (shouldClose)
            {
                connection.Close();
            }
        }
    }

    /// <summary>
    /// seed database mới hoặc lần hỗ trợ có force flag; database đang dùng không bị seed lại mặc định.
    /// </summary>
    private void SeedIfNeeded(AppDbContext db)
    {
        // hasAnyUsers là dấu hiệu dữ liệu nền đã được tạo; forceSeed ghi đè dấu hiệu này có chủ đích.
        var excelPath = Path.Combine(_baseDirectory, "Database", "warepro_database_seed.xlsx");
        var forceSeed = StartupSeedPolicy.IsForceSeedEnabled();
        var hasAnyUsers = db.AppUsers.AsNoTracking().Any();

        // thiếu workbook chỉ là lỗi bắt buộc với database mới hoặc khi người vận hành yêu cầu seed lại.
        if (!File.Exists(excelPath))
        {
            if (!hasAnyUsers || forceSeed)
            {
                throw new SeedWorkbookMissingException(excelPath);
            }

            return;
        }

        // database đã có người dùng đi qua nhánh này mà không chạm dữ liệu hiện hữu.
        if (!StartupSeedPolicy.ShouldSeed(seedFileExists: true, hasAnyUsers, forceSeed))
        {
            return;
        }

        // seeder dùng cùng context đang nằm trong schema lock để không có tiến trình startup khác chạy song song.
        var seeder = new DatabaseSeeder(db, excelPath);
        var log = Task.Run(seeder.SeedAsync).GetAwaiter().GetResult();
        Console.WriteLine($"[SEED] Result: {log}");
    }
}
