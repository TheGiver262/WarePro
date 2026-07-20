namespace QuanLyHangHoa.Models;

public static class InvoiceStatus
{
    public const string Active = "Active";
    public const string Voided = "Voided";
    public const string CheckConstraint =
        "[Status] IN ('Active', 'Voided')";
}
