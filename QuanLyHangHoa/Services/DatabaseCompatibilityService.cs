using System;

namespace QuanLyHangHoa.Services;

public enum DatabaseCompatibilityStatus
{
    Compatible,
    UpgradeRequired,
    ClientUpdateRequired
}

public sealed record DatabaseCompatibilityResult(
    DatabaseCompatibilityStatus Status,
    int DatabaseSchemaVersion,
    int CurrentSchemaVersion);

public sealed class DatabaseCompatibilityException : Exception
{
    public DatabaseCompatibilityException(
        int databaseSchemaVersion,
        string? minimumClientVersion = null,
        string? currentClientVersion = null)
        : base($"DB-CLIENT-UPDATE-REQUIRED: Database schema {databaseSchemaVersion} requires a newer WarePro version." +
            $" Minimum client: {minimumClientVersion ?? "unknown"}; current client: {currentClientVersion ?? "unknown"}.")
    {
        Code = "DB-CLIENT-UPDATE-REQUIRED";
        DatabaseSchemaVersion = databaseSchemaVersion;
        MinimumClientVersion = minimumClientVersion;
        CurrentClientVersion = currentClientVersion;
    }

    public string Code { get; }
    public int DatabaseSchemaVersion { get; }
    public string? MinimumClientVersion { get; }
    public string? CurrentClientVersion { get; }
}

public sealed class DatabaseCompatibilityService
{
    public const int CurrentSchemaVersion = 5;
    public const int MinimumSupportedSchemaVersion = 5;
    public const int MaximumSupportedSchemaVersion = 5;

    public DatabaseCompatibilityResult Evaluate(int databaseSchemaVersion)
    {
        if (databaseSchemaVersion < MinimumSupportedSchemaVersion)
        {
            return new DatabaseCompatibilityResult(
                DatabaseCompatibilityStatus.UpgradeRequired,
                databaseSchemaVersion,
                CurrentSchemaVersion);
        }

        if (databaseSchemaVersion > MaximumSupportedSchemaVersion)
        {
            return new DatabaseCompatibilityResult(
                DatabaseCompatibilityStatus.ClientUpdateRequired,
                databaseSchemaVersion,
                CurrentSchemaVersion);
        }

        return new DatabaseCompatibilityResult(
            DatabaseCompatibilityStatus.Compatible,
            databaseSchemaVersion,
            CurrentSchemaVersion);
    }

    public DatabaseCompatibilityResult Evaluate(
        int databaseSchemaVersion,
        string? minimumClientVersion,
        string? currentClientVersion)
    {
        var schemaResult = Evaluate(databaseSchemaVersion);
        if (schemaResult.Status == DatabaseCompatibilityStatus.ClientUpdateRequired)
        {
            return schemaResult;
        }

        var minimum = ParseVersion(minimumClientVersion);
        var current = ParseVersion(currentClientVersion);
        if (minimum is null || current is null || current < minimum)
        {
            return schemaResult with { Status = DatabaseCompatibilityStatus.ClientUpdateRequired };
        }

        return schemaResult;
    }

    public bool RequiresBackup(int databaseSchemaVersion, bool hasExistingBusinessData) =>
        hasExistingBusinessData && databaseSchemaVersion < CurrentSchemaVersion;

    private static Version? ParseVersion(string? value)
    {
        var numericPart = value?.Split(['-', '+'], 2)[0];
        return Version.TryParse(numericPart, out var parsed) ? parsed : null;
    }
}
