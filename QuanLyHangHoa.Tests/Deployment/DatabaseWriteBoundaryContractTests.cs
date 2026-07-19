using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace QuanLyHangHoa.Tests.Deployment;

public sealed class DatabaseWriteBoundaryContractTests
{
    private const string UnitOfWorkFile = "QuanLyHangHoa/Inventory/EfInventoryUnitOfWork.cs";
    private static readonly string Root = FindRoot();
    private static readonly HashSet<string> ProductionProjects = ["QuanLyHangHoa/QuanLyHangHoa.csproj", "WarePro.Core/WarePro.Core.csproj", "WarePro.SetupHelper/WarePro.SetupHelper.csproj"];
    private static readonly HashSet<string> WriteApis = ["SaveChanges", "SaveChangesAsync", "BeginTransaction", "BeginTransactionAsync", "ExecuteNonQuery", "ExecuteNonQueryAsync"];
    private static readonly HashSet<string> RawDmlApis = ["ExecuteNonQuery", "ExecuteNonQueryAsync"];
    private static readonly IReadOnlyDictionary<CallKey, int> ExpectedCalls = new Dictionary<CallKey, int>
    {
        [new("QuanLyHangHoa/Data/DatabaseWriteExecutor.cs", "ExecuteAttemptAsync", "BeginTransactionAsync")] = 1, [new("QuanLyHangHoa/Data/DatabaseWriteExecutor.cs", "ExecuteAttemptAsync", "SaveChangesAsync")] = 1,
        [new(UnitOfWorkFile, "Commit", "SaveChanges")] = 1,
        [new("QuanLyHangHoa/Services/AppUserService.cs", "AddUserAsync", "SaveChangesAsync")] = 1, [new("QuanLyHangHoa/Services/BrandService.cs", "AddAsync", "SaveChangesAsync")] = 1, [new("QuanLyHangHoa/Services/CategoryService.cs", "AddAsync", "SaveChangesAsync")] = 1, [new("QuanLyHangHoa/Services/CustomerService.cs", "AddAsync", "SaveChangesAsync")] = 1,
        [new("QuanLyHangHoa/Services/DataImport/DatabaseSeeder.cs", "SeedProductUnitsAsync", "SaveChangesAsync")] = 1, [new("QuanLyHangHoa/Services/DataImport/DatabaseSeeder.cs", "SeedTableWithMappingAsync", "SaveChangesAsync")] = 1, [new("QuanLyHangHoa/Services/DataImport/DatabaseSeeder.cs", "SeedWorkbookAsync", "SaveChangesAsync")] = 6,
        [new("QuanLyHangHoa/Services/DataImport/DynamicImportService.cs", "ImportProductsAsync", "SaveChangesAsync")] = 3, [new("QuanLyHangHoa/Services/DataImport/DynamicImportService.cs", "ImportPurchaseInvoicesAsync", "SaveChangesAsync")] = 2, [new("QuanLyHangHoa/Services/DataImport/DynamicImportService.cs", "ImportSalesInvoicesAsync", "SaveChangesAsync")] = 2, [new("QuanLyHangHoa/Services/DataImport/DynamicImportService.cs", "ImportStockInDocumentsAsync", "SaveChangesAsync")] = 2, [new("QuanLyHangHoa/Services/DataImport/DynamicImportService.cs", "ImportStockOutDocumentsAsync", "SaveChangesAsync")] = 2,
        [new("QuanLyHangHoa/Services/InvoiceService.Integrity.cs", "UpsertPurchaseInvoiceAsync", "SaveChangesAsync")] = 1, [new("QuanLyHangHoa/Services/InvoiceService.Integrity.cs", "UpsertSalesInvoiceAsync", "SaveChangesAsync")] = 1,
        [new("QuanLyHangHoa/Services/OpeningBalanceImportService.cs", "ImportRowsAsync", "SaveChangesAsync")] = 1, [new("QuanLyHangHoa/Services/ProductService.cs", "AddProductAsync", "SaveChangesAsync")] = 1, [new("QuanLyHangHoa/Services/StockAdjustmentService.cs", "StageSaveDraftAsync", "SaveChangesAsync")] = 1,
        [new("QuanLyHangHoa/Services/StockCountService.cs", "CreateAsync", "SaveChangesAsync")] = 1, [new("QuanLyHangHoa/Services/StockCountService.cs", "PostStockInCorrectionAsync", "SaveChangesAsync")] = 1, [new("QuanLyHangHoa/Services/StockCountService.cs", "PostStockOutCorrectionAsync", "SaveChangesAsync")] = 1, [new("QuanLyHangHoa/Services/StockCountService.cs", "StageProcessResultsAsync", "SaveChangesAsync")] = 1,
        [new("QuanLyHangHoa/Services/StockInService.cs", "StageSaveDraftAsync", "SaveChangesAsync")] = 1, [new("QuanLyHangHoa/Services/StockOutService.cs", "StageSaveDraftAsync", "SaveChangesAsync")] = 1, [new("QuanLyHangHoa/Services/StockReversalService.cs", "StageReverseDocumentAsync", "SaveChangesAsync")] = 1, [new("QuanLyHangHoa/Services/StockTransferService.cs", "StageSaveDraftAsync", "SaveChangesAsync")] = 1,
        [new("QuanLyHangHoa/Services/SupplierService.cs", "AddAsync", "SaveChangesAsync")] = 1, [new("QuanLyHangHoa/Services/UnitService.cs", "AddAsync", "SaveChangesAsync")] = 1,
        [new("QuanLyHangHoa/Services/WarrantyClaimService.Writes.cs", "CreateClaimAsync", "SaveChangesAsync")] = 1, [new("QuanLyHangHoa/Services/WarrantyClaimService.Writes.cs", "ReceiveFromManufacturerReplacedAsync", "SaveChangesAsync")] = 2, [new("QuanLyHangHoa/Services/WarrantyClaimService.Writes.cs", "ReplaceSerialAsync", "SaveChangesAsync")] = 1,
        [new("WarePro.SetupHelper/SetupCommands.cs", "FinalizeAsync", "BeginTransactionAsync")] = 1, [new("WarePro.SetupHelper/SetupCommands.cs", "PrepareAsync", "BeginTransactionAsync")] = 1,
        [new("WarePro.SetupHelper/SetupCommands.cs", "OpenConnectionWithCreationAsync", "ExecuteNonQueryAsync")] = 1, [new("WarePro.SetupHelper/SetupCommands.cs", "ExecuteAsync", "ExecuteNonQueryAsync")] = 2,
        [new("QuanLyHangHoa/Services/ClientSessionLease.cs", "ExecuteAsync", "ExecuteNonQueryAsync")] = 1, [new("QuanLyHangHoa/Services/DatabaseBackupService.cs", "BackupWithChecksum", "ExecuteNonQuery")] = 1, [new("QuanLyHangHoa/Services/DatabaseBackupService.cs", "VerifyWithChecksum", "ExecuteNonQuery")] = 1, [new("QuanLyHangHoa/Services/SchemaUpgradeLock.cs", "Dispose", "ExecuteNonQuery")] = 1
    };

    [Fact]
    public void Direct_write_apis_match_exact_method_api_counts_and_governed_paths()
    {
        var sources = EnumerateProductionSourceFiles().Select(file => new SourceFile(Relative(file), File.ReadAllText(file))).ToArray();
        var calls = sources.SelectMany(source => FindDirectWriteCalls(source.Text).Select(call => new SourceCall(source.Path, ContainingMethod(call), Api(call), call))).ToArray();
        Assert.Equal(54, calls.Length);
        Assert.Equal(53, calls.Count(call => call.File != UnitOfWorkFile));
        var actual = calls.GroupBy(call => new CallKey(call.File, call.Method, call.Api)).ToDictionary(group => group.Key, group => group.Count());
        Assert.Equal(ExpectedCalls.OrderBy(item => item.Key.ToString()), actual.OrderBy(item => item.Key.ToString()));
        foreach (var call in calls.Where(call => IsServiceFile(call.File) && call.File != UnitOfWorkFile && !RawDmlApis.Contains(call.Api)))
            Assert.True(IsExecutorCallback(call.Invocation) || AreAllCallsitesExecutorWrapped(call.Method, sources.Select(source => source.Text)), $"{call.File}|{call.Method}|{call.Api} is not executor governed.");
    }

    [Fact]
    public void Concurrency_document_covers_exact_files_and_operational_contract()
    {
        var document = File.ReadAllText(Path.Combine(Root, "docs", "DATABASE_CONCURRENCY.md"));
        foreach (var required in ExpectedCalls.Keys.Select(key => key.File).Distinct().Concat(new[] { "retry", "conflict", "operation ID", "maintenance", "backup", "DB-WRITE-CONFLICT", "DB-WRITE-RETRY-EXHAUSTED", "rowversion", "deadlock", "commit acknowledgement", "RCSI", "schema 6", "Plan 2", "approval" })) Assert.Contains(required, document, StringComparison.OrdinalIgnoreCase);
    }

    internal static IReadOnlyList<InvocationExpressionSyntax> FindDirectWriteCalls(string source) => CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Where(invocation => invocation.Expression switch { MemberAccessExpressionSyntax member => member.Expression is not BaseExpressionSyntax && WriteApis.Contains(member.Name.Identifier.ValueText), MemberBindingExpressionSyntax binding => WriteApis.Contains(binding.Name.Identifier.ValueText), _ => false }).ToArray();
    internal static bool IsAllowedDirectWrite(string file, string method, string api) =>
        ExpectedCalls.ContainsKey(new CallKey(file, method, api));
    internal static bool IsExecutorCallback(InvocationExpressionSyntax call) => call.Ancestors().OfType<AnonymousFunctionExpressionSyntax>().Any(lambda => lambda.Ancestors().OfType<InvocationExpressionSyntax>().Any(execution => execution.Expression is MemberAccessExpressionSyntax member && member.Name.Identifier.ValueText == "ExecuteAsync" && member.Expression.ToString() is "_writeExecutor" or "WriteExecutor"));
    internal static bool AreAllCallsitesExecutorWrapped(string method, IEnumerable<string> sources)
    {
        var roots = sources.Select(source => CSharpSyntaxTree.ParseText(source).GetRoot()).ToArray();
        return IsGovernedMethod(method, roots, []);
    }
    private static bool IsGovernedMethod(string method, IReadOnlyList<Microsoft.CodeAnalysis.SyntaxNode> roots, HashSet<string> visiting)
    {
        if (!visiting.Add(method)) return false;
        var callers = roots.SelectMany(root => root.DescendantNodes().OfType<InvocationExpressionSyntax>()).Where(call => InvokedName(call) == method).ToArray();
        return callers.Length > 0 && callers.All(call => IsExecutorCallback(call) || IsGovernedMethod(ContainingMethod(call), roots, new HashSet<string>(visiting)));
    }
    private static string? InvokedName(InvocationExpressionSyntax call) => call.Expression switch { IdentifierNameSyntax identifier => identifier.Identifier.ValueText, GenericNameSyntax generic => generic.Identifier.ValueText, MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText, MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText, _ => null };
    private static string ContainingMethod(InvocationExpressionSyntax call) => call.Ancestors().OfType<MethodDeclarationSyntax>().First().Identifier.ValueText;
    private static string Api(InvocationExpressionSyntax call) => call.Expression switch { MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText, MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText, _ => throw new InvalidOperationException("Invocation is not a direct write API call.") };
    private static bool IsServiceFile(string path) => path.StartsWith("QuanLyHangHoa/Services/", StringComparison.Ordinal);
    private static IEnumerable<string> EnumerateProductionSourceFiles() => ProductionProjects.Select(project => Path.Combine(Root, project.Replace('/', Path.DirectorySeparatorChar))).Select(Path.GetDirectoryName).SelectMany(directory => Directory.EnumerateFiles(directory!, "*.cs", SearchOption.AllDirectories)).Where(file => !IsGenerated(file));
    private static bool IsGenerated(string path) => Path.GetRelativePath(Root, path).Split(Path.DirectorySeparatorChar).Any(segment => segment is "bin" or "obj" or ".git" or ".worktrees");
    private static string Relative(string file) => Path.GetRelativePath(Root, file).Replace(Path.DirectorySeparatorChar, '/');
    private static string FindRoot() { for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) if (File.Exists(Path.Combine(directory.FullName, "QuanLyHangHoa", "QuanLyHangHoa.csproj"))) return directory.FullName; throw new DirectoryNotFoundException("Cannot locate the WarePro repository root."); }
    private sealed record SourceFile(string Path, string Text);
    private sealed record SourceCall(string File, string Method, string Api, InvocationExpressionSyntax Invocation);
    private sealed record CallKey(string File, string Method, string Api);
}
