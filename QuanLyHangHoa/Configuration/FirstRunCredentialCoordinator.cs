using Microsoft.Data.SqlClient;

namespace QuanLyHangHoa.Configuration;

public sealed class FirstRunCredentialCoordinator
{
    private readonly Func<WareProSettings?> _settingsLoader;
    private readonly ISqlCredentialStore _credentialStore;
    private readonly Func<string?> _environmentReader;

    public FirstRunCredentialCoordinator(
        Func<WareProSettings?> settingsLoader,
        ISqlCredentialStore credentialStore,
        Func<string?> environmentReader)
    {
        _settingsLoader = settingsLoader ?? throw new ArgumentNullException(nameof(settingsLoader));
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        _environmentReader = environmentReader ?? throw new ArgumentNullException(nameof(environmentReader));
    }

    public static FirstRunCredentialCoordinator CreateDefault() => new(
        () => new WareProSettingsStore().Load(),
        new SqlCredentialStore(),
        () => Environment.GetEnvironmentVariable("WAREPRO_CONNECTION_STRING"));

    public bool EnsureCredential(
        Func<SqlCredential?> credentialPrompt,
        bool replaceExisting = false)
    {
        ArgumentNullException.ThrowIfNull(credentialPrompt);

        if (!string.IsNullOrWhiteSpace(_environmentReader()))
        {
            return true;
        }

        var settings = _settingsLoader();
        if (settings is null || settings.Database.Authentication == DatabaseAuthentication.Windows)
        {
            return true;
        }

        if (!replaceExisting)
        {
            var existingCredential = _credentialStore.Read();
            if (existingCredential is not null)
            {
                existingCredential.Password.Dispose();
                return true;
            }
        }

        var enteredCredential = credentialPrompt();
        if (enteredCredential is null)
        {
            return false;
        }

        try
        {
            _credentialStore.Write(enteredCredential);
            return true;
        }
        finally
        {
            enteredCredential.Password.Dispose();
        }
    }
}
