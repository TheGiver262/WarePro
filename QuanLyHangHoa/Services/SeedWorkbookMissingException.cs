using System;
using System.IO;

namespace QuanLyHangHoa.Services;

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
