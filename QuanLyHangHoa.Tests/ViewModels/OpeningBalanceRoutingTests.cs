using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Microsoft.Data.Sqlite;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services.DataImport;
using QuanLyHangHoa.Tests.Helpers;
using QuanLyHangHoa.ViewModels;
using Xunit;

namespace QuanLyHangHoa.Tests.ViewModels;

public sealed class OpeningBalanceRoutingTests
{
    [Fact]
    public void Stock_in_selection_routes_to_opening_balance_document_and_ledger()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(db);
            db.Products.Add(new Product
            {
                Id = 1720,
                ProductCode = "OPENING-ROUTE",
                DisplayName = "Opening route product",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 1,
                DefaultPrice = 10m,
                IsActive = true,
                IsSerialTracked = false
            });
            db.SaveChanges();
        }

        var viewModel = new OpeningBalanceImportViewModel(
            1,
            () => DatabaseHelper.CreateContext(connection),
            (_, _) => { });

        SetRawRows(viewModel, new List<Dictionary<string, string>>
        {
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["DocumentCode"] = "OB-ROUTE-001",
                ["ImportDate"] = "2026-07-13",
                ["ProductCode"] = "OPENING-ROUTE",
                ["Quantity"] = "3",
                ["SerialNumbers"] = string.Empty
            }
        });
        viewModel.SelectedImportTypeItem = viewModel.ImportTypes.Single(item => item.Value == ImportFileType.StockIn);
        viewModel.ColumnMappings = new ObservableCollection<ColumnMappingItem>
        {
            Mapping("DocumentCode"),
            Mapping("ImportDate"),
            Mapping("ProductCode"),
            Mapping("Quantity"),
            Mapping("SerialNumbers")
        };

        viewModel.ConfirmImportCommand.Execute(null);

        Assert.Equal(1, viewModel.SuccessCount);
        Assert.Empty(viewModel.Errors);
        using var assertContext = DatabaseHelper.CreateContext(connection);
        var stockIn = Assert.Single(assertContext.StockIns);
        Assert.Equal("OpeningBalance", stockIn.PurposeCode);
        var ledger = Assert.Single(assertContext.StockLedgers);
        Assert.Equal("StockIn", ledger.SourceDocumentType);
        Assert.Equal(stockIn.Id, ledger.SourceDocumentId);
    }

    private static ColumnMappingItem Mapping(string key)
    {
        return new ColumnMappingItem
        {
            DbFieldKey = key,
            DbFieldName = key,
            ExcelHeader = key
        };
    }

    private static void SetRawRows(
        OpeningBalanceImportViewModel viewModel,
        List<Dictionary<string, string>> rows)
    {
        var field = typeof(OpeningBalanceImportViewModel).GetField(
            "_rawRows",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(viewModel, rows);
    }
}
