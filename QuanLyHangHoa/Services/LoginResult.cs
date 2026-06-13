using System;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public enum LoginStatus
    {
        Success,
        InvalidCredentials,
        LockedOut,
        Inactive
    }

    public class LoginResult
    {
        public LoginStatus Status { get; set; }
        public AppUser? User { get; set; }
        public DateTime? LockoutUntil { get; set; }
        public int FailedLoginCount { get; set; }

        public static LoginResult Success(AppUser user) => new LoginResult { Status = LoginStatus.Success, User = user };
        public static LoginResult Invalid(int failedCount = 0) => new LoginResult { Status = LoginStatus.InvalidCredentials, FailedLoginCount = failedCount };
        public static LoginResult Locked(DateTime? until) => new LoginResult { Status = LoginStatus.LockedOut, LockoutUntil = until };
        public static LoginResult Inactive() => new LoginResult { Status = LoginStatus.Inactive };
    }
}
