using System;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace QuanLyHangHoa.Data;

/// <summary>
/// giữ cấu hình provider SQL Server ở một chỗ để mọi context dùng cùng retry policy và application name.
/// </summary>
public static class AppDbContextOptionsFactory
{
    private const string ApplicationName = "WarePro";

    public static DbContextOptions<AppDbContext> Create(string connectionString) =>
        Configure(new DbContextOptionsBuilder<AppDbContext>(), connectionString).Options;

    public static DbContextOptionsBuilder<AppDbContext> Configure(
        DbContextOptionsBuilder<AppDbContext> builder,
        string connectionString)
    {
        Configure((DbContextOptionsBuilder)builder, connectionString);
        return builder;
    }

    public static DbContextOptionsBuilder Configure(
        DbContextOptionsBuilder builder,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        // ép application name giúp dba nhận ra kết nối WarePro kể cả khi connection string đến từ biến môi trường.
        var normalized = new SqlConnectionStringBuilder(connectionString)
        {
            ApplicationName = ApplicationName
        };

        return builder.UseSqlServer(
            normalized.ConnectionString,
            // maxRetryCount: 2 nghĩa là strategy này chạy tối đa ba attempt; executor tạo context/state mới cho từng attempt.
            sql => sql.EnableRetryOnFailure(
                maxRetryCount: 2,
                maxRetryDelay: TimeSpan.FromSeconds(2),
                errorNumbersToAdd: [1205]));
    }
}