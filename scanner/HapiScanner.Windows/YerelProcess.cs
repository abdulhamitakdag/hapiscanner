using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace HapiScanner.Windows;

internal static class YerelProcess
{
    internal const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    internal const uint PROCESS_VM_READ = 0x0010;

    internal const uint LIST_MODULES_ALL = 0x03;

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern SafeProcessHandle OpenProcess(
        uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool IsWow64Process(SafeProcessHandle hProcess, out bool Wow64Process);

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool ReadProcessMemory(
        SafeProcessHandle hProcess, IntPtr lpBaseAddress, byte[] lpBuffer,
        int dwSize, out IntPtr lpNumberOfBytesRead);

    [SupportedOSPlatform("windows")]
    [DllImport("psapi.dll", SetLastError = true)]
    internal static extern bool EnumProcessModulesEx(
        SafeProcessHandle hProcess, [Out] IntPtr[] lphModule, int cb,
        out int lpcbNeeded, uint dwFilterFlag);

    [SupportedOSPlatform("windows")]
    [DllImport("psapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int GetModuleFileNameExW(
        SafeProcessHandle hProcess, IntPtr hModule,
        [Out] char[] lpBaseName, int nSize);

    [SupportedOSPlatform("windows")]
    [DllImport("psapi.dll", SetLastError = true)]
    internal static extern bool GetModuleInformation(
        SafeProcessHandle hProcess, IntPtr hModule, out MODULEINFO lpmodinfo, int cb);

    [StructLayout(LayoutKind.Sequential)]
    internal struct MODULEINFO
    {
        internal IntPtr lpBaseOfDll;
        internal uint SizeOfImage;
        internal IntPtr EntryPoint;
    }

    [SupportedOSPlatform("windows")]
    [DllImport("ntdll.dll")]
    internal static extern int NtQueryInformationProcess(
        SafeProcessHandle processHandle, int processInformationClass,
        byte[] processInformation, int processInformationLength, out int returnLength);

    internal const int PebBaseAddressOfseti = 0x08;

    internal const int ProcessParametersOfseti = 0x20;

    internal const int CommandLineOfseti = 0x70;
}
