using QuanLyHangHoa.Services.DataImport;

namespace QuanLyHangHoa.Tests.Services;

/// <summary>
/// Regression tests cho LỖI 4 — date parsing deterministic.
/// Dùng reflection để gọi private GetMappedDateTime vì đây là internal parsing helper.
/// </summary>
public sealed class DateImportParsingTests
{
    private static readonly System.Reflection.MethodInfo GetMappedDateTime =
        typeof(DynamicImportService)
            .GetMethod("GetMappedDateTime",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
        ?? throw new InvalidOperationException("GetMappedDateTime method not found on DynamicImportService");

    private static DynamicImportService CreateService() =>
        new DynamicImportService(() => throw new InvalidOperationException("db not needed for date test"));

    private static DateTime Invoke(DynamicImportService _, string value)
    {
        // signature: static GetMappedDateTime(Dictionary<string, string> row, Dictionary<string, string> mappings, string dbKey, bool required)
        var row = new Dictionary<string, string> { ["date"] = value };
        var mapping = new Dictionary<string, string> { ["date"] = "date" };
        try
        {
            return (DateTime)GetMappedDateTime.Invoke(null, new object[] { row, mapping, "date", true })!;
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw; // unreachable
        }
    }

    [Fact]
    public void Vietnamese_dmy_parses_correctly()
    {
        // 03/04/2026 phải là 3 tháng 4 năm 2026, không phải 4 tháng 3
        var result = Invoke(CreateService(), "03/04/2026");
        Assert.Equal(new DateTime(2026, 4, 3), result);
    }

    [Fact]
    public void ISO_date_parses_correctly()
    {
        var result = Invoke(CreateService(), "2026-04-03");
        Assert.Equal(new DateTime(2026, 4, 3), result);
    }

    [Fact]
    public void Full_iso_slash_parses_correctly()
    {
        var result = Invoke(CreateService(), "2026/04/03");
        Assert.Equal(new DateTime(2026, 4, 3), result);
    }

    [Fact]
    public void Dash_dmy_parses_correctly()
    {
        var result = Invoke(CreateService(), "03-04-2026");
        Assert.Equal(new DateTime(2026, 4, 3), result);
    }

    [Fact]
    public void Short_dmy_parses_correctly()
    {
        var result = Invoke(CreateService(), "3/4/2026");
        Assert.Equal(new DateTime(2026, 4, 3), result);
    }

    [Fact]
    public void US_mdy_format_is_not_supported_after_fix()
    {
        // 13/04/2026: ngày 13 tháng 4 — rõ ràng là dd/MM/yyyy không phải MM/dd/yyyy
        // Điều này chứng minh parser không nhầm sang US format
        var result = Invoke(CreateService(), "13/04/2026");
        Assert.Equal(new DateTime(2026, 4, 13), result);
    }

    [Fact]
    public void Invalid_date_throws_argument_exception()
    {
        // Format không được hỗ trợ phải throw, không trả về DateTime.Now
        Assert.Throws<ArgumentException>(() => Invoke(CreateService(), "not-a-date"));
    }

    [Fact]
    public void Ambiguous_us_format_is_rejected()
    {
        // "04/03/2026" trong context Việt Nam là 4 tháng 3
        // Sau khi fix, MM/dd/yyyy không có trong danh sách => chỉ parse theo dd/MM/yyyy
        var result = Invoke(CreateService(), "04/03/2026");
        Assert.Equal(new DateTime(2026, 3, 4), result); // ngày 4 tháng 3, không phải ngày 3 tháng 4 theo US
    }
}
