using System;
using System.Collections.Generic;
using System.Linq;

namespace QuanLyHangHoa.Helpers;

public static class SerialNumberNormalizer
{
    public static string? Normalize(string? serialNumber)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
            return null;

        return serialNumber.Trim().ToUpperInvariant();
    }

    public static List<string> NormalizeAll(IEnumerable<string?> serialNumbers)
    {
        if (serialNumbers == null)
            return [];

        return serialNumbers
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static HashSet<string> ToNormalizedHashSet(IEnumerable<string?> serialNumbers)
    {
        if (serialNumbers == null)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return serialNumbers
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
