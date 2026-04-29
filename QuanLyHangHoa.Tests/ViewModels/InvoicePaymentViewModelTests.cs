using QuanLyHangHoa.Models;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Tests.ViewModels;

public class InvoicePaymentViewModelTests
{
    [Fact]
    public void SavePaymentRecordsSalesPaymentForCurrentUser()
    {
        int? invoiceId = null;
        decimal? amount = null;
        string? method = null;
        string? note = null;
        int? receivedBy = null;

        var viewModel = new InvoicePaymentViewModel(
            new Employee { Id = 12 },
            (id, inputAmount, inputMethod, inputNote, userId) =>
            {
                invoiceId = id;
                amount = inputAmount;
                method = inputMethod;
                note = inputNote;
                receivedBy = userId;
            },
            (_, _, _, _, _) => { },
            (_, _) => { });
        viewModel.IsSalesMode = true;
        viewModel.InvoiceIdText = "5";
        viewModel.Amount = 250m;
        viewModel.PaymentMethod = "Cash";
        viewModel.Note = "Dot 1";

        viewModel.SavePaymentCommand.Execute(null);

        Assert.Equal(5, invoiceId);
        Assert.Equal(250m, amount);
        Assert.Equal("Cash", method);
        Assert.Equal("Dot 1", note);
        Assert.Equal(12, receivedBy);
        Assert.Equal("Da ghi nhan thanh toan.", viewModel.StatusMessage);
    }

    [Fact]
    public void SavePaymentRejectsInvalidInvoiceId()
    {
        var called = false;
        var viewModel = new InvoicePaymentViewModel(
            new Employee { Id = 12 },
            (_, _, _, _, _) => called = true,
            (_, _, _, _, _) => called = true,
            (_, _) => { });
        viewModel.InvoiceIdText = "abc";
        viewModel.Amount = 250m;

        viewModel.SavePaymentCommand.Execute(null);

        Assert.False(called);
        Assert.Equal("InvoiceId khong hop le.", viewModel.StatusMessage);
    }
}
