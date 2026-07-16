using System;
using QuanLyHangHoa.Inventory;

namespace QuanLyHangHoa.ViewModels;

// chuẩn hóa status tiếng anh và nhãn cũ để mọi ViewModel khóa/mở command giống nhau
internal static class StockDocumentUiLifecycle
{
    public static bool IsDraft(string? status) =>
        Matches(status, DocumentStatus.Draft) || Matches(status, "nháp");

    public static bool IsPendingApproval(string? status) =>
        Matches(status, DocumentStatus.PendingApproval);

    public static bool IsApproved(string? status) =>
        Matches(status, DocumentStatus.Approved);

    public static bool IsPosted(string? status) =>
        Matches(status, DocumentStatus.Posted) || Matches(status, "đã ghi sổ");

    private static bool Matches(string? actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
}
