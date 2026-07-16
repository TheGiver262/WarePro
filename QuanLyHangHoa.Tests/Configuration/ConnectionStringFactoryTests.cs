using System.Net;
using System.Security;
using Microsoft.Data.SqlClient;
using QuanLyHangHoa.Configuration;

namespace QuanLyHangHoa.Tests.Configuration;

public class ConnectionStringFactoryTests
{
    [Fact]
    public void Non_empty_environment_connection_string_wins_over_every_other_source()
    {
        const string environmentValue = "Server=env;Database=env-db;Trusted_Connection=True";
        var credentialStore = new FakeSqlCredentialStore(CreateCredential("ignored", "ignored"));
        var settings = WareProSettings.CreateDefault();
        settings.Database.Authentication = DatabaseAuthentication.SqlPassword;

        var result = new ConnectionStringFactory(credentialStore, () => environmentValue).Resolve(settings);

        Assert.Equal(environmentValue, result);
        Assert.Equal(0, credentialStore.ReadCount);
    }

    [Fact]
    public void Windows_authentication_uses_integrated_security()
    {
        var settings = WareProSettings.CreateDefault();
        settings.Database.Server = @"server\WAREPRO";
        settings.Database.Database = "Ware Pro Data";

        var result = new ConnectionStringFactory(new FakeSqlCredentialStore(), () => null).Resolve(settings);
        var parsed = new SqlConnectionStringBuilder(result);

        Assert.Equal(@"server\WAREPRO", parsed.DataSource);
        Assert.Equal("Ware Pro Data", parsed.InitialCatalog);
        Assert.True(parsed.IntegratedSecurity);
        Assert.True(parsed.TrustServerCertificate);
        Assert.Equal(string.Empty, parsed.UserID);
        Assert.Equal(string.Empty, parsed.Password);
    }

    [Fact]
    public void Sql_authentication_uses_the_builder_for_special_characters()
    {
        var settings = WareProSettings.CreateDefault();
        settings.Database.Authentication = DatabaseAuthentication.SqlPassword;
        var credential = CreateCredential("user;name", "p;ass=word}");

        var result = new ConnectionStringFactory(
            new FakeSqlCredentialStore(credential),
            () => "  ").Resolve(settings);
        var parsed = new SqlConnectionStringBuilder(result);

        Assert.False(parsed.IntegratedSecurity);
        Assert.Equal("user;name", parsed.UserID);
        Assert.Equal("p;ass=word}", parsed.Password);
    }

    [Fact]
    public void Sql_authentication_without_a_saved_credential_returns_a_stable_error_code()
    {
        var settings = WareProSettings.CreateDefault();
        settings.Database.Authentication = DatabaseAuthentication.SqlPassword;

        var error = Assert.Throws<WareProCredentialException>(() =>
            new ConnectionStringFactory(new FakeSqlCredentialStore(), () => null).Resolve(settings));

        Assert.Equal("CFG-CREDENTIAL-MISSING", error.Code);
        Assert.DoesNotContain("password", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Redactor_hides_connection_string_secrets_and_tokens()
    {
        const string value = "Server=db;User ID=admin;Password=p@ss;token=abc123;Database=WarePro";

        var redacted = SensitiveDataRedactor.Redact(value);

        Assert.DoesNotContain("admin", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("p@ss", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", redacted, StringComparison.Ordinal);
        Assert.Contains("User ID=***", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Password=***", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("token=***", redacted, StringComparison.OrdinalIgnoreCase);
    }

    private static SqlCredential CreateCredential(string userName, string password)
    {
        var securePassword = new SecureString();
        foreach (var character in password)
        {
            securePassword.AppendChar(character);
        }

        securePassword.MakeReadOnly();
        return new SqlCredential(userName, securePassword);
    }

    private sealed class FakeSqlCredentialStore(SqlCredential? credential = null) : ISqlCredentialStore
    {
        public int ReadCount { get; private set; }

        public SqlCredential? Read()
        {
            ReadCount++;
            return credential;
        }

        public void Write(SqlCredential value) => throw new NotSupportedException();

        public void Delete() => throw new NotSupportedException();
    }
}
