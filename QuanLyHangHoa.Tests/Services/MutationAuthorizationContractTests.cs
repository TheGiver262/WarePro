using System.IO;

namespace QuanLyHangHoa.Tests.Services;

public class MutationAuthorizationContractTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    public static TheoryData<string, string, string> DirectGuardCases => new()
    {
        { "InvoiceService.cs", "public async Task<int> SaveSalesInvoiceAsync(", "PermissionAction.CreateSalesInvoice" },
        { "InvoiceService.cs", "public async Task<int> SavePurchaseInvoiceAsync(", "PermissionAction.CreatePurchaseInvoice" },
        { "StockInService.cs", "private async Task<(int Id, string DocumentCode)> StageSaveDraftAsync(", "PermissionAction.PostStockIn" },
        { "StockInService.cs", "private async Task StageSubmitForApprovalAsync(", "PermissionAction.PostStockIn" },
        { "StockInService.cs", "private Task StageApproveAsync(", "PermissionAction.PostStockIn" },
        { "StockInService.cs", "private Task StagePostAsync(", "PermissionAction.PostStockIn" },
        { "StockInService.cs", "private Task StageDeleteAsync(", "PermissionAction.PostStockIn" },
        { "StockOutService.cs", "private async Task<(int Id, string DocumentCode)> StageSaveDraftAsync(", "PermissionAction.PostStockOut" },
        { "StockOutService.cs", "private async Task StageSubmitForApprovalAsync(", "PermissionAction.PostStockOut" },
        { "StockOutService.cs", "private Task StageApproveAsync(", "PermissionAction.PostStockOut" },
        { "StockOutService.cs", "private Task StagePostAsync(", "PermissionAction.PostStockOut" },
        { "StockOutService.cs", "private Task StageDeleteAsync(", "PermissionAction.PostStockOut" },
        { "StockTransferService.cs", "private async Task<int> StageSaveDraftAsync(", "PermissionAction.PostStockAdjustment" },
        { "StockTransferService.cs", "private Task StageSubmitForApprovalAsync(", "PermissionAction.PostStockAdjustment" },
        { "StockTransferService.cs", "private Task StageApproveAsync(", "PermissionAction.PostStockAdjustment" },
        { "StockTransferService.cs", "private Task StagePostAsync(", "PermissionAction.PostStockAdjustment" },
        { "StockTransferService.cs", "private Task StageDeleteAsync(", "PermissionAction.PostStockAdjustment" },
        { "StockAdjustmentService.cs", "private async Task<(int Id, string DocumentCode)> StageSaveDraftAsync(", "PermissionAction.PostStockAdjustment" },
        { "StockAdjustmentService.cs", "private Task StageSubmitForApprovalAsync(", "PermissionAction.PostStockAdjustment" },
        { "StockAdjustmentService.cs", "private Task StageApproveAsync(", "PermissionAction.PostStockAdjustment" },
        { "StockAdjustmentService.cs", "private Task StagePostAsync(", "PermissionAction.PostStockAdjustment" },
        { "StockCountService.cs", "public async Task CreateAsync(", "PermissionAction.PostStockAdjustment" },
        { "StockCountService.cs", "private async Task StageProcessResultsAsync(", "PermissionAction.PostStockAdjustment" },
        { "StockCountService.Mutations.cs", "private Task StageSaveDraftLinesAsync(", "PermissionAction.PostStockAdjustment" },
        { "StockReversalService.cs", "private static async Task<int> StageReverseDocumentAsync(", "PermissionAction.PostStockAdjustment" },
        { "ProductSerialImportService.cs", "public async Task<(int SuccessCount, string Message)> ImportFromExcelAsync(", "PermissionAction.PostStockIn" }
    };

    [Theory]
    [MemberData(nameof(DirectGuardCases))]
    public void Mutation_method_revalidates_actor_with_exact_permission(
        string fileName,
        string methodMarker,
        string permission)
    {
        var method = ExtractMethod(ReadService(fileName), methodMarker);

        Assert.Contains("AuthorizationService.RequireFreshActor", method);
        Assert.Contains(permission, method);
    }

    [Theory]
    [InlineData("public Task UpdateDraftAsync(")]
    [InlineData("public Task CommitSessionAsync(")]
    public void Stock_count_public_edit_methods_forward_actor_to_guarded_helper(string marker)
    {
        var method = ExtractMethod(ReadService("StockCountService.Mutations.cs"), marker);

        Assert.Contains("StageSaveDraftLinesAsync(", method);
    }

    public static TheoryData<string, string, string> TransactionCases => new()
    {
        { "InvoiceService.cs", "public async Task<int> SaveSalesInvoiceAsync(", "PrepareSalesInvoice" },
        { "InvoiceService.cs", "public async Task<int> SavePurchaseInvoiceAsync(", "PreparePurchaseInvoice" },
        { "StockInService.cs", "private Task StagePostAsync(", "var stockIn =" },
        { "StockOutService.cs", "private Task StagePostAsync(", "var stockOut =" },
        { "StockTransferService.cs", "private Task StagePostAsync(", "var stockTransfer =" },
        { "StockAdjustmentService.cs", "private Task StagePostAsync(", "var adjustment =" },
        { "StockCountService.cs", "private async Task StageProcessResultsAsync(", "var session =" },
        { "StockCountService.Mutations.cs", "private Task StageSaveDraftLinesAsync(", "var session =" },
        { "StockReversalService.cs", "private static async Task<int> StageReverseDocumentAsync(", "db.StockAdjustments.Any" }
    };

    [Theory]
    [MemberData(nameof(TransactionCases))]
    public void Transactional_mutation_guards_actor_before_target_or_write(
        string fileName,
        string methodMarker,
        string targetMarker)
    {
        var method = ExtractMethod(ReadService(fileName), methodMarker);
        var transactionIndex = method.IndexOf("BeginTransaction", StringComparison.Ordinal);
        if (transactionIndex < 0)
        {
            transactionIndex = method.IndexOf("new DatabaseWriteRequest", StringComparison.Ordinal);
        }
        var guardIndex = method.IndexOf("AuthorizationService.RequireFreshActor", StringComparison.Ordinal);
        var targetIndex = method.IndexOf(targetMarker, StringComparison.Ordinal);

        Assert.True(guardIndex >= 0, $"{methodMarker} must authorize its actor.");
        if (!methodMarker.Contains("Stage", StringComparison.Ordinal))
        {
            Assert.True(transactionIndex >= 0, $"{methodMarker} must start an executor transaction.");
            Assert.True(guardIndex > transactionIndex, $"{methodMarker} must authorize inside its transaction.");
        }

        Assert.True(targetIndex > guardIndex, $"{methodMarker} must authorize before target access or writes.");
    }

    [Theory]
    [InlineData("StockInService.cs")]
    [InlineData("StockOutService.cs")]
    [InlineData("StockTransferService.cs")]
    public void Inventory_delete_is_async_and_has_no_public_sync_bypass(string fileName)
    {
        var source = ReadService(fileName);

        Assert.Contains("public Task DeleteAsync(", source);
        Assert.DoesNotContain("public virtual void Delete(", source);
    }
    public static TheoryData<string, string> ViewModelActorCases => new()
    {
        { "SalesInvoiceViewModel.cs", "await _invoiceService.SaveSalesInvoiceAsync(invoice, _currentUser.Id, operationId)" },
        { "PurchaseInvoiceViewModel.cs", "await _invoiceService.SavePurchaseInvoiceAsync(invoice, _currentUser.Id, operationId)" },
        { "StockInViewModel.cs", "await _stockInService.SaveDraftAsync(si, siLines, _currentUser.Id, operationId, cancellationToken)" },
        { "StockInViewModel.cs", "await _stockInService.SubmitForApprovalAsync(StockInId, _currentUser.Id, operationId, cancellationToken)" },
        { "StockInViewModel.cs", "await _stockInService.ApproveAsync(StockInId, _currentUser.Id, operationId, cancellationToken)" },
        { "StockInViewModel.cs", "await _stockInService.PostAsync(StockInId, _currentUser.Id, operationId, cancellationToken)" },
        { "StockOutViewModel.cs", "await _stockOutService.SaveDraftAsync(so, soLines, _currentUser.Id, operationId, cancellationToken)" },
        { "StockOutViewModel.cs", "await _stockOutService.SubmitForApprovalAsync(StockOutId, _currentUser.Id, operationId, cancellationToken)" },
        { "StockOutViewModel.cs", "await _stockOutService.ApproveAsync(StockOutId, _currentUser.Id, operationId, cancellationToken)" },
        { "StockOutViewModel.cs", "await _stockOutService.PostAsync(StockOutId, _currentUser.Id, operationId, cancellationToken)" },
        { "StockTransferViewModel.cs", "await _stockTransferService.SaveDraftAsync(st, stLines, _currentUser.Id, operationId, cancellationToken)" },
        { "StockTransferViewModel.cs", "await _stockTransferService.SubmitForApprovalAsync(StockTransferId, _currentUser.Id, operationId, cancellationToken)" },
        { "StockTransferViewModel.cs", "await _stockTransferService.ApproveAsync(StockTransferId, _currentUser.Id, operationId, cancellationToken)" },
        { "StockTransferViewModel.cs", "await _stockTransferService.PostAsync(StockTransferId, _currentUser.Id, operationId, cancellationToken)" },
        { "StockAdjustmentViewModel.cs", "await _adjustmentService.SaveDraftAsync(adj, lineModels, _currentUser.Id, operationId, cancellationToken)" },
        { "StockAdjustmentViewModel.cs", "await _adjustmentService.ApproveAsync(EditingId, _currentUser.Id, operationId, cancellationToken)" },
        { "StockAdjustmentViewModel.cs", "await _adjustmentService.PostAsync(EditingId, _currentUser.Id, operationId, cancellationToken)" },
        { "StockCountViewModel.cs", "await _stockCountService.CreateAsync(session, _currentUser.Id, operationId, cancellationToken)" },
        { "StockCountViewModel.cs", "await _stockCountService.UpdateDraftAsync(SelectedSession.Id, SelectedSessionLines, _currentUser.Id, operationId, cancellationToken)" },
        { "StockCountViewModel.cs", "await _stockCountService.CommitSessionAsync(currentId, SelectedSessionLines, _currentUser.Id, operationId, cancellationToken)" },
        { "StockCountViewModel.cs", "await _stockCountService.ProcessResultsAsync(session.Id, _currentUser.Id, operationId, cancellationToken)" },
        { "StockReversalViewModel.cs", "await _reverseDocument(DocumentType, DocumentIdText, Reason, _currentUser.Id, operationId, cancellationToken)" },
        { "ProductSerialViewModel.cs", "_importService.ImportFromExcelAsync(excelPath, _currentUser.Id, Guid.NewGuid())" }
    };

    [Theory]
    [MemberData(nameof(ViewModelActorCases))]
    public void Scoped_view_model_passes_explicit_actor(string fileName, string expectedCall)
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot,
            "QuanLyHangHoa",
            "ViewModels",
            fileName));

        var rowVersionAwareCall = expectedCall.Replace(
            ", _currentUser.Id",
            ", _editingRowVersion, _currentUser.Id",
            StringComparison.Ordinal);
        Assert.True(
            source.Contains(expectedCall, StringComparison.Ordinal) || source.Contains(rowVersionAwareCall, StringComparison.Ordinal),
            $"Expected an actor-scoped call matching '{expectedCall}' (with RowVersion when required).");
    }

    private static string ReadService(string fileName) => File.ReadAllText(Path.Combine(
        RepoRoot,
        "QuanLyHangHoa",
        "Services",
        fileName));

    private static string ExtractMethod(string source, string marker)
    {
        var markerIndex = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, $"Method marker not found: {marker}");
        var bodyStart = source.IndexOf('{', markerIndex);
        Assert.True(bodyStart >= 0, $"Method body not found: {marker}");
        var depth = 0;
        for (var index = bodyStart; index < source.Length; index++)
        {
            if (source[index] == '{') depth++;
            if (source[index] != '}') continue;
            depth--;
            if (depth == 0) return source[markerIndex..(index + 1)];
        }

        throw new InvalidOperationException($"Unclosed method body: {marker}");
    }
}
