using System;

namespace QuanLyHangHoa.Models;

public static class PaymentStatus
{
    public const string Unpaid = "Unpaid";
    public const string PartiallyPaid = "PartiallyPaid";
    public const string Paid = "Paid";
    public const string Overdue = "Overdue";
    public const string CheckConstraint =
        "[PaymentStatus] IN ('Unpaid', 'PartiallyPaid', 'Paid', 'Overdue')";

    public static string Normalize(string? value)
    {
        var status = value?.Trim();
        if (string.Equals(status, "Partial", StringComparison.OrdinalIgnoreCase))
        {
            return PartiallyPaid;
        }

        foreach (var canonical in new[] { Unpaid, PartiallyPaid, Paid, Overdue })
        {
            if (string.Equals(status, canonical, StringComparison.OrdinalIgnoreCase))
            {
                return canonical;
            }
        }

        throw new InvalidOperationException($"Unsupported payment status '{value}'.");
    }
}
