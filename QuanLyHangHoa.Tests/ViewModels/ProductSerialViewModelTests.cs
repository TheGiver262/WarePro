using QuanLyHangHoa.Models;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Tests.ViewModels;

public class ProductSerialViewModelTests
{
    [Fact]
    public void SearchSerialsPassesSearchTextAndStatusToLoader()
    {
        string? searchText = null;
        string? status = null;
        var viewModel = new ProductSerialViewModel((inputSearch, inputStatus) =>
        {
            searchText = inputSearch;
            status = inputStatus;
            return new List<ProductSerial>
            {
                new() { Id = 1, SerialNumber = "ABC-001", Status = "InStock" }
            };
        });
        viewModel.SearchText = "ABC";
        viewModel.SelectedStatus = "InStock";

        viewModel.SearchSerialsCommand.Execute(null);

        Assert.Equal("ABC", searchText);
        Assert.Equal("InStock", status);
        Assert.Single(viewModel.Serials);
        Assert.Equal("ABC-001", viewModel.Serials.Single().SerialNumber);
    }

    [Fact]
    public void ClearSearchResetsFiltersAndReloadsSerials()
    {
        var calls = new List<(string SearchText, string Status)>();
        var viewModel = new ProductSerialViewModel((inputSearch, inputStatus) =>
        {
            calls.Add((inputSearch, inputStatus));
            return new List<ProductSerial>();
        });
        viewModel.SearchText = "ABC";
        viewModel.SelectedStatus = "Sold";

        viewModel.ClearSearchCommand.Execute(null);

        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Equal("All", viewModel.SelectedStatus);
        Assert.Equal((string.Empty, "All"), calls.Last());
    }
}
