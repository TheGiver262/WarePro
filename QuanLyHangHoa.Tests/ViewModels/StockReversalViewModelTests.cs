using QuanLyHangHoa.Models;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Tests.ViewModels
{
    public class StockReversalViewModelTests
    {
        [Fact]
        public void ReverseDocumentPassesGuidReasonAndCurrentUserToService()
        {
            var documentId = Guid.Parse("12345678-1234-1234-1234-123456789abc");
            Guid? reversedDocumentId = null;
            string? reason = null;
            int? reversedBy = null;

            var viewModel = new StockReversalViewModel(
                new Employee { Id = 9 },
                (id, inputReason, userId) =>
                {
                    reversedDocumentId = id;
                    reason = inputReason;
                    reversedBy = userId;
                    return 44;
                },
                (_, _) => { });
            viewModel.DocumentIdText = documentId.ToString();
            viewModel.Reason = "Wrong posting";

            viewModel.ReverseDocumentCommand.Execute(null);

            Assert.Equal(documentId, reversedDocumentId);
            Assert.Equal("Wrong posting", reason);
            Assert.Equal(9, reversedBy);
            Assert.Equal("Da dao chung tu kho, adjustment #44.", viewModel.StatusMessage);
        }

        [Fact]
        public void ReverseDocumentRejectsInvalidGuid()
        {
            var viewModel = new StockReversalViewModel(
                new Employee { Id = 9 },
                (_, _, _) => 44,
                (_, _) => { });
            viewModel.DocumentIdText = "bad-guid";
            viewModel.Reason = "Wrong posting";

            viewModel.ReverseDocumentCommand.Execute(null);

            Assert.Equal("DocumentId khong hop le.", viewModel.StatusMessage);
        }
    }
}
