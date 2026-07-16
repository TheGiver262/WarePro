using System;
using System.Collections.Generic;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services;

public enum WarrantyClaimAction
{
    Resolve,
    Send,
    Repair,
    Replace,
    Reject,
    Close,
    CompleteShopRepair,
    ReceiveManufacturerRepair,
    ReceiveManufacturerReplacement,
    ReplaceFromStock
}

public static class WarrantyClaimTransitions
{
    // bảng này là state machine duy nhất; Closed và Rejected có tập rỗng nên là trạng thái cuối
    private static readonly IReadOnlyDictionary<string, HashSet<WarrantyClaimAction>> AllowedActions =
        new Dictionary<string, HashSet<WarrantyClaimAction>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Open"] =
            [
                WarrantyClaimAction.Resolve,
                WarrantyClaimAction.Send,
                WarrantyClaimAction.Repair,
                WarrantyClaimAction.CompleteShopRepair,
                WarrantyClaimAction.Reject
            ],
            ["ManufacturerWait"] =
            [
                WarrantyClaimAction.Repair,
                WarrantyClaimAction.Replace,
                WarrantyClaimAction.ReceiveManufacturerRepair,
                WarrantyClaimAction.ReceiveManufacturerReplacement
            ],
            ["Ready"] =
            [
                WarrantyClaimAction.Replace,
                WarrantyClaimAction.ReplaceFromStock,
                WarrantyClaimAction.Close
            ],
            ["Closed"] = [],
            ["Rejected"] = []
        };

    public static bool IsAllowed(string currentStatus, WarrantyClaimAction action)
    {
        return AllowedActions.TryGetValue(currentStatus ?? string.Empty, out var actions)
            && actions.Contains(action);
    }

    // thay trực tiếp từ kho còn yêu cầu ResolutionType=Replace ngoài điều kiện status
    public static bool IsAllowed(WarrantyClaim claim, WarrantyClaimAction action)
    {
        ArgumentNullException.ThrowIfNull(claim);
        return IsAllowed(claim.Status, action)
            && (action != WarrantyClaimAction.ReplaceFromStock
                || string.Equals(
                    claim.ResolutionType,
                    "Replace",
                    StringComparison.OrdinalIgnoreCase));
    }

    // service gọi EnsureAllowed trước khi sửa entity để lỗi không để lại trạng thái dở dang
    public static void EnsureAllowed(string currentStatus, WarrantyClaimAction action)
    {
        if (!IsAllowed(currentStatus, action))
        {
            throw new InvalidOperationException(
                $"Warranty action '{action}' is not allowed from status '{currentStatus}'.");
        }
    }

    public static void EnsureAllowed(WarrantyClaim claim, WarrantyClaimAction action)
    {
        ArgumentNullException.ThrowIfNull(claim);
        if (!IsAllowed(claim, action))
        {
            throw new InvalidOperationException(
                $"Warranty action '{action}' is not allowed for status '{claim.Status}' " +
                $"and resolution '{claim.ResolutionType}'.");
        }
    }

    // claim cuối chỉ đọc; các trường mô tả cũng không được sửa sau khi đóng hoặc từ chối
    public static void EnsureMutable(string currentStatus)
    {
        if (IsTerminal(currentStatus))
        {
            throw new InvalidOperationException(
                $"Warranty claim in terminal status '{currentStatus}' is read-only.");
        }
    }

    public static bool IsTerminal(string currentStatus)
    {
        return string.Equals(currentStatus, "Closed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(currentStatus, "Rejected", StringComparison.OrdinalIgnoreCase);
    }
}
