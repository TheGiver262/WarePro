namespace QuanLyHangHoa.Tests.Deployment;

public sealed class DatabaseWriteBoundaryRegexRegressionTests
{
    [Fact]
    public void Roslyn_scanner_detects_real_invocations_ignores_comments_and_strings_and_excludes_base()
    {
        const string source = "database.SaveChanges(); db.SaveChanges /* comment */ (); // ignored.SaveChanges();\nvar text = \"ignored.SaveChanges()\"; base.SaveChangesAsync();";
        var calls = DatabaseWriteBoundaryContractTests.FindDirectWriteCalls(source);
        Assert.Equal(2, calls.Count);
        Assert.Contains(calls, call => call.ToString().Contains("/* comment */", StringComparison.Ordinal));
    }

    [Fact]
    public void Roslyn_scanner_detects_conditional_access_write_apis()
    {
        const string source = "class C { void Write(AppDbContext database) { database?.SaveChanges(); database?.SaveChangesAsync(); database?.Database?.BeginTransaction(); database?.Database?.BeginTransactionAsync(); } }";
        var calls = DatabaseWriteBoundaryContractTests.FindDirectWriteCalls(source);
        Assert.Equal(4, calls.Count);
        Assert.All(new[] { "SaveChanges", "SaveChangesAsync", "BeginTransaction", "BeginTransactionAsync" }, api => Assert.Contains(calls, call => call.ToString().Contains(api, StringComparison.Ordinal)));
    }
    [Fact]
    public void Direct_write_outside_executor_callback_is_rejected_for_an_allowlisted_file_shape()
    {
        const string source = "class C { async Task Write(AppDbContext db) { await db.SaveChangesAsync(); } }";
        var call = Assert.Single(DatabaseWriteBoundaryContractTests.FindDirectWriteCalls(source));
        Assert.False(DatabaseWriteBoundaryContractTests.IsExecutorCallback(call));
    }

    [Fact]
    public void Governed_helper_called_once_outside_executor_callback_is_rejected()
    {
        var sources = new[]
        {
            "class A { async Task Flush(AppDbContext db) { await db.SaveChangesAsync(); } }",
            "class B { async Task Caller(AppDbContext db) { await Flush(db); } }"
        };
        Assert.False(DatabaseWriteBoundaryContractTests.AreAllCallsitesExecutorWrapped("Flush", sources));
    }
    [Fact]
    public void Roslyn_scanner_detects_raw_dml_member_and_conditional_access()
    {
        const string source = "class C { async Task Write(DbCommand command) { command.ExecuteNonQuery(); await command.ExecuteNonQueryAsync(); command?.ExecuteNonQuery(); await command?.ExecuteNonQueryAsync(); } }";
        var calls = DatabaseWriteBoundaryContractTests.FindDirectWriteCalls(source);
        Assert.Equal(4, calls.Count);
        Assert.Equal(2, calls.Count(call => call.ToString().Contains("ExecuteNonQueryAsync", StringComparison.Ordinal)));
        Assert.Equal(2, calls.Count(call => call.ToString().Contains("ExecuteNonQuery", StringComparison.Ordinal) && !call.ToString().Contains("ExecuteNonQueryAsync", StringComparison.Ordinal)));
    }

    [Fact]
    public void Raw_dml_outside_allowlist_is_rejected()
    {
        Assert.False(DatabaseWriteBoundaryContractTests.IsAllowedDirectWrite(
            "QuanLyHangHoa/Services/UnexpectedWriter.cs", "Write", "ExecuteNonQuery"));
    }
}
