using System.Net;
using System.Security;
using Microsoft.Data.SqlClient;
using QuanLyHangHoa.Configuration;

namespace QuanLyHangHoa.Tests.Configuration;

public class FirstRunCredentialCoordinatorTests
{
    [Fact]
    public void Windows_authentication_does_not_open_the_prompt()
    {
        var settings = WareProSettings.CreateDefault();
        var promptCount = 0;
        var store = new RecordingCredentialStore();
        var coordinator = CreateCoordinator(settings, store);

        var result = coordinator.EnsureCredential(() =>
        {
            promptCount++;
            return CreateCredential("unused", "unused");
        });

        Assert.True(result);
        Assert.Equal(0, promptCount);
        Assert.Equal(0, store.ReadCount);
        Assert.Equal(0, store.WriteCount);
    }

    [Fact]
    public void Environment_connection_string_does_not_open_the_prompt()
    {
        var settings = WareProSettings.CreateDefault();
        settings.Database.Authentication = DatabaseAuthentication.SqlPassword;
        var promptCount = 0;
        var store = new RecordingCredentialStore();
        var coordinator = CreateCoordinator(settings, store, "Server=managed;Database=WarePro;Trusted_Connection=True");

        var result = coordinator.EnsureCredential(() =>
        {
            promptCount++;
            return null;
        });

        Assert.True(result);
        Assert.Equal(0, promptCount);
        Assert.Equal(0, store.ReadCount);
    }

    [Fact]
    public void Existing_sql_credential_does_not_open_the_prompt()
    {
        var settings = WareProSettings.CreateDefault();
        settings.Database.Authentication = DatabaseAuthentication.SqlPassword;
        var promptCount = 0;
        var store = new RecordingCredentialStore(CreateCredential("saved-user", "saved-password"));
        var coordinator = CreateCoordinator(settings, store);

        var result = coordinator.EnsureCredential(() =>
        {
            promptCount++;
            return null;
        });

        Assert.True(result);
        Assert.Equal(0, promptCount);
        Assert.Equal(1, store.ReadCount);
        Assert.Equal(0, store.WriteCount);
    }

    [Fact]
    public void Missing_sql_credential_is_saved_from_the_first_run_prompt()
    {
        var settings = WareProSettings.CreateDefault();
        settings.Database.Authentication = DatabaseAuthentication.SqlPassword;
        var store = new RecordingCredentialStore();
        var coordinator = CreateCoordinator(settings, store);

        var result = coordinator.EnsureCredential(() => CreateCredential("warepro-user", "secret-value"));

        Assert.True(result);
        Assert.Equal(1, store.ReadCount);
        Assert.Equal(1, store.WriteCount);
        Assert.Equal("warepro-user", store.WrittenUserName);
    }

    [Fact]
    public void Cancelling_the_first_run_prompt_stops_startup_without_writing()
    {
        var settings = WareProSettings.CreateDefault();
        settings.Database.Authentication = DatabaseAuthentication.SqlPassword;
        var store = new RecordingCredentialStore();
        var coordinator = CreateCoordinator(settings, store);

        var result = coordinator.EnsureCredential(() => null);

        Assert.False(result);
        Assert.Equal(1, store.ReadCount);
        Assert.Equal(0, store.WriteCount);
    }

    [Fact]
    public void Rejected_saved_credential_can_be_replaced_from_a_forced_prompt()
    {
        var settings = WareProSettings.CreateDefault();
        settings.Database.Authentication = DatabaseAuthentication.SqlPassword;
        var promptCount = 0;
        var store = new RecordingCredentialStore(CreateCredential("old-user", "old-password"));
        var coordinator = CreateCoordinator(settings, store);

        var result = coordinator.EnsureCredential(
            () =>
            {
                promptCount++;
                return CreateCredential("new-user", "new-password");
            },
            replaceExisting: true);

        Assert.True(result);
        Assert.Equal(1, promptCount);
        Assert.Equal(0, store.ReadCount);
        Assert.Equal(1, store.WriteCount);
        Assert.Equal("new-user", store.WrittenUserName);
    }

    private static FirstRunCredentialCoordinator CreateCoordinator(
        WareProSettings settings,
        ISqlCredentialStore store,
        string? environmentConnectionString = null) =>
        new(() => settings, store, () => environmentConnectionString);

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

    private sealed class RecordingCredentialStore(SqlCredential? saved = null) : ISqlCredentialStore
    {
        public int ReadCount { get; private set; }
        public int WriteCount { get; private set; }
        public string? WrittenUserName { get; private set; }

        public SqlCredential? Read()
        {
            ReadCount++;
            return saved;
        }

        public void Write(SqlCredential credential)
        {
            WriteCount++;
            WrittenUserName = credential.UserId;
        }

        public void Delete() => throw new NotSupportedException();
    }
}
