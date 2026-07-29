using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace WarePro.SetupHelper;

public sealed record SqlLanOptions(string Instance, int Port, string Scope);

public interface ISqlLanRuntime
{
    Task<string> ResolveInstanceIdAsync(string instance, CancellationToken cancellationToken);
    Task SetTcpConfigurationAsync(
        string resolvedInstanceId,
        string dynamicPorts,
        string staticPort,
        CancellationToken cancellationToken);
    Task EnableTcpProtocolAsync(string resolvedInstanceId, CancellationToken cancellationToken);
    Task RestartServiceAsync(string serviceName, CancellationToken cancellationToken);
    Task VerifyListenerAsync(string serviceName, int port, CancellationToken cancellationToken);
    Task ReplaceFirewallRuleAsync(
        string displayName,
        string protocol,
        int localPort,
        string remoteScope,
        CancellationToken cancellationToken);
}

public interface IServerNetworkConfigurator
{
    Task ConfigureAsync(SqlLanOptions options, CancellationToken cancellationToken);
}

public sealed class ServerNetworkConfigurator : IServerNetworkConfigurator
{
    public const string FirewallRuleName = "WarePro SQL Server LAN";

    private readonly ISqlLanRuntime _runtime;

    public ServerNetworkConfigurator(ISqlLanRuntime runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public static ServerNetworkConfigurator CreateDefault() =>
        new(new WindowsSqlLanRuntime());

    public async Task ConfigureAsync(SqlLanOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        Validate(options);

        var instanceId = await _runtime
            .ResolveInstanceIdAsync(options.Instance, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            throw new InvalidOperationException("SQL Server instance ID was not found.");
        }

        await _runtime
            .SetTcpConfigurationAsync(instanceId, string.Empty, options.Port.ToString(), cancellationToken)
            .ConfigureAwait(false);
        await _runtime
            .EnableTcpProtocolAsync(instanceId, cancellationToken)
            .ConfigureAwait(false);
        await _runtime
            .RestartServiceAsync("MSSQL$" + options.Instance, cancellationToken)
            .ConfigureAwait(false);
        await _runtime
            .VerifyListenerAsync("MSSQL$" + options.Instance, options.Port, cancellationToken)
            .ConfigureAwait(false);
        await _runtime
            .ReplaceFirewallRuleAsync(
                FirewallRuleName,
                "TCP",
                options.Port,
                options.Scope,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static void Validate(SqlLanOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Instance)
            || !Regex.IsMatch(options.Instance, "^[A-Za-z0-9_]+$"))
        {
            throw new ArgumentException("SQL instance must contain only letters, numbers, and underscores.", nameof(options));
        }

        if (options.Port is < 1024 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "TCP port must be between 1024 and 65535.");
        }

        if (!string.Equals(options.Scope, "LocalSubnet", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only LocalSubnet firewall scope is allowed.", nameof(options));
        }
    }
}

internal sealed class WindowsSqlLanRuntime : ISqlLanRuntime
{
    public Task<string> ResolveInstanceIdAsync(string instance, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
            .OpenSubKey(@"SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL", writable: false);
        var instanceId = key?.GetValue(instance) as string;
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            throw new InvalidOperationException("SQL Server instance was not found.");
        }

        return Task.FromResult(instanceId);
    }

    public Task SetTcpConfigurationAsync(
        string resolvedInstanceId,
        string dynamicPorts,
        string staticPort,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
            .OpenSubKey(
                @"SOFTWARE\Microsoft\Microsoft SQL Server\"
                + resolvedInstanceId
                + @"\MSSQLServer\SuperSocketNetLib\Tcp\IPAll",
                writable: true);
        if (key is null)
        {
            throw new InvalidOperationException("SQL Server TCP registry key was not found.");
        }

        key.SetValue("TcpDynamicPorts", dynamicPorts, RegistryValueKind.String);
        key.SetValue("TcpPort", staticPort, RegistryValueKind.String);
        return Task.CompletedTask;
    }

    public Task EnableTcpProtocolAsync(string resolvedInstanceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
            .OpenSubKey(
                @"SOFTWARE\Microsoft\Microsoft SQL Server\"
                + resolvedInstanceId
                + @"\MSSQLServer\SuperSocketNetLib\Tcp",
                writable: true);
        if (key is null)
        {
            throw new InvalidOperationException("SQL Server TCP registry key was not found.");
        }

        key.SetValue("Enabled", 1, RegistryValueKind.DWord);
        return Task.CompletedTask;
    }

    public Task RestartServiceAsync(string serviceName, CancellationToken cancellationToken) =>
        RunPowerShellAsync(
            "Restart-Service -Name '" + serviceName + "' -Force -ErrorAction Stop",
            cancellationToken);

    public Task VerifyListenerAsync(
        string serviceName,
        int port,
        CancellationToken cancellationToken) =>
        RunPowerShellAsync(
            "$service = Get-CimInstance Win32_Service -Filter 'Name=''"
            + serviceName
            + "''' -ErrorAction Stop; "
            + "if ($service.ProcessId -le 0) { throw 'SQL Server service is not running.' }; "
            + "for ($attempt = 0; $attempt -lt 20; $attempt++) { "
            + "$listener = Get-NetTCPConnection -State Listen -LocalPort "
            + port
            + " -ErrorAction SilentlyContinue | Where-Object { $_.OwningProcess -eq $service.ProcessId } | Select-Object -First 1; "
            + "if ($null -ne $listener) { return }; "
            + "Start-Sleep -Milliseconds 500 }; "
            + "throw 'SQL Server is not listening on the configured TCP port.'",
            cancellationToken);

    public Task ReplaceFirewallRuleAsync(
        string displayName,
        string protocol,
        int localPort,
        string remoteScope,
        CancellationToken cancellationToken) =>
        RunPowerShellAsync(
            "$existing = Get-NetFirewallRule -DisplayName '"
            + displayName
            + "' -ErrorAction SilentlyContinue; "
            + "if ($null -ne $existing) { $existing | Remove-NetFirewallRule }; "
            + "New-NetFirewallRule -DisplayName '"
            + displayName
            + "' -Direction Inbound -Action Allow -Protocol "
            + protocol
            + " -LocalPort "
            + localPort
            + " -RemoteAddress "
            + remoteScope
            + " -Profile Any | Out-Null",
            cancellationToken);

    private static async Task RunPowerShellAsync(string command, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("powershell.exe")
            {
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            }
        };
        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-NonInteractive");
        process.StartInfo.ArgumentList.Add("-Command");
        process.StartInfo.ArgumentList.Add(command);

        process.Start();
        var standardError = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Windows network configuration command failed: " + standardError.Trim());
        }
    }
}