using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services;

public sealed class DocumentPrintModel
{
    public string Title { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string DateLabel { get; init; } = "Ngày lập";
    public DateTime? DocumentDate { get; init; }
    public string PartnerLabel { get; init; } = string.Empty;
    public string PartnerName { get; init; } = "—";
    public string PartnerCode { get; init; } = "—";
    public string PartnerPhone { get; init; } = "—";
    public string PartnerAddress { get; init; } = "—";
    public string WarehouseName { get; init; } = "—";
    public string LinkedDocumentCode { get; init; } = "—";
    public string CreatedByName { get; init; } = "—";
    public string StatusText { get; init; } = "—";
    public string Notes { get; init; } = "—";
    // các giá trị tiền giữ decimal gốc; XAML quyết định định dạng tiền tệ khi hiển thị
    public decimal SubTotal { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal GrandTotal { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal RemainingAmount => Math.Max(0, GrandTotal - PaidAmount);
    public bool ShowPaymentSummary { get; init; }
    public string LeftSignatureTitle { get; init; } = "NGƯỜI LẬP";
    public string RightSignatureTitle { get; init; } = "ĐỐI TÁC";
    public IReadOnlyList<DocumentPrintLine> Lines { get; init; } = Array.Empty<DocumentPrintLine>();

    // tạo snapshot phẳng để cửa sổ in không còn phụ thuộc DbContext sau khi service đóng
    public static DocumentPrintModel FromPurchaseInvoice(PurchaseInvoice invoice) => new()
    {
        Title = "HÓA ĐƠN MUA HÀNG",
        Code = invoice.InvoiceCode,
        DateLabel = "Ngày hóa đơn",
        DocumentDate = invoice.InvoiceDate,
        PartnerLabel = "Nhà cung cấp",
        PartnerName = invoice.Supplier?.DisplayName ?? "—",
        PartnerCode = invoice.Supplier?.SupplierCode ?? "—",
        PartnerPhone = invoice.Supplier?.Phone ?? "—",
        PartnerAddress = invoice.Supplier?.Address ?? "—",
        WarehouseName = invoice.StockIn?.Warehouse?.DisplayName ?? "—",
        LinkedDocumentCode = invoice.StockIn?.DocumentCode ?? "—",
        CreatedByName = invoice.Creator?.FullName ?? "—",
        StatusText = PaymentStatus(invoice.PaymentStatus),
        Notes = Text(invoice.Notes),
        SubTotal = invoice.SubTotal,
        TaxAmount = invoice.TaxAmount,
        GrandTotal = invoice.GrandTotal,
        PaidAmount = invoice.PaidAmount,
        ShowPaymentSummary = true,
        RightSignatureTitle = "NHÀ CUNG CẤP",
        Lines = invoice.Lines.Select((line, index) => InvoiceLine(
            index, line.Product, line.Unit, line.Quantity, line.UnitPrice,
            line.TaxRate, line.GrandTotal)).ToList()
    };

    public static DocumentPrintModel FromSalesInvoice(SalesInvoice invoice) => new()
    {
        Title = "HÓA ĐƠN BÁN HÀNG",
        Code = invoice.InvoiceCode,
        DateLabel = "Ngày hóa đơn",
        DocumentDate = invoice.InvoiceDate,
        PartnerLabel = "Khách hàng",
        PartnerName = invoice.Customer?.DisplayName ?? "—",
        PartnerCode = invoice.Customer?.CustomerCode ?? "—",
        PartnerPhone = invoice.Customer?.Phone ?? "—",
        PartnerAddress = invoice.Customer?.Address ?? "—",
        WarehouseName = invoice.StockOut?.Warehouse?.DisplayName ?? "—",
        LinkedDocumentCode = invoice.StockOut?.DocumentCode ?? "—",
        CreatedByName = invoice.Creator?.FullName ?? "—",
        StatusText = PaymentStatus(invoice.PaymentStatus),
        Notes = Text(invoice.Notes),
        SubTotal = invoice.SubTotal,
        TaxAmount = invoice.TaxAmount,
        GrandTotal = invoice.GrandTotal,
        PaidAmount = invoice.PaidAmount,
        ShowPaymentSummary = true,
        RightSignatureTitle = "KHÁCH HÀNG",
        Lines = invoice.Lines.Select((line, index) => InvoiceLine(
            index, line.Product, line.Unit, line.Quantity, line.UnitPrice,
            line.TaxRate, line.GrandTotal)).ToList()
    };

    // phiếu kho không có tóm tắt thanh toán; tổng dòng tính từ quantity và unit price
    public static DocumentPrintModel FromStockIn(StockIn stockIn)
    {
        var lines = stockIn.Lines.Select((line, index) => StockLine(
            index, line.Product, line.Unit, line.Quantity, line.UnitPrice, line.DraftSerials)).ToList();
        return new DocumentPrintModel
        {
            Title = "PHIẾU NHẬP KHO",
            Code = stockIn.DocumentCode,
            DateLabel = "Ngày nhập",
            DocumentDate = stockIn.ImportDate,
            PartnerLabel = "Nhà cung cấp",
            PartnerName = stockIn.Supplier?.DisplayName ?? "—",
            PartnerCode = stockIn.Supplier?.SupplierCode ?? "—",
            PartnerPhone = stockIn.Supplier?.Phone ?? "—",
            PartnerAddress = stockIn.Supplier?.Address ?? "—",
            WarehouseName = stockIn.Warehouse?.DisplayName ?? "—",
            CreatedByName = stockIn.Creator?.FullName ?? "—",
            StatusText = DocumentStatusText(stockIn.Status),
            Notes = Text(stockIn.Notes),
            SubTotal = lines.Sum(line => line.LineTotal),
            GrandTotal = lines.Sum(line => line.LineTotal),
            RightSignatureTitle = "THỦ KHO",
            Lines = lines
        };
    }

    public static DocumentPrintModel FromStockOut(StockOut stockOut)
    {
        var lines = stockOut.Lines.Select((line, index) => StockLine(
            index, line.Product, line.Unit, line.Quantity, line.UnitPrice, line.DraftSerials)).ToList();
        return new DocumentPrintModel
        {
            Title = "PHIẾU XUẤT KHO",
            Code = stockOut.DocumentCode,
            DateLabel = "Ngày xuất",
            DocumentDate = stockOut.ExportDate,
            PartnerLabel = "Khách hàng",
            PartnerName = stockOut.Customer?.DisplayName ?? "—",
            PartnerCode = stockOut.Customer?.CustomerCode ?? "—",
            PartnerPhone = stockOut.Customer?.Phone ?? "—",
            PartnerAddress = stockOut.Customer?.Address ?? "—",
            WarehouseName = stockOut.Warehouse?.DisplayName ?? "—",
            CreatedByName = stockOut.Creator?.FullName ?? "—",
            StatusText = DocumentStatusText(stockOut.Status),
            Notes = Text(stockOut.Notes),
            SubTotal = lines.Sum(line => line.LineTotal),
            GrandTotal = lines.Sum(line => line.LineTotal),
            RightSignatureTitle = "NGƯỜI NHẬN",
            Lines = lines
        };
    }

    // taxRate là tỷ lệ thập phân, ví dụ 0.1 được hiển thị thành 10%
    private static DocumentPrintLine InvoiceLine(
        int index, Product? product, Unit? unit, decimal quantity,
        decimal unitPrice, decimal taxRate, decimal total) => new()
    {
        Number = index + 1,
        ProductCode = product?.ProductCode ?? "—",
        ProductName = product?.DisplayName ?? "—",
        UnitName = unit?.DisplayName ?? "—",
        Quantity = quantity,
        UnitPrice = unitPrice,
        TaxRate = taxRate,
        LineTotal = total
    };

    // serials giữ dạng chuỗi đã chốt trên dòng chứng từ để bản in phản ánh đúng snapshot
    private static DocumentPrintLine StockLine(
        int index, Product? product, Unit? unit, decimal quantity,
        decimal unitPrice, string? serials) => new()
    {
        Number = index + 1,
        ProductCode = product?.ProductCode ?? "—",
        ProductName = product?.DisplayName ?? "—",
        UnitName = unit?.DisplayName ?? "—",
        Quantity = quantity,
        UnitPrice = unitPrice,
        LineTotal = quantity * unitPrice,
        Serials = Text(serials)
    };

    private static string Text(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private static string PaymentStatus(string? status) => status switch
    {
        global::QuanLyHangHoa.Models.PaymentStatus.Paid => "Đã thanh toán",
        global::QuanLyHangHoa.Models.PaymentStatus.PartiallyPaid => "Thanh toán một phần",
        global::QuanLyHangHoa.Models.PaymentStatus.Overdue => "Quá hạn",
        global::QuanLyHangHoa.Models.PaymentStatus.Unpaid => "Chưa thanh toán",
        _ => Text(status)
    };

    private static string DocumentStatusText(string? status) => status switch
    {
        "Draft" or "nháp" => "Nháp",
        "Approved" or "đã duyệt" => "Đã duyệt",
        "Posted" or "đã ghi sổ" => "Đã ghi sổ",
        _ => Text(status)
    };
}

// một dòng in đã phẳng hóa tên sản phẩm/đơn vị, số lượng, đơn giá và tổng tiền
public sealed class DocumentPrintLine
{
    public int Number { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string UnitName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal TaxRate { get; init; }
    public decimal LineTotal { get; init; }
    public string Serials { get; init; } = "—";
    public string TaxRateText => TaxRate <= 0 ? "—" : $"{TaxRate:P0}";
}
