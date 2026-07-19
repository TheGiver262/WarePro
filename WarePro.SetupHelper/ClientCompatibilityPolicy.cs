namespace WarePro.SetupHelper;

public enum ClientCompatibilityStatus { Compatible, Rejected }
public sealed record ClientCompatibilityResult(ClientCompatibilityStatus Status, Version MinimumClientVersion);

public static class ClientCompatibilityPolicy
{
    public static ClientCompatibilityResult Evaluate(Version clientVersion, int schemaVersion)
    {
        // chỉ chấp nhận schema 6 và client từ 1.1.0 trở lên; thiếu một điều kiện thì từ chối.
        var minimum = new Version("1.1.0");
        return new ClientCompatibilityResult(
            schemaVersion == 6 && clientVersion >= minimum
                ? ClientCompatibilityStatus.Compatible
                : ClientCompatibilityStatus.Rejected,
            minimum);
    }
}