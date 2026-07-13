using System.IO;

namespace QuanLyHangHoa.Tests.Services;

public class MutationAuthorizationContractTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    public static TheoryData<string, string, string> DirectGuardCases => new()
    {
        { "InvoiceService.cs", "public void SaveSalesInvoice(", "PermissionAction.CreateSalesInvoice" },
        { "InvoiceService.cs", "public void SavePurchaseInvoice(", "PermissionAction.CreatePurchaseInvoice" },
        { "StockInService.cs", "public virtual void SaveDraft(", "PermissionAction.PostStockIn" },
        { "StockInService.cs", "public virtual void SubmitForApproval(", "PermissionAction.PostStockIn" },
        { "StockInService.cs", "public virtual void Approve(", "PermissionAction.PostStockIn" },
        { "StockInService.cs", "public virtual void Post(", "PermissionAction.PostStockIn" },
        { "StockInService.cs", "public virtual void Delete(", "PermissionAction.PostStockIn" },
        { "StockOutService.cs", "public void SaveDraft(", "PermissionAction.PostStockOut" },
        { "StockOutService.cs", "public virtual void SubmitForApproval(", "PermissionAction.PostStockOut" },
        { "StockOutService.cs", "public virtual void Approve(", "PermissionAction.PostStockOut" },
        { "StockOutService.cs", "public void Post(", "PermissionAction.PostStockOut" },
        { "StockOutService.cs", "public virtual void Delete(", "PermissionAction.PostStockOut" },
        { "StockTransferService.cs", "public virtual void SaveDraft(", "PermissionAction.PostStockAdjustment" },
        { "StockTransferService.cs", "public virtual void SubmitForApproval(", "PermissionAction.PostStockAdjustment" },
        { "StockTransferService.cs", "public virtual void Approve(", "PermissionAction.PostStockAdjustment" },
        { "StockTransferService.cs", "public virtual void Post(", "PermissionAction.PostStockAdjustment" },
        { "StockTransferService.cs", "public virtual void Delete(", "PermissionAction.PostStockAdjustment" },
        { "StockAdjustmentService.cs", "public virtual void SaveDraft(", "PermissionAction.PostStockAdjustment" },
        { "StockAdjustmentService.cs", "public virtual void SubmitForApproval(", "PermissionAction.PostStockAdjustment" },
        { "StockAdjustmentService.cs", "public virtual void Approve(", "PermissionAction.PostStockAdjustment" },
        { "StockAdjustmentService.cs", "public void Post(", "PermissionAction.PostStockAdjustment" },
        { "StockCountService.cs", "public void CreateSession(", "PermissionAction.PostStockAdjustment" },
        { "StockCountService.cs", "public void ProcessResults(", "PermissionAction.PostStockAdjustment" },
        { "StockCountService.Mutations.cs", "private void SaveDraftLines(", "PermissionAction.PostStockAdjustment" },
        { "StockReversalService.cs", "public int ReverseDocument(", "PermissionAction.PostStockAdjustment" },
        { "ProductSerialImportService.cs", "public async Task<(int SuccessCount, string Message)> ImportFromExcelAsync(", "PermissionAction.ManageMasterData" }
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
    [InlineData("public void UpdateDraft(")]
    [InlineData("public void CommitSession(")]
    public void Stock_count_public_edit_methods_forward_actor_to_guarded_helper(string marker)
    {
        var method = ExtractMethod(ReadService("StockCountService.Mutations.cs"), marker);

        Assert.Contains("SaveDraftLines(sessionId, lines, userId", method);
    }

    public static TheoryData<string, string, string> TransactionCases => new()
    {
        { "InvoiceService.cs", "public void SaveSalesInvoice(", "PrepareSalesInvoice" },
        { "InvoiceService.cs", "public void SavePurchaseInvoice(", "PreparePurchaseInvoice" },
        { "StockInService.cs", "public virtual void Post(", "var stockIn =" },
        { "StockOutService.cs", "public void Post(", "var stockOut =" },
        { "StockTransferService.cs", "public virtual void Post(", "var stockTransfer =" },
        { "StockAdjustmentService.cs", "public void Post(", "var adjustment =" },
        { "StockCountService.cs", "public void ProcessResults(", "var session =" },
        { "StockCountService.Mutations.cs", "private void SaveDraftLines(", "var session =" },
        { "StockReversalService.cs", "public int ReverseDocument(", "db.StockAdjustments.Any" }
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
        var guardIndex = method.IndexOf("AuthorizationService.RequireFreshActor", StringComparison.Ordinal);
        var targetIndex = method.IndexOf(targetMarker, StringComparison.Ordinal);

        Assert.True(transactionIndex >= 0, $"{methodMarker} must start a transaction.");
        Assert.True(guardIndex > transactionIndex, $"{methodMarker} must authorize inside its transaction.");
        Assert.True(targetIndex > guardIndex, $"{methodMarker} must authorize before target access or writes.");
    }

    public static TheoryData<string, string> ViewModelActorCases => new()
    {
        { "SalesInvoiceViewModel.cs", "_invoiceService.SaveSalesInvoice(invoice, _currentUser.Id)" },
        { "PurchaseInvoiceViewModel.cs", "_invoiceService.SavePurchaseInvoice(invoice, _currentUser.Id)" },
        { "StockInViewModel.cs", "_stockInService.SaveDraft(si, siLines, _currentUser.Id)" },
        { "StockInViewModel.cs", "_stockInService.SubmitForApproval(StockInId, _currentUser.Id)" },
        { "StockInViewModel.cs", "_stockInService.Approve(StockInId, _currentUser.Id)" },
        { "StockInViewModel.cs", "_stockInService.Post(StockInId, _currentUser.Id)" },
        { "StockOutViewModel.cs", "_stockOutService.SaveDraft(so, soLines, _currentUser.Id)" },
        { "StockOutViewModel.cs", "_stockOutService.SubmitForApproval(StockOutId, _currentUser.Id)" },
        { "StockOutViewModel.cs", "_stockOutService.Approve(StockOutId, _currentUser.Id)" },
        { "StockOutViewModel.cs", "_stockOutService.Post(StockOutId, _currentUser.Id)" },
        { "StockTransferViewModel.cs", "_stockTransferService.SaveDraft(st, stLines, _currentUser.Id)" },
        { "StockTransferViewModel.cs", "_stockTransferService.SubmitForApproval(StockTransferId, _currentUser.Id)" },
        { "StockTransferViewModel.cs", "_stockTransferService.Approve(StockTransferId, _currentUser.Id)" },
        { "StockTransferViewModel.cs", "_stockTransferService.Post(StockTransferId, _currentUser.Id)" },
        { "StockAdjustmentViewModel.cs", "_adjustmentService.SaveDraft(adj, lineModels, _currentUser.Id)" },
        { "StockAdjustmentViewModel.cs", "_adjustmentService.Approve(EditingId, _currentUser.Id)" },
        { "StockAdjustmentViewModel.cs", "_adjustmentService.Post(EditingId, _currentUser.Id)" },
        { "StockCountViewModel.cs", "_stockCountService.CreateSession(session, _currentUser.Id)" },
        { "StockCountViewModel.cs", "_stockCountService.UpdateDraft(SelectedSession.Id, SelectedSessionLines, _currentUser.Id)" },
        { "StockCountViewModel.cs", "_stockCountService.CommitSession(currentId, SelectedSessionLines, _currentUser.Id)" },
        { "StockCountViewModel.cs", "_stockCountService.ProcessResults(session.Id, _currentUser.Id)" },
        { "StockReversalViewModel.cs", "_reverseDocument(DocumentType, documentId, _currentUser.Id)" },
        { "ProductSerialViewModel.cs", "_importService.ImportFromExcelAsync(excelPath, _currentUser.Id)" }
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

        Assert.Contains(expectedCall, source);
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
