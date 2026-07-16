using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace QuanLyHangHoa.Updates;

public sealed record AuthenticodeVerificationResult(
    bool SignatureValid,
    bool ChainValid,
    bool TimestampValid,
    string Thumbprint);

public interface IAuthenticodeVerifier
{
    AuthenticodeVerificationResult Verify(string filePath);
}

public sealed class AuthenticodeVerifier : IAuthenticodeVerifier
{
    private static readonly Guid GenericVerifyV2 =
        new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
    private const uint StateActionVerify = 1;
    private const uint StateActionClose = 2;

    public AuthenticodeVerificationResult Verify(string filePath)
    {
        // dùng chính chính sách Authenticode của Windows để kiểm tra chain và thu hồi.
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be empty.", nameof(filePath));
        }

        var fileInfo = new WinTrustFileInfo(filePath);
        var fileInfoPointer = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>());
        try
        {
            Marshal.StructureToPtr(fileInfo, fileInfoPointer, fDeleteOld: false);
            var trustData = new WinTrustData(fileInfoPointer);
            try
            {
                var action = GenericVerifyV2;
                // giữ StateData đến lúc đọc countersigner, rồi đóng để WinTrust trả bộ nhớ.
                var trustStatus = WinVerifyTrust(new IntPtr(-1), ref action, ref trustData);
                var trusted = trustStatus == 0;
                var thumbprint = ReadSignerThumbprint(filePath);

                return new AuthenticodeVerificationResult(
                    SignatureValid: trusted,
                    ChainValid: trusted,
                    TimestampValid: trusted && HasTrustedTimestamp(trustData.StateData),
                    Thumbprint: thumbprint);
            }
            finally
            {
                if (trustData.StateData != IntPtr.Zero)
                {
                    trustData.StateAction = StateActionClose;
                    var action = GenericVerifyV2;
                    _ = WinVerifyTrust(new IntPtr(-1), ref action, ref trustData);
                }
            }
        }
        finally
        {
            Marshal.DestroyStructure<WinTrustFileInfo>(fileInfoPointer);
            Marshal.FreeHGlobal(fileInfoPointer);
        }
    }

    private static bool HasTrustedTimestamp(IntPtr stateData)
    {
        // timestamp phải có countersigner không lỗi và có certificate chain.
        // chỉ nhìn ngày ký của certificate chính chưa chứng minh timestamp tin cậy.
        if (stateData == IntPtr.Zero)
        {
            return false;
        }

        var providerData = WTHelperProvDataFromStateData(stateData);
        if (providerData == IntPtr.Zero)
        {
            return false;
        }

        var signerPointer = WTHelperGetProvSignerFromChain(
            providerData, signerIndex: 0, counterSigner: false, counterSignerIndex: 0);
        if (signerPointer == IntPtr.Zero)
        {
            return false;
        }

        var signer = Marshal.PtrToStructure<CryptProviderSigner>(signerPointer);
        var counterSignerPointer = WTHelperGetProvSignerFromChain(
            providerData, signerIndex: 0, counterSigner: true, counterSignerIndex: 0);
        if (signer.CounterSignerCount == 0 || counterSignerPointer == IntPtr.Zero)
        {
            return false;
        }

        var counterSigner = Marshal.PtrToStructure<CryptProviderSigner>(counterSignerPointer);
        return counterSigner.Error == 0 && counterSigner.CertificateChainCount > 0;
    }

    private static string ReadSignerThumbprint(string filePath)
    {
        try
        {
            using var certificate = new X509Certificate2(
                X509Certificate.CreateFromSignedFile(filePath));
            return certificate.Thumbprint ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = true)]
    private static extern int WinVerifyTrust(
        IntPtr windowHandle,
        [In] ref Guid actionId,
        [In] ref WinTrustData trustData);

    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern IntPtr WTHelperProvDataFromStateData(IntPtr stateData);

    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern IntPtr WTHelperGetProvSignerFromChain(
        IntPtr providerData,
        uint signerIndex,
        [MarshalAs(UnmanagedType.Bool)] bool counterSigner,
        uint counterSignerIndex);

    [StructLayout(LayoutKind.Sequential)]
    private struct CryptProviderSigner
    {
        public uint StructureSize;
        public System.Runtime.InteropServices.ComTypes.FILETIME VerifyAsOf;
        public uint CertificateChainCount;
        public IntPtr CertificateChain;
        public uint SignerType;
        public IntPtr SignerInfo;
        public uint Error;
        public uint CounterSignerCount;
        public IntPtr CounterSigners;
        public IntPtr ChainContext;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class WinTrustFileInfo
    {
        public WinTrustFileInfo(string filePath)
        {
            StructureSize = (uint)Marshal.SizeOf<WinTrustFileInfo>();
            FilePath = filePath;
        }

        public uint StructureSize;
        public string FilePath;
        public IntPtr FileHandle;
        public IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustData
    {
        public WinTrustData(IntPtr fileInfoPointer)
        {
            StructureSize = (uint)Marshal.SizeOf<WinTrustData>();
            PolicyCallbackData = IntPtr.Zero;
            SipClientData = IntPtr.Zero;
            UiChoice = 2;
            RevocationChecks = 1;
            UnionChoice = 1;
            FileInfoPointer = fileInfoPointer;
            StateAction = StateActionVerify;
            StateData = IntPtr.Zero;
            UrlReference = IntPtr.Zero;
            ProviderFlags = 0x00000040;
            UiContext = 0;
            SignatureSettings = IntPtr.Zero;
        }

        public uint StructureSize;
        public IntPtr PolicyCallbackData;
        public IntPtr SipClientData;
        public uint UiChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr FileInfoPointer;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr UrlReference;
        public uint ProviderFlags;
        public uint UiContext;
        public IntPtr SignatureSettings;
    }
}
