using System;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.ViewModels;
using Xunit;

namespace QuanLyHangHoa.Tests.ViewModels;

public class OpeningBalanceImportViewModelTests
{
    private static AppDbContext CreateMockContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        
        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public void ResetWizard_ClearsAllWizardProperties()
    {
        Func<AppDbContext> contextFactory = () => CreateMockContext();
        var viewModel = new OpeningBalanceImportViewModel(7, contextFactory, (msg, title) => { });

        viewModel.FilePath = "some_file.xlsx";
        viewModel.ActiveStep = 3;
        viewModel.SuccessCount = 10;
        viewModel.StatusMessage = "In Progress";

        viewModel.ResetWizardCommand.Execute(null);

        Assert.Equal(string.Empty, viewModel.FilePath);
        Assert.Equal(1, viewModel.ActiveStep);
        Assert.Equal(0, viewModel.SuccessCount);
        Assert.Equal(string.Empty, viewModel.StatusMessage);
        Assert.Empty(viewModel.ColumnMappings);
        Assert.Null(viewModel.PreviewData);
    }

    [Fact]
    public void StepNavigation_MovesForwardAndBackwardCorrectly()
    {
        Func<AppDbContext> contextFactory = () => CreateMockContext();
        var viewModel = new OpeningBalanceImportViewModel(7, contextFactory, (msg, title) => { });

        // Starts at step 1
        Assert.Equal(1, viewModel.ActiveStep);

        // Advance to step 2 manually since BrowseFile has interactive dialogs
        viewModel.ActiveStep = 2;

        // Move to step 3 (Preview)
        viewModel.NextToPreviewCommand.Execute(null);
        Assert.Equal(3, viewModel.ActiveStep);

        // Move back to mapping (Step 2)
        viewModel.BackToMappingCommand.Execute(null);
        Assert.Equal(2, viewModel.ActiveStep);

        // Move back to file select (Step 1)
        viewModel.BackToFileSelectCommand.Execute(null);
        Assert.Equal(1, viewModel.ActiveStep);
    }
}
