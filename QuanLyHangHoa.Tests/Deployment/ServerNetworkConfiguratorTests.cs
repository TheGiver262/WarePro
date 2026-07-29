using WarePro.SetupHelper;

namespace QuanLyHangHoa.Tests.Deployment;

public sealed class ServerNetworkConfiguratorTests
{
    [Fact]
    public async Task Configure_async_sets_a_static_TCP_port_restarts_SQL_and_scopes_firewall()
    {
        var runtime = new RecordingLanRuntime("MSSQL16.SQLEXPRESS");
        var configurator = new ServerNetworkConfigurator(runtime);

        await configurator.ConfigureAsync(
            new SqlLanOptions("SQLEXPRESS", 1433, "LocalSubnet"),
            CancellationToken.None);

        Assert.Equal(("MSSQL16.SQLEXPRESS", "", "1433"), runtime.TcpConfiguration);
        Assert.True(runtime.TcpProtocolEnabled);
        Assert.Equal(1433, runtime.VerifiedPort);
        Assert.Equal("MSSQL$SQLEXPRESS", runtime.VerifiedService);
        Assert.Equal("MSSQL$SQLEXPRESS", runtime.RestartedService);
        Assert.Equal(
            ["set-tcp", "enable-tcp", "restart", "verify-listener", "firewall"],
            runtime.Calls);
        Assert.Equal(
            ("WarePro SQL Server LAN", "TCP", 1433, "LocalSubnet"),
            runtime.FirewallRule);
    }

    [Fact]
    public async Task Configure_async_does_not_create_a_firewall_rule_when_listener_verification_fails()
    {
        var runtime = new RecordingLanRuntime("MSSQL16.SQLEXPRESS") { FailListenerVerification = true };
        var configurator = new ServerNetworkConfigurator(runtime);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => configurator.ConfigureAsync(
                new SqlLanOptions("SQLEXPRESS", 1433, "LocalSubnet"),
                CancellationToken.None));

        Assert.Null(runtime.FirewallRule);
    }

    [Fact]
    public void Windows_runtime_enables_TCP_and_verifies_the_listener()
    {
        var source = File.ReadAllText(Path.Combine(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")),
            "WarePro.SetupHelper",
            "ServerNetworkConfigurator.cs"));

        Assert.Contains(@"SetValue(""Enabled"", 1", source, StringComparison.Ordinal);
        Assert.Contains("Get-NetTCPConnection", source, StringComparison.Ordinal);
        Assert.Contains("Get-CimInstance Win32_Service", source, StringComparison.Ordinal);
        Assert.Contains("OwningProcess -eq $service.ProcessId", source, StringComparison.Ordinal);
        Assert.Contains("$attempt -lt 20", source, StringComparison.Ordinal);
        Assert.Contains("Start-Sleep", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1023)]
    [InlineData(65536)]
    public async Task Configure_async_rejects_an_unsafe_TCP_port(int port)
    {
        var configurator = new ServerNetworkConfigurator(new RecordingLanRuntime("MSSQL16.SQLEXPRESS"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => configurator.ConfigureAsync(
                new SqlLanOptions("SQLEXPRESS", port, "LocalSubnet"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Configure_async_rejects_a_firewall_scope_broader_than_LocalSubnet()
    {
        var configurator = new ServerNetworkConfigurator(new RecordingLanRuntime("MSSQL16.SQLEXPRESS"));

        await Assert.ThrowsAsync<ArgumentException>(
            () => configurator.ConfigureAsync(
                new SqlLanOptions("SQLEXPRESS", 1433, "Any"),
                CancellationToken.None));
    }

    private sealed class RecordingLanRuntime(string instanceId) : ISqlLanRuntime
    {
        public (string InstanceId, string DynamicPorts, string StaticPort)? TcpConfiguration { get; private set; }
        public string? RestartedService { get; private set; }
        public bool TcpProtocolEnabled { get; private set; }
        public int? VerifiedPort { get; private set; }
        public string? VerifiedService { get; private set; }
        public bool FailListenerVerification { get; init; }
        public List<string> Calls { get; } = [];
        public (string Name, string Protocol, int Port, string Scope)? FirewallRule { get; private set; }

        public Task<string> ResolveInstanceIdAsync(string instance, CancellationToken cancellationToken) =>
            Task.FromResult(instanceId);

        public Task SetTcpConfigurationAsync(
            string resolvedInstanceId,
            string dynamicPorts,
            string staticPort,
            CancellationToken cancellationToken)
        {
            TcpConfiguration = (resolvedInstanceId, dynamicPorts, staticPort);
            Calls.Add("set-tcp");
            return Task.CompletedTask;
        }

        public Task EnableTcpProtocolAsync(string resolvedInstanceId, CancellationToken cancellationToken)
        {
            TcpProtocolEnabled = true;
            Calls.Add("enable-tcp");
            return Task.CompletedTask;
        }

        public Task RestartServiceAsync(string serviceName, CancellationToken cancellationToken)
        {
            RestartedService = serviceName;
            Calls.Add("restart");
            return Task.CompletedTask;
        }

        public Task VerifyListenerAsync(int port, CancellationToken cancellationToken) =>
            VerifyListenerAsyncCore(null, port);

        public Task VerifyListenerAsync(
            string serviceName,
            int port,
            CancellationToken cancellationToken) =>
            VerifyListenerAsyncCore(serviceName, port);

        private Task VerifyListenerAsyncCore(string? serviceName, int port)
        {
            Calls.Add("verify-listener");
            if (FailListenerVerification)
            {
                throw new InvalidOperationException("listener was not started");
            }

            VerifiedService = serviceName;
            VerifiedPort = port;
            return Task.CompletedTask;
        }

        public Task ReplaceFirewallRuleAsync(
            string displayName,
            string protocol,
            int localPort,
            string remoteScope,
            CancellationToken cancellationToken)
        {
            FirewallRule = (displayName, protocol, localPort, remoteScope);
            Calls.Add("firewall");
            return Task.CompletedTask;
        }
    }
}
