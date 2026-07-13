using Xunit;
using Moq;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.ViewModels;
using QuanLyHangHoa.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System;

namespace QuanLyHangHoa.Tests.ViewModels;

public class ProductSerialViewModelTests
{
    [Fact]
    public void SearchSerialsPassesSearchTextAndStatusToLoader()
    {
        string? searchText = null;
        string? status = null;
        var viewModel = new ProductSerialViewModel(
            () => null!, 
            (inputSearch, prod, brand, inputStatus, from, to, note) =>
            {
                return new List<ProductSerial>();
            },
            (inputSearch, prod, brand, inputStatus, from, to, note, skip, take) =>
            {
                searchText = inputSearch;
                status = inputStatus;
                return new List<ProductSerial>
                {
                    new() { Id = 1, SerialNumber = "ABC-001", CurrentStatus = "InStock" }
                };
            },
            new Moq.Mock<IProductSerialImportService>().Object,
            new AppUser { Id = 1 }
        );
        viewModel.SearchSerial = "ABC";
        viewModel.SelectedStatus = "Trong kho";

        // Chờ tối đa 1 giây cho tác vụ nền hoàn thành
        int timeout = 100;
        while ((searchText != "ABC" || status != "InStock" || viewModel.Serials.Count == 0) && timeout-- > 0)
        {
            Thread.Sleep(10);
        }

        Assert.Equal("ABC", searchText);
        Assert.Equal("InStock", status);
        Assert.Single(viewModel.Serials);
        Assert.Equal("ABC-001", viewModel.Serials.Single().SerialNumber);
    }

    [Fact]
    public void ClearSearchResetsFiltersAndReloadsSerials()
    {
        var calls = new List<(string SearchText, string Status)>();
        var viewModel = new ProductSerialViewModel(
            () => null!,
            (inputSearch, prod, brand, inputStatus, from, to, note) =>
            {
                return new List<ProductSerial>();
            },
            (inputSearch, prod, brand, inputStatus, from, to, note, skip, take) =>
            {
                calls.Add((inputSearch, inputStatus));
                return new List<ProductSerial>();
            },
            new Moq.Mock<IProductSerialImportService>().Object,
            new AppUser { Id = 1 }
        );
        viewModel.SearchSerial = "ABC";
        viewModel.SelectedStatus = "Đã bán";

        viewModel.ClearSearchCommand.Execute(null);

        // Chờ tác vụ nền hoàn thành
        int timeout = 100;
        while ((calls.Count == 0 || calls.Last().Status != "All") && timeout-- > 0)
        {
            Thread.Sleep(10);
        }

        Assert.Equal(string.Empty, viewModel.SearchSerial);
        Assert.Equal("Tất cả trạng thái", viewModel.SelectedStatus);
        Assert.Equal((string.Empty, "All"), calls.Last());
    }

    [Fact]
    public void Serial_edit_exposes_read_only_status_display_without_status_editor_state()
    {
        var type = typeof(ProductSerialEditViewModel);
        var statusDisplay = type.GetProperty("StatusDisplay");

        Assert.NotNull(statusDisplay);
        Assert.False(statusDisplay!.CanWrite);
        Assert.Null(type.GetProperty("SelectedStatus"));
        Assert.Null(type.GetProperty("Statuses"));

        var viewModel = new ProductSerialEditViewModel(
            () => null!, new ProductSerial { CurrentStatus = "ReturnedToManufacturer" }, userId: 1);
        Assert.Equal("Trả lại NCC", statusDisplay.GetValue(viewModel));
    }
}
