using QuanLyHangHoa.Models;
using QuanLyHangHoa.ViewModels;
using Xunit;
using System;

namespace QuanLyHangHoa.Tests.ViewModels;

public class StockReversalViewModelTests
{
    [Fact]
    public void ReverseDocumentPassesIdReasonAndCurrentUserToService()
    {
        string? docTypeUsed = null;
        int? docIdUsed = null;
        int? userIdUsed = null;

        var viewModel = new StockReversalViewModel(
            new AppUser { Id = 7, Username = "admin" },
            (docType, id, userId) =>
            {
                docTypeUsed = docType;
                docIdUsed = id;
                userIdUsed = userId;
                return 999;
            },
            (_, _) => { });
        
        viewModel.DocumentType = "StockOut";
        viewModel.DocumentIdText = "123";
        viewModel.Reason = "Mistake";

        viewModel.ReverseDocumentCommand.Execute(null);

        Assert.Equal("StockOut", docTypeUsed);
        Assert.Equal(123, docIdUsed);
        Assert.Equal(7, userIdUsed);
        Assert.Equal("Đã đảo chứng từ kho, adjustment #999.", viewModel.StatusMessage);
    }
}
