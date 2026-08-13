namespace QuanLyHangHoa.Services;

// snapshot số lượng dùng chung cho thẻ tổng, nháp và đã ghi sổ trên danh sách chứng từ
public sealed class DocumentListStats
{
    public int TotalCount { get; init; }
    public int DraftCount { get; init; }
    public int PendingApprovalCount { get; init; }
    public int ApprovedCount { get; init; }
    public int PostedCount { get; init; }
}
