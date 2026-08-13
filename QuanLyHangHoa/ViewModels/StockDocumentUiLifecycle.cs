using System;
using QuanLyHangHoa.Inventory;

namespace QuanLyHangHoa.ViewModels;

// chuẩn hóa status tiếng anh và nhãn cũ để mọi ViewModel khóa/mở command giống nhau
internal static class StockDocumentUiLifecycle
{
    public static bool IsDraft(string? status) =>
        Matches(status, DocumentStatus.Draft) || Matches(status, "nháp");

    public static bool IsPendingApproval(string? status) =>
        Matches(status, DocumentStatus.PendingApproval) || Matches(status, "chờ duyệt");

    public static bool IsApproved(string? status) =>
        Matches(status, DocumentStatus.Approved) || Matches(status, "đã duyệt");

    public static bool IsPosted(string? status) =>
        Matches(status, DocumentStatus.Posted) || Matches(status, "đã ghi sổ");

    public static string? ParseFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status) || Matches(status, "Tất cả")) return null;
        if (IsDraft(status) || Matches(status, "Phiếu nháp")) return DocumentStatus.Draft;
        if (IsPendingApproval(status)) return DocumentStatus.PendingApproval;
        if (IsApproved(status)) return DocumentStatus.Approved;
        if (IsPosted(status)) return DocumentStatus.Posted;
        return status.Trim();
    }

    public static string GetDisplayLabel(string? status)
    {
        var canonical = ParseFilter(status);
        return canonical switch
        {
            DocumentStatus.Draft => "Phiếu nháp",
            DocumentStatus.PendingApproval => "Chờ duyệt",
            DocumentStatus.Approved => "Đã duyệt",
            DocumentStatus.Posted => "Đã ghi sổ",
            null => "Tất cả",
            _ => status?.Trim() ?? string.Empty
        };
    }

    private static bool Matches(string? actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
}
