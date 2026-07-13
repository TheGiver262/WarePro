using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Services;

public enum WarrantyClaimAction
{
    Resolve,
    Send,
    Repair,
    Replace,
    Reject,
    Close
}

public static class WarrantyClaimTransitions
{
    private static readonly IReadOnlyDictionary<string, HashSet<WarrantyClaimAction>> AllowedActions =
        new Dictionary<string, HashSet<WarrantyClaimAction>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Open"] =
            [
                WarrantyClaimAction.Resolve,
                WarrantyClaimAction.Send,
                WarrantyClaimAction.Repair,
                WarrantyClaimAction.Reject
            ],
            ["ManufacturerWait"] =
            [
                WarrantyClaimAction.Repair,
                WarrantyClaimAction.Replace
            ],
            ["Ready"] =
            [
                WarrantyClaimAction.Replace,
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

    public static void EnsureAllowed(string currentStatus, WarrantyClaimAction action)
    {
        if (!IsAllowed(currentStatus, action))
        {
            throw new InvalidOperationException(
                $"Warranty action '{action}' is not allowed from status '{currentStatus}'.");
        }
    }

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
