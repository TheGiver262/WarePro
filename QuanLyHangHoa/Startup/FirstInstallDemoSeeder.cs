using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Configuration;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Services.DataImport;

namespace QuanLyHangHoa.Startup;

/// <summary>
/// Ranh giới nhỏ để kiểm thử quyết định nạp dữ liệu mẫu mà không cần SQL Server thật.
/// </summary>
public interface IFirstInstallDemoSeedRuntime
{
    string SeedWorkbookPath { get; }
    Task<bool> IsSeedRequiredAsync(CancellationToken cancellationToken);
    Task SeedAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Nạp dữ liệu demo đúng một lần cho catalog trống do bộ cài WarePro vừa tạo.
/// </summary>
public sealed class FirstInstallDemoSeeder
{
    private readonly IFirstInstallDemoSeedRuntime _runtime;
    private readonly bool _isExplicitDemoInstall;

    public FirstInstallDemoSeeder(
        IFirstInstallDemoSeedRuntime runtime,
        WareProSettings? settings = null)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _isExplicitDemoInstall = IsExplicitDemoInstall(settings);
    }

    public static FirstInstallDemoSeeder CreateDefault(
        string connectionString,
        WareProSettings? settings) =>
        new(new DefaultFirstInstallDemoSeedRuntime(connectionString), settings);

    private static bool IsExplicitDemoInstall(WareProSettings? settings) =>
        settings is not null
        && settings.InitialDataProfile == InitialDataProfile.Demo
        && (settings.DeploymentRole == DeploymentRole.Server
            || settings.DeploymentRole == DeploymentRole.Standalone);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!_isExplicitDemoInstall)
        {
            return;
        }

        if (!await _runtime.IsSeedRequiredAsync(cancellationToken))
        {
            return;
        }

        if (!File.Exists(_runtime.SeedWorkbookPath))
        {
            throw new SeedWorkbookMissingException(_runtime.SeedWorkbookPath);
        }

        await _runtime.SeedAsync(cancellationToken);
    }
}

internal sealed class DefaultFirstInstallDemoSeedRuntime : IFirstInstallDemoSeedRuntime
{
    private readonly string _connectionString;

    public DefaultFirstInstallDemoSeedRuntime(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public string SeedWorkbookPath => WareProPaths.Current.SeedWorkbookPath;

    public async Task<bool> IsSeedRequiredAsync(CancellationToken cancellationToken)
    {
        await using var database = CreateContext();
        await using var connection = database.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CASE WHEN
                OBJECT_ID(N'dbo.__WareProUpgradeCutover', N'U') IS NOT NULL
                AND OBJECT_ID(N'dbo.Product', N'U') IS NOT NULL
            THEN 1 ELSE 0 END;
            """;
        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) != 1)
        {
            return false;
        }

        command.CommandText = """
            SELECT CASE WHEN
                EXISTS
                (
                    SELECT 1
                    FROM dbo.__WareProUpgradeCutover
                    WHERE Id = 1
                      AND Status = N'Finalized'
                      AND InstallerCreatedDatabase = 1
                )
                AND NOT EXISTS (SELECT 1 FROM dbo.Product)
            THEN 1 ELSE 0 END;
            """;

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await using var lockConnection = await AcquireSeedLockAsync(cancellationToken);
        if (!await IsSeedRequiredAsync(cancellationToken))
        {
            return;
        }

        var seeder = new DatabaseSeeder(CreateContext, SeedWorkbookPath);
        await seeder.SeedAsync(cancellationToken);
    }

    private AppDbContext CreateContext() =>
        new(AppDbContextOptionsFactory.Create(_connectionString));

    private async Task<SqlConnection> AcquireSeedLockAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand("""
                DECLARE @LockResult INT;
                EXEC @LockResult = sys.sp_getapplock
                    @Resource = N'WAREPRO:FIRSTINSTALLSEED',
                    @LockMode = N'Exclusive',
                    @LockOwner = N'Session',
                    @LockTimeout = -1;
                IF @LockResult < 0
                    THROW 51000, 'Could not acquire first-install seed lock.', 1;
                """, connection)
            {
                CommandTimeout = 0
            };
            await command.ExecuteNonQueryAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}