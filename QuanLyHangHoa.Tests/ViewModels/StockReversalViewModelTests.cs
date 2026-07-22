using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.ViewModels;
using Xunit;
using System;
using System.Threading.Tasks;

namespace QuanLyHangHoa.Tests.ViewModels;

public class StockReversalViewModelTests
{
    [Fact]
    public async Task Conflict_resets_form_before_showing_safe_message()
    {
        string? shownMessage = null;
        var viewModel = new StockReversalViewModel(
            new AppUser { Id = 7, Username = "admin" },
            (_, _, _, _, _, _) => Task.FromException<int>(
                new DatabaseWriteConflictException(Guid.NewGuid(), new Exception("provider detail"))),
            (message, _) => shownMessage = message)
        {
            DocumentIdText = "123",
            Reason = "Nhập nhầm"
        };

        await viewModel.ReverseDocumentCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.DocumentIdText);
        Assert.Equal("WrongPosting", viewModel.Reason);
        Assert.Equal(DatabaseWriteUi.ConflictMessage, shownMessage);
    }
    [Fact]
    public async Task ReverseDocumentPassesCodeReasonAndCurrentUserToService()
    {
        string? docTypeUsed = null;
        string? documentReferenceUsed = null;
        string? reasonUsed = null;
        int? userIdUsed = null;

        var viewModel = new StockReversalViewModel(
            new AppUser { Id = 7, Username = "admin" },
            (docType, documentReference, reason, userId, _, _) =>
            {
                docTypeUsed = docType;
                documentReferenceUsed = documentReference;
                reasonUsed = reason;
                userIdUsed = userId;
                return Task.FromResult(999);
            },
            (_, _) => { });
        
        viewModel.DocumentType = "StockOut";
        viewModel.DocumentIdText = "PXK-2026-001";
        viewModel.Reason = "Xuất nhầm chứng từ";

        await viewModel.ReverseDocumentCommand.ExecuteAsync(null);

        Assert.Equal("StockOut", docTypeUsed);
        Assert.Equal("PXK-2026-001", documentReferenceUsed);
        Assert.Equal("Xuất nhầm chứng từ", reasonUsed);
        Assert.Equal(7, userIdUsed);
        Assert.Equal("Đã đảo chứng từ kho, adjustment #999.", viewModel.StatusMessage);
    }
}
