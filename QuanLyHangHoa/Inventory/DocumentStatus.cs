namespace QuanLyHangHoa.Inventory
{
    /// <summary>
    /// chuỗi trạng thái tương thích với entity và dữ liệu cũ trong database.
    /// </summary>
    public static class DocumentStatus
    {
        public const string Draft = "Draft";
        public const string PendingApproval = "PendingApproval";
        public const string Approved = "Approved";
        public const string Posted = "Posted";
        public const string Locked = "Locked";
        public const string Cancelled = "Cancelled";
    }
}
