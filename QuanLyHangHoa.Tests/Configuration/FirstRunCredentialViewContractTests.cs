namespace QuanLyHangHoa.Tests.Configuration;

public class FirstRunCredentialViewContractTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void Startup_opens_the_sql_credential_prompt_before_database_initialization()
    {
        var source = File.ReadAllText(Path.Combine(Root, "QuanLyHangHoa", "App.xaml.cs"));
        var promptIndex = source.IndexOf("FirstRunCredentialCoordinator.CreateDefault", StringComparison.Ordinal);
        var startupIndex = source.IndexOf("StartupCoordinator.CreateDefault", StringComparison.Ordinal);

        Assert.True(promptIndex >= 0, "App startup must create the first-run credential coordinator.");
        Assert.True(startupIndex > promptIndex, "Credential collection must happen before database startup.");
        Assert.Contains("EnsureCredential", source, StringComparison.Ordinal);
        Assert.Contains("SqlCredentialPromptView", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejected_sql_credential_returns_to_the_prompt_before_startup_fails()
    {
        var source = File.ReadAllText(Path.Combine(Root, "QuanLyHangHoa", "App.xaml.cs"));

        Assert.Contains("SQL-CREDENTIAL-REJECTED", source, StringComparison.Ordinal);
        Assert.Contains("replaceExisting: true", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Credential_view_uses_secure_password_input_and_plain_non_purple_styling()
    {
        var xaml = File.ReadAllText(Path.Combine(
            Root, "QuanLyHangHoa", "Views", "SqlCredentialPromptView.xaml"));
        var source = File.ReadAllText(Path.Combine(
            Root, "QuanLyHangHoa", "Views", "SqlCredentialPromptView.xaml.cs"));

        Assert.Contains("PasswordBox", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"UserNameInput\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PasswordInput\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Save_Click", xaml, StringComparison.Ordinal);
        Assert.Contains("Cancel_Click", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Purple", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Violet", xaml, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("SecurePassword.Copy()", source, StringComparison.Ordinal);
        Assert.Contains("new SqlCredential", source, StringComparison.Ordinal);
        Assert.Contains("DialogResult = true", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PasswordInput.Password", source, StringComparison.Ordinal);
    }
}
