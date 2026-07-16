using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Data.SqlClient;

namespace QuanLyHangHoa.Configuration;

/// <summary>
/// ranh giới lưu trữ credential để logic startup không phụ thuộc trực tiếp vào API Windows.
/// </summary>
public interface ISqlCredentialStore
{
    SqlCredential? Read();
    void Write(SqlCredential credential);
    void Delete();
}

/// <summary>
/// lưu credential theo tài khoản Windows hiện tại bằng Windows Credential Manager.
/// </summary>
public sealed class SqlCredentialStore : ISqlCredentialStore
{
    public const string CredentialTarget = "WarePro/Database";

    private const uint GenericCredential = 1;
    private const uint PersistOnLocalMachine = 2;
    private const int ErrorNotFound = 1168;

    public SqlCredential? Read()
    {
        // CredRead cấp phát vùng nhớ native; nếu đọc thành công thì mọi nhánh đều phải đi qua CredFree.
        if (!CredRead(CredentialTarget, GenericCredential, 0, out var credentialPointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
            {
                return null;
            }

            throw new Win32Exception(error);
        }

        try
        {
            var native = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            // mật khẩu được lưu dạng UTF-16 nên kích thước blob phải chia hết cho kích thước char.
            if (native.CredentialBlobSize % sizeof(char) != 0)
            {
                throw new InvalidOperationException("Stored WarePro credential has an invalid size.");
            }

            // hai mảng này chỉ là vùng đệm chuyển đổi và phải được xóa trước khi phương thức kết thúc.
            var bytes = new byte[native.CredentialBlobSize];
            var characters = new char[native.CredentialBlobSize / sizeof(char)];
            try
            {
                if (bytes.Length > 0)
                {
                    Marshal.Copy(native.CredentialBlob, bytes, 0, bytes.Length);
                    Buffer.BlockCopy(bytes, 0, characters, 0, bytes.Length);
                }

                var password = new SecureString();
                foreach (var character in characters)
                {
                    password.AppendChar(character);
                }

                password.MakeReadOnly();
                return new SqlCredential(native.UserName ?? string.Empty, password);
            }
            finally
            {
                // GC không đảm bảo thời điểm thu hồi, vì vậy xóa ngay các bản sao managed của mật khẩu.
                Array.Clear(bytes);
                Array.Clear(characters);
            }
        }
        finally
        {
            // con trỏ và blob gốc thuộc quyền sở hữu của Credential Manager.
            CredFree(credentialPointer);
        }
    }

    public void Write(SqlCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        // SecureString chỉ được mở tại ranh giới P/Invoke cần con trỏ mật khẩu UTF-16.
        var passwordPointer = Marshal.SecureStringToGlobalAllocUnicode(credential.Password);
        try
        {
            var native = new NativeCredential
            {
                Type = GenericCredential,
                TargetName = CredentialTarget,
                CredentialBlobSize = checked((uint)(credential.Password.Length * sizeof(char))),
                CredentialBlob = passwordPointer,
                Persist = PersistOnLocalMachine,
                UserName = credential.UserId
            };

            if (!CredWrite(ref native, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        finally
        {
            // vừa xóa nội dung vừa giải phóng vùng nhớ native, kể cả khi CredWrite trả lỗi.
            Marshal.ZeroFreeGlobalAllocUnicode(passwordPointer);
        }
    }

    // thao tác xóa có tính lặp lại: credential không tồn tại vẫn được xem là trạng thái mong muốn.
    public void Delete()
    {
        if (CredDelete(CredentialTarget, GenericCredential, 0))
        {
            return;
        }

        var error = Marshal.GetLastWin32Error();
        if (error != ErrorNotFound)
        {
            throw new Win32Exception(error);
        }
    }

    // bố cục và chữ ký dưới đây phải khớp Win32 Credential API; không đổi thứ tự các trường.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public string? TargetName;
        public string? Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref NativeCredential credential, uint flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("Advapi32.dll")]
    private static extern void CredFree(IntPtr credential);
}
