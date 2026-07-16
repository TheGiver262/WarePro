using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Data.SqlClient;

namespace QuanLyHangHoa.Configuration;

public interface ISqlCredentialStore
{
    SqlCredential? Read();
    void Write(SqlCredential credential);
    void Delete();
}

public sealed class SqlCredentialStore : ISqlCredentialStore
{
    public const string CredentialTarget = "WarePro/Database";

    private const uint GenericCredential = 1;
    private const uint PersistOnLocalMachine = 2;
    private const int ErrorNotFound = 1168;

    public SqlCredential? Read()
    {
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
            if (native.CredentialBlobSize % sizeof(char) != 0)
            {
                throw new InvalidOperationException("Stored WarePro credential has an invalid size.");
            }

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
                Array.Clear(bytes);
                Array.Clear(characters);
            }
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    public void Write(SqlCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
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
            Marshal.ZeroFreeGlobalAllocUnicode(passwordPointer);
        }
    }

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
