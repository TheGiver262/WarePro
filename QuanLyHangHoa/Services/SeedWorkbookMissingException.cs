using System;
using System.IO;

namespace QuanLyHangHoa.Services;

/// <summary>
/// phân biệt lỗi thiếu workbook bắt buộc của database mới với trường hợp database đã có người dùng.
/// </summary>
public sealed class SeedWorkbookMissingException : FileNotFoundException
{
    public SeedWorkbookMissingException(string seedPath)
        : base($"DB-SEED-MISSING: WarePro seed workbook was not found: {seedPath}", seedPath)
    {
        Code = "DB-SEED-MISSING";
        SeedPath = seedPath;
    }

    public string Code { get; }
    public string SeedPath { get; }
}
