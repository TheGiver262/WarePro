namespace QuanLyHangHoa.Configuration;

public enum DatabaseAuthentication
{
    Windows,
    SqlPassword
}

public sealed class WareProDatabaseSettings
{
    public string Server { get; set; } = @".\SQLEXPRESS";
    public string Database { get; set; } = "ProductManagementDb";
    public DatabaseAuthentication Authentication { get; set; } = DatabaseAuthentication.Windows;
    public bool TrustServerCertificate { get; set; } = true;
}

public sealed class WareProUpdateSettings
{
    public string Repository { get; set; } = "TheGiver262/WarePro-Releases";
    public string Channel { get; set; } = "stable";
    public int CheckIntervalHours { get; set; } = 24;
}

public sealed class WareProSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public WareProDatabaseSettings Database { get; set; } = new();
    public WareProUpdateSettings Updates { get; set; } = new();

    public static WareProSettings CreateDefault() => new();
}
