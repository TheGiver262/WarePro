using Microsoft.Data.Sqlite;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Tests.Helpers;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Tests.ViewModels;

public sealed class StockLifecycleViewModelTests
{
    [Theory]
    [InlineData(nameof(StockDocumentStatus.PendingApproval))]
    [InlineData(nameof(StockDocumentStatus.Approved))]
    public void StockIn_resume_states_are_processable_but_not_editable(string status)
    {
        using var connection = OpenDatabase();
        var viewModel = new StockInViewModel(Manager(), () => DatabaseHelper.CreateContext(connection))
        {
            Status = status
        };

        Assert.False(viewModel.CanEdit);
        Assert.True(viewModel.CanApprove);
    }

    [Theory]
    [InlineData(nameof(StockDocumentStatus.PendingApproval))]
    [InlineData(nameof(StockDocumentStatus.Approved))]
    public void StockOut_resume_states_are_processable_but_not_editable(string status)
    {
        using var connection = OpenDatabase();
        var viewModel = new StockOutViewModel(Manager(), () => DatabaseHelper.CreateContext(connection))
        {
            Status = status
        };

        Assert.False(viewModel.CanEdit);
        Assert.True(viewModel.CanApprove);
    }

    [Fact]
    public void StockIn_draft_can_be_submitted_by_warehouse_user()
    {
        using var connection = OpenDatabase();
        var viewModel = new StockInViewModel(WarehouseUser(), () => DatabaseHelper.CreateContext(connection));

        Assert.True(viewModel.CanEdit);
        Assert.True(viewModel.CanApprove);
    }

    [Fact]
    public void StockOut_draft_can_be_submitted_by_warehouse_user()
    {
        using var connection = OpenDatabase();
        var viewModel = new StockOutViewModel(WarehouseUser(), () => DatabaseHelper.CreateContext(connection));

        Assert.True(viewModel.CanEdit);
        Assert.True(viewModel.CanApprove);
    }

    [Theory]
    [InlineData(nameof(StockDocumentStatus.PendingApproval))]
    [InlineData(nameof(StockDocumentStatus.Approved))]
    public void StockTransfer_resume_states_are_processable_but_not_editable(string status)
    {
        using var connection = OpenDatabase();
        var viewModel = new StockTransferViewModel(Manager(), () => DatabaseHelper.CreateContext(connection))
        {
            Status = status
        };

        Assert.False(viewModel.CanEdit);
        Assert.True(viewModel.CanProcessLifecycle);
    }

    [Theory]
    [InlineData(nameof(StockDocumentStatus.PendingApproval))]
    [InlineData(nameof(StockDocumentStatus.Approved))]
    public void StockAdjustment_resume_states_are_processable_but_not_editable(string status)
    {
        using var connection = OpenDatabase();
        var viewModel = new StockAdjustmentViewModel(Manager(), () => DatabaseHelper.CreateContext(connection))
        {
            IsEditMode = true,
            Status = status
        };

        Assert.False(viewModel.IsEditMode);
        Assert.True(viewModel.CanProcessLifecycle);
    }

    private static AppUser WarehouseUser() => new()
    {
        Id = 2,
        Username = "warehouse",
        RoleCode = "Nhân viên kho",
        IsActive = true
    };

    private static AppUser Manager() => new()
    {
        Id = 1,
        Username = "manager",
        RoleCode = "Quản lý",
        IsActive = true
    };

    private static SqliteConnection OpenDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = DatabaseHelper.CreateContext(connection);
        DatabaseHelper.SeedBasicData(db);
        return connection;
    }
}
