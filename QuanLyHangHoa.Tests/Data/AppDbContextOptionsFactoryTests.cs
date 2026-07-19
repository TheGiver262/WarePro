using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;

namespace QuanLyHangHoa.Tests.Data;

public sealed class AppDbContextOptionsFactoryTests
{
    [Fact]
    public void Create_uses_sql_server_retrying_strategy_and_warepro_application_name()
    {
        const string connectionString =
            "Server=.\\SQLEXPRESS;Database=master;Integrated Security=True;TrustServerCertificate=True";

        var options = AppDbContextOptionsFactory.Create(connectionString);
        using var context = new AppDbContext(options);
        var configuredConnectionString = context.Database.GetConnectionString();

        var strategy = Assert.IsType<SqlServerRetryingExecutionStrategy>(
            context.Database.CreateExecutionStrategy());
        Assert.Equal(2, strategy.MaxRetryCount);
        Assert.Equal(TimeSpan.FromSeconds(2), strategy.MaxRetryDelay);
        Assert.Contains(1205, strategy.AdditionalErrorNumbers!);
        Assert.NotNull(configuredConnectionString);

        var parsed = new SqlConnectionStringBuilder(configuredConnectionString);
        Assert.Equal("master", parsed.InitialCatalog);
        Assert.Equal("WarePro", parsed.ApplicationName);
    }

    [Fact]
    public void Configure_returns_the_same_builder_for_composition()
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();

        var configured = AppDbContextOptionsFactory.Configure(
            builder,
            "Server=.\\SQLEXPRESS;Database=master;Integrated Security=True");

        Assert.Same(builder, configured);
    }
}