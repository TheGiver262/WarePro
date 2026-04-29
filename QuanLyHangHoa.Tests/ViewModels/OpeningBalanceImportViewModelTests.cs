using QuanLyHangHoa.Models;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Tests.ViewModels;

public class OpeningBalanceImportViewModelTests
{
    [Fact]
    public void ImportOpeningBalancePassesFilePathAndCurrentUserToImporter()
    {
        string? filePath = null;
        int? postedBy = null;
        var viewModel = new OpeningBalanceImportViewModel(
            postedByUserId: 7,
            (inputPath, inputUserId) =>
            {
                filePath = inputPath;
                postedBy = inputUserId;
                return new ImportResult<OpeningBalanceImportRow> { SuccessCount = 2 };
            },
            (_, _) => { });
        viewModel.FilePath = "opening.csv";

        viewModel.ImportOpeningBalanceCommand.Execute(null);

        Assert.Equal("opening.csv", filePath);
        Assert.Equal(7, postedBy);
        Assert.Equal("Da import 2 dong ton dau ky.", viewModel.StatusMessage);
    }

    [Fact]
    public void ImportOpeningBalanceRejectsMissingFilePath()
    {
        var called = false;
        var viewModel = new OpeningBalanceImportViewModel(
            postedByUserId: 7,
            (_, _) =>
            {
                called = true;
                return new ImportResult<OpeningBalanceImportRow>();
            },
            (_, _) => { });

        viewModel.ImportOpeningBalanceCommand.Execute(null);

        Assert.False(called);
        Assert.Equal("Chua chon file import.", viewModel.StatusMessage);
    }
}
