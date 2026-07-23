

using System;

using System.Collections.Generic;

using System.Linq;

using Microsoft.EntityFrameworkCore;

using QuanLyHangHoa.Data;



namespace QuanLyHangHoa.Services;



public sealed class SerialTraceFilter

{

    public string SearchText { get; set; } = string.Empty;
    
    public string ProductText { get; set; } = string.Empty;
    
    public string DocumentText { get; set; } = string.Empty;
    
    public string PartnerText { get; set; } = string.Empty;
    
    public string? Status { get; set; }
    
    public DateTime? FromDate { get; set; }
    
    public DateTime? ToDate { get; set; }
    
}



public sealed class ProductTimelineResult

{

    public decimal StartQuantity { get; init; }
    
    public decimal EndQuantity { get; init; }
    
    public IReadOnlyList<ProductTimelineItem> Items { get; init; } = Array.Empty<ProductTimelineItem>();
    
}



public sealed class ProductTimelineItem

{

    public DateTime Date { get; init; }
    
    public string ProductCode { get; init; } = string.Empty;
    
    public string ProductName { get; init; } = string.Empty;
    
    public string DocumentCode { get; init; } = string.Empty;
    
    public string SourceDocumentType { get; init; } = string.Empty;
    
    public string Purpose { get; init; } = string.Empty;
    
    public string PartnerName { get; init; } = string.Empty;
    
    public string WarehouseName { get; init; } = string.Empty;
    
    public string UserName { get; init; } = string.Empty;
    
    public decimal InQty { get; init; }
    
    public decimal OutQty { get; init; }
    
    public decimal BalanceQty { get; init; }
    
}



public sealed class SerialTraceItem

{

    public string SerialNumber { get; init; } = string.Empty;
    
    public string ProductCode { get; init; } = string.Empty;
    
    public string ProductName { get; init; } = string.Empty;
    
    public string CurrentStatus { get; init; } = string.Empty;
    
    public string CurrentWarehouseName { get; init; } = string.Empty;
    
    public string ImportDocCode { get; init; } = string.Empty;
    
    public DateTime? ImportDate { get; init; }
    
    public string ImportWarehouseName { get; init; } = string.Empty;
    
    public string SupplierName { get; init; } = string.Empty;
    
    public string ExportDocCode { get; init; } = string.Empty;
    
    public DateTime? ExportDate { get; init; }
    
    public string ExportWarehouseName { get; init; } = string.Empty;
    
    public string CustomerName { get; init; } = string.Empty;
    
    public decimal? SellPrice { get; init; }
    
    public string SalesInvoiceCode { get; init; } = string.Empty;
    
    public DateTime? SalesInvoiceDate { get; init; }
    
    public string WarrantyStatus { get; init; } = string.Empty;
    
    public DateTime? WarrantyStartDate { get; init; }
    
    public DateTime? WarrantyEndDate { get; init; }
    
    public string WarrantyCustomerName { get; init; } = string.Empty;
    
}



public sealed class ReportTraceService

{

    private readonly Func<AppDbContext> _contextFactory;
    


    public ReportTraceService(Func<AppDbContext> contextFactory)
    
    {
    
        _contextFactory = contextFactory;
        
    }
    


    // timeline lấy cả ledger trước ngày bắt đầu để tính StartQuantity, nhưng chỉ tạo item trong khoảng được chọn
    public ProductTimelineResult GetProductTimeline(int productId, DateTime fromDate, DateTime toDate)
    
    {
    
        using var db = _contextFactory();
        
        var start = fromDate.Date;
        
        // tick cuối ngày giữ lại mọi giao dịch có giờ phút trong ngày kết thúc
        var end = toDate.Date.AddDays(1).AddTicks(-1);
        


        var ledgers = db.StockLedgers.AsNoTracking()
        
            .Include(l => l.Product)
            
            .Include(l => l.Warehouse)
            
            .Include(l => l.Poster)
            
            .Where(l => l.ProductId == productId && l.PostedAt <= end)
            
            .OrderBy(l => l.PostedAt)
            
            .ThenBy(l => l.Id)
            
            .ToList();
            


        // currentQty là số dư chạy: bắt đầu bằng tổng lịch sử trước kỳ rồi cộng/trừ từng ledger theo thứ tự
        var currentQty = ledgers
        
            .Where(l => l.PostedAt < start)
            
            .Sum(l => l.MovementType == "In" ? l.Quantity : -l.Quantity);
            


        var currentLedgers = ledgers
        
            .Where(l => l.PostedAt >= start && l.PostedAt <= end)
            
            .ToList();
            


        // context chứng từ được tải theo lô để vòng lặp không phát sinh một query cho mỗi ledger
        var contexts = LoadDocumentContexts(db, currentLedgers);
        
        var items = new List<ProductTimelineItem>();
        


        foreach (var ledger in currentLedgers)
        
        {
        
            var inQty = ledger.MovementType == "In" ? ledger.Quantity : 0m;
            
            var outQty = ledger.MovementType == "Out" ? ledger.Quantity : 0m;
            
            currentQty += inQty - outQty;
            
            var context = GetDocumentContext(contexts, ledger.SourceDocumentType, ledger.SourceDocumentId);
            


            items.Add(new ProductTimelineItem
            
            {
            
                Date = ledger.PostedAt,
                
                ProductCode = ledger.Product?.ProductCode ?? string.Empty,
                
                ProductName = ledger.Product?.DisplayName ?? string.Empty,
                
                DocumentCode = context.DocumentCode,
                
                SourceDocumentType = ledger.SourceDocumentType,
                
                Purpose = context.Purpose,
                
                PartnerName = context.PartnerName,
                
                WarehouseName = ledger.Warehouse?.DisplayName ?? context.WarehouseName,
                
                UserName = ledger.Poster?.FullName ?? ledger.PostedBy.ToString(),
                
                InQty = inQty,
                
                OutQty = outQty,
                
                BalanceQty = currentQty
                
            });
            
        }
        


        return new ProductTimelineResult
        
        {
        
            StartQuantity = ledgers.Where(l => l.PostedAt < start).Sum(l => l.MovementType == "In" ? l.Quantity : -l.Quantity),
            
            EndQuantity = currentQty,
            
            Items = items
            
        };
        
    }
    


    // phần text/status lọc ở database; projection và lọc ngày đa trường thực hiện sau khi giới hạn 500 serial
    public IReadOnlyList<SerialTraceItem> SearchSerialTrace(SerialTraceFilter filter)
    
    {
    
        using var db = _contextFactory();
        
        var start = filter.FromDate?.Date;
        
        var end = filter.ToDate?.Date.AddDays(1).AddTicks(-1);
        


        var query = db.ProductSerials.AsNoTracking()
        
            .Include(s => s.Product)
            
            .Include(s => s.CurrentWarehouse)
            
            .Include(s => s.LastStockInLine).ThenInclude(l => l!.StockIn).ThenInclude(si => si!.Supplier)
            
            .Include(s => s.LastStockInLine).ThenInclude(l => l!.StockIn).ThenInclude(si => si!.Warehouse)
            
            .Include(s => s.LastStockOutLine).ThenInclude(l => l!.StockOut).ThenInclude(so => so!.Customer)
            
            .Include(s => s.LastStockOutLine).ThenInclude(l => l!.StockOut).ThenInclude(so => so!.Warehouse)
            
            .Include(s => s.LastStockOutLine).ThenInclude(l => l!.StockOut).ThenInclude(so => so!.SalesInvoices)
            
            .Include(s => s.WarrantyCoverages).ThenInclude(w => w.Customer)
            
            .Include(s => s.WarrantyCoverages).ThenInclude(w => w.SalesInvoice)
            
            .AsQueryable();
            


        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        
        {
        
            var keyword = filter.SearchText.Trim().ToLower();
            
            query = query.Where(s =>
            
                s.SerialNumber.ToLower().Contains(keyword) ||
                
                (s.Product != null && s.Product.ProductCode.ToLower().Contains(keyword)) ||
                
                (s.Product != null && s.Product.DisplayName.ToLower().Contains(keyword)) ||
                
                (s.LastStockInLine != null && s.LastStockInLine.StockIn != null && s.LastStockInLine.StockIn.DocumentCode.ToLower().Contains(keyword)) ||
                
                (s.LastStockOutLine != null && s.LastStockOutLine.StockOut != null && s.LastStockOutLine.StockOut.DocumentCode.ToLower().Contains(keyword)) ||
                
                s.WarrantyCoverages.Any(w => w.SalesInvoice != null && w.SalesInvoice.InvoiceCode.ToLower().Contains(keyword)));
                
        }
        


        if (!string.IsNullOrWhiteSpace(filter.ProductText))
        
        {
        
            var productKeyword = filter.ProductText.Trim().ToLower();
            
            query = query.Where(s => s.Product != null && (s.Product.ProductCode.ToLower().Contains(productKeyword) || s.Product.DisplayName.ToLower().Contains(productKeyword)));
            
        }
        


        if (!string.IsNullOrWhiteSpace(filter.DocumentText))
        
        {
        
            var docKeyword = filter.DocumentText.Trim().ToLower();
            
            query = query.Where(s =>
            
                (s.LastStockInLine != null && s.LastStockInLine.StockIn != null && s.LastStockInLine.StockIn.DocumentCode.ToLower().Contains(docKeyword)) ||
                
                (s.LastStockOutLine != null && s.LastStockOutLine.StockOut != null && s.LastStockOutLine.StockOut.DocumentCode.ToLower().Contains(docKeyword)) ||
                
                s.WarrantyCoverages.Any(w => w.SalesInvoice != null && w.SalesInvoice.InvoiceCode.ToLower().Contains(docKeyword)));
                
        }
        


        if (!string.IsNullOrWhiteSpace(filter.PartnerText))
        
        {
        
            var partnerKeyword = filter.PartnerText.Trim().ToLower();
            
            query = query.Where(s =>
            
                (s.LastStockInLine != null && s.LastStockInLine.StockIn != null && s.LastStockInLine.StockIn.Supplier != null && s.LastStockInLine.StockIn.Supplier.DisplayName.ToLower().Contains(partnerKeyword)) ||
                
                (s.LastStockOutLine != null && s.LastStockOutLine.StockOut != null && s.LastStockOutLine.StockOut.Customer != null && s.LastStockOutLine.StockOut.Customer.DisplayName.ToLower().Contains(partnerKeyword)) ||
                
                s.WarrantyCoverages.Any(w => w.Customer != null && w.Customer.DisplayName.ToLower().Contains(partnerKeyword)));
                
        }
        


        if (!string.IsNullOrWhiteSpace(filter.Status) && filter.Status != "All")
        
        {
        
            query = query.Where(s => s.CurrentStatus == filter.Status);
            
        }
        


        // Take bảo vệ màn hình khỏi truy vấn không giới hạn; ToList kết thúc SQL trước khi dùng helper C#
        return query
        
            .OrderBy(s => s.SerialNumber)
            
            .Take(500)
            
            .ToList()
            
            .Select(ToSerialTraceItem)
            
            .Where(item => IsWithinDateRange(item, start, end))
            
            .ToList();
            
    }
    


    // serial đạt nếu ít nhất một mốc nhập, xuất, bán hoặc bảo hành nằm trong khoảng ngày
    private static bool IsWithinDateRange(SerialTraceItem item, DateTime? start, DateTime? end)
    
    {
    
        if (!start.HasValue && !end.HasValue)
        
        {
        
            return true;
            
        }
        


        var dates = new[] { item.ImportDate, item.ExportDate, item.SalesInvoiceDate, item.WarrantyStartDate, item.WarrantyEndDate }
        
            .Where(d => d.HasValue)
            
            .Select(d => d!.Value);
            


        return dates.Any(d => (!start.HasValue || d >= start.Value) && (!end.HasValue || d <= end.Value));
        
    }
    


    // ưu tiên hóa đơn gắn với coverage, nếu thiếu mới lấy hóa đơn bán đầu tiên của phiếu xuất
    private static SerialTraceItem ToSerialTraceItem(Models.ProductSerial serial)
    
    {
    
        var stockIn = serial.LastStockInLine?.StockIn;
        
        var stockOut = serial.LastStockOutLine?.StockOut;
        
        var warranty = serial.WarrantyCoverages
            .OrderByDescending(coverage => coverage.CoverageStatus == "Active")
            .ThenByDescending(coverage => coverage.WarrantyStartDate)
            .ThenByDescending(coverage => coverage.Id)
            .FirstOrDefault();
        var invoice = warranty?.SalesInvoice ?? stockOut?.SalesInvoices.OrderBy(i => i.InvoiceDate).FirstOrDefault();
        


        return new SerialTraceItem
        
        {
        
            SerialNumber = serial.SerialNumber,
            
            ProductCode = serial.Product?.ProductCode ?? string.Empty,
            
            ProductName = serial.Product?.DisplayName ?? string.Empty,
            
            CurrentStatus = serial.CurrentStatus,
            
            CurrentWarehouseName = serial.CurrentWarehouse?.DisplayName ?? "-",
            
            ImportDocCode = stockIn?.DocumentCode ?? "-",
            
            ImportDate = stockIn?.PostedAt ?? stockIn?.ImportDate ?? stockIn?.CreatedAt,
            
            ImportWarehouseName = stockIn?.Warehouse?.DisplayName ?? "-",
            
            SupplierName = stockIn?.Supplier?.DisplayName ?? "-",
            
            ExportDocCode = stockOut?.DocumentCode ?? "-",
            
            ExportDate = stockOut?.PostedAt ?? stockOut?.ExportDate ?? stockOut?.CreatedAt,
            
            ExportWarehouseName = stockOut?.Warehouse?.DisplayName ?? "-",
            
            CustomerName = stockOut?.Customer?.DisplayName ?? warranty?.Customer?.DisplayName ?? "-",
            
            SellPrice = serial.LastStockOutLine?.UnitPrice,
            
            SalesInvoiceCode = invoice?.InvoiceCode ?? "-",
            
            SalesInvoiceDate = invoice?.InvoiceDate,
            
            WarrantyStatus = GetWarrantyStatus(serial.LastStockOutLine != null, warranty),
            
            WarrantyStartDate = warranty?.WarrantyStartDate,
            
            WarrantyEndDate = warranty?.WarrantyEndDate,
            
            WarrantyCustomerName = warranty?.Customer?.DisplayName ?? stockOut?.Customer?.DisplayName ?? "-"
            
        };
        
    }
    


    // chưa xuất kho khác với đã bán nhưng không có bảo hành; hai trường hợp cần nhãn riêng trên báo cáo
    private static string GetWarrantyStatus(bool hasStockOut, Models.WarrantyCoverage? warranty)
    
    {
    
        if (!hasStockOut)
        
        {
        
            return "Chưa bán";
            
        }
        


        if (warranty == null)
        
        {
        
            return "Không có bảo hành";
            
        }
        


        return warranty.CoverageStatus == "Active" && warranty.WarrantyEndDate.Date >= DateTime.Today
        
            ? "Còn bảo hành"
            
            : "Hết hạn bảo hành";
            
    }
    


    // khóa kép loại chứng từ + id tránh trùng id giữa StockIn, StockOut, Adjustment và Transfer
    private static Dictionary<(string Type, int Id), DocumentContext> LoadDocumentContexts(AppDbContext db, IReadOnlyCollection<Models.StockLedger> ledgers)
    
    {
    
        var contexts = new Dictionary<(string Type, int Id), DocumentContext>();
        


        var stockInIds = ledgers.Where(l => l.SourceDocumentType == "StockIn").Select(l => l.SourceDocumentId).Distinct().ToList();
        
        foreach (var stockIn in db.StockIns.AsNoTracking().Include(s => s.Supplier).Include(s => s.Warehouse).Where(s => stockInIds.Contains(s.Id)))
        
        {
        
            contexts[("StockIn", stockIn.Id)] = new DocumentContext(stockIn.DocumentCode, stockIn.PurposeCode switch
            
            {
            
                "Purchase" => "Nhap mua",
                
                "OpeningBalance" => "Nhap ton dau ky",
                
                _ => "Nhap dieu chinh"
                
            }, stockIn.Supplier?.DisplayName ?? "-", stockIn.Warehouse?.DisplayName ?? "-");
            
        }
        


        var stockOutIds = ledgers.Where(l => l.SourceDocumentType == "StockOut").Select(l => l.SourceDocumentId).Distinct().ToList();
        
        foreach (var stockOut in db.StockOuts.AsNoTracking().Include(s => s.Customer).Include(s => s.Warehouse).Where(s => stockOutIds.Contains(s.Id)))
        
        {
        
            contexts[("StockOut", stockOut.Id)] = new DocumentContext(stockOut.DocumentCode, stockOut.PurposeCode switch
            
            {
            
                "Sale" => "Xuat ban",
                
                "WarrantyReplacement" => "Xuat bao hanh",
                
                _ => "Xuat dieu chinh"
                
            }, stockOut.Customer?.DisplayName ?? "-", stockOut.Warehouse?.DisplayName ?? "-");
            
        }
        


        var adjustmentIds = ledgers.Where(l => l.SourceDocumentType == "StockAdjustment").Select(l => l.SourceDocumentId).Distinct().ToList();
        
        foreach (var adjustment in db.StockAdjustments.AsNoTracking().Include(s => s.Warehouse).Where(s => adjustmentIds.Contains(s.Id)))
        
        {
        
            contexts[("StockAdjustment", adjustment.Id)] = new DocumentContext(adjustment.DocumentCode, "Dieu chinh kho", "He thong", adjustment.Warehouse?.DisplayName ?? "-");
            
        }
        


        var transferIds = ledgers.Where(l => l.SourceDocumentType == "StockTransfer").Select(l => l.SourceDocumentId).Distinct().ToList();
        
        foreach (var transfer in db.StockTransfers.AsNoTracking().Include(s => s.FromWarehouse).Include(s => s.ToWarehouse).Where(s => transferIds.Contains(s.Id)))
        
        {
        
            contexts[("StockTransfer", transfer.Id)] = new DocumentContext(
            
                transfer.DocumentCode,
                
                "Chuyen kho",
                
                $"{transfer.FromWarehouse?.DisplayName ?? "-"} -> {transfer.ToWarehouse?.DisplayName ?? "-"}",
                
                string.Empty);
                
        }
        


        return contexts;
        
    }
    


    // ledger cũ thiếu chứng từ vẫn có nhãn fallback để báo cáo không bị lỗi toàn bộ
    private static DocumentContext GetDocumentContext(Dictionary<(string Type, int Id), DocumentContext> contexts, string type, int id)
    
    {
    
        return contexts.TryGetValue((type, id), out var context)
        
            ? context
            
            : new DocumentContext($"Ref-{id}", type, "-", "-");
            
    }
    


    private sealed record DocumentContext(string DocumentCode, string Purpose, string PartnerName, string WarehouseName);
    
}

