using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class AppUser
{
    public int Id { get; set; }

    public string Username { get; set; } = null!;

    // chỉ lưu BCrypt hash, không lưu mật khẩu rõ
    public string PasswordHash { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string RoleCode { get; set; } = null!;

    public bool MustChangePassword { get; set; }

    // bộ đếm và LockoutUntil tạo chính sách khóa tạm sau nhiều lần đăng nhập sai
    public int FailedLoginCount { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LockoutUntil { get; set; }

    public DateTime? LastFailedLoginAt { get; set; }

    public DateTime? LastPasswordChangedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual AppUser? Creator { get; set; }

    public virtual ICollection<AppUser> InverseCreator { get; set; } = new List<AppUser>();

    public virtual ICollection<StockAdjustment> StockAdjustmentApprovers { get; set; } = new List<StockAdjustment>();

    public virtual ICollection<StockAdjustment> StockAdjustmentCreators { get; set; } = new List<StockAdjustment>();

    public virtual ICollection<StockAdjustment> StockAdjustmentPosters { get; set; } = new List<StockAdjustment>();

    public virtual ICollection<StockCountSession> StockCountSessionApprovers { get; set; } = new List<StockCountSession>();

    public virtual ICollection<StockCountSession> StockCountSessionCreators { get; set; } = new List<StockCountSession>();

    public virtual ICollection<StockCountSession> StockCountSessionPosters { get; set; } = new List<StockCountSession>();

    public virtual ICollection<StockIn> StockInApprovers { get; set; } = new List<StockIn>();

    public virtual ICollection<StockIn> StockInCreators { get; set; } = new List<StockIn>();

    public virtual ICollection<StockIn> StockInPosters { get; set; } = new List<StockIn>();

    public virtual ICollection<PurchaseInvoice> PurchaseInvoices { get; set; } = new List<PurchaseInvoice>();

    public virtual ICollection<SalesInvoice> SalesInvoices { get; set; } = new List<SalesInvoice>();

    public virtual ICollection<StockLedger> StockLedgers { get; set; } = new List<StockLedger>();

    public virtual ICollection<StockOut> StockOutApprovers { get; set; } = new List<StockOut>();

    public virtual ICollection<StockOut> StockOutCreators { get; set; } = new List<StockOut>();

    public virtual ICollection<StockOut> StockOutPosters { get; set; } = new List<StockOut>();

    public virtual ICollection<WarrantyClaim> WarrantyClaimApprovers { get; set; } = new List<WarrantyClaim>();

    public virtual ICollection<WarrantyClaim> WarrantyClaimProcessors { get; set; } = new List<WarrantyClaim>();
    public virtual ICollection<StockTransfer> StockTransferApprovers { get; set; } = new List<StockTransfer>();
    public virtual ICollection<StockTransfer> StockTransferCreators { get; set; } = new List<StockTransfer>();
    public virtual ICollection<StockTransfer> StockTransferPosters { get; set; } = new List<StockTransfer>();
}
