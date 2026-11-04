using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace HapiScanner.Windows;

internal static class Yerel
{
    internal const uint FSCTL_QUERY_USN_JOURNAL = 0x000900f4;
    internal const uint FSCTL_READ_USN_JOURNAL = 0x000900bb;
    internal const uint FSCTL_ENUM_USN_DATA = 0x000900b3;

    internal const uint USN_REASON_DATA_OVERWRITE = 0x00000001;
    internal const uint USN_REASON_DATA_EXTEND = 0x00000002;
    internal const uint USN_REASON_DATA_TRUNCATION = 0x00000004;
    internal const uint USN_REASON_FILE_CREATE = 0x00000100;
    internal const uint USN_REASON_FILE_DELETE = 0x00000200;
    internal const uint USN_REASON_RENAME_OLD_NAME = 0x00001000;
    internal const uint USN_REASON_RENAME_NEW_NAME = 0x00002000;

    internal const int ERROR_ACCESS_DENIED = 5;
    internal const int ERROR_INVALID_FUNCTION = 1;
    internal const int ERROR_NOT_SUPPORTED = 50;
    internal const int ERROR_JOURNAL_DELETE_IN_PROGRESS = 1178;
    internal const int ERROR_JOURNAL_NOT_ACTIVE = 1179;
    internal const int ERROR_JOURNAL_ENTRY_DELETED = 1181;
    internal const int ERROR_HANDLE_EOF = 38;

    private const uint GENERIC_READ = 0x80000000;
    private const uint FILE_SHARE_READ = 0x1;
    private const uint FILE_SHARE_WRITE = 0x2;
    private const uint OPEN_EXISTING = 3;

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
        uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice, uint dwIoControlCode,
        byte[]? lpInBuffer, int nInBufferSize,
        byte[]? lpOutBuffer, int nOutBufferSize,
        out int lpBytesReturned, IntPtr lpOverlapped);

    [SupportedOSPlatform("windows")]
    internal static SafeFileHandle BirimAc(string kok)
    {
        var harf = kok.TrimEnd('\\', '/');
        var yol = $@"\\.\{harf}";
        return CreateFileW(yol, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
    }

    [SupportedOSPlatform("windows")]
    internal static bool Ioctl(SafeFileHandle handle, uint kod, byte[]? giris, byte[] cikis,
        out int donenBayt, out int hataKodu)
    {
        var sonuc = DeviceIoControl(handle, kod, giris, giris?.Length ?? 0,
            cikis, cikis.Length, out donenBayt, IntPtr.Zero);
        hataKodu = sonuc ? 0 : Marshal.GetLastWin32Error();
        return sonuc;
    }
}
