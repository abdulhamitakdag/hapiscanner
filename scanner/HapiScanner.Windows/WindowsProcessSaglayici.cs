using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;
using HapiScanner.Core.Runtime;
using Microsoft.Win32.SafeHandles;

namespace HapiScanner.Windows;

public sealed class WindowsProcessSaglayici : IProcessSaglayici
{

    public int AzamiModul { get; init; } = 1024;

    public IReadOnlyList<RuntimeProcess> Processler()
    {
        var liste = new List<RuntimeProcess>();

        foreach (var p in Process.GetProcesses())
        {
            try
            {
                liste.Add(new RuntimeProcess(
                    p.Id,
                    p.ProcessName,
                    Yol(p),
                    OperatingSystem.IsWindows() ? KomutSatiri(p.Id) : null,
                    BaslangicZamani(p)));
            }
            catch (Exception hata) when (hata is InvalidOperationException or Win32Exception
                                         or NotSupportedException)
            {

            }
            finally
            {
                p.Dispose();
            }
        }

        return liste;
    }

    private static string? Yol(Process p)
    {
        try
        {
            return p.MainModule?.FileName;
        }
        catch (Exception hata) when (hata is InvalidOperationException or Win32Exception
                                     or NotSupportedException)
        {

            return null;
        }
    }

    private static DateTimeOffset? BaslangicZamani(Process p)
    {
        try
        {
            return p.StartTime.ToUniversalTime();
        }
        catch (Exception hata) when (hata is InvalidOperationException or Win32Exception)
        {
            return null;
        }
    }

    public IReadOnlyList<YuklenenModul>? Moduller(int processId)
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            return WindowsModuller(processId);
        }
        catch (Exception)
        {
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private IReadOnlyList<YuklenenModul>? WindowsModuller(int processId)
    {
        using var handle = YerelProcess.OpenProcess(
            YerelProcess.PROCESS_QUERY_LIMITED_INFORMATION | YerelProcess.PROCESS_VM_READ,
            false, processId);
        if (handle.IsInvalid) return null;

        var tanitici = new IntPtr[AzamiModul];
        if (!YerelProcess.EnumProcessModulesEx(handle, tanitici,
                tanitici.Length * IntPtr.Size, out var gerekli, YerelProcess.LIST_MODULES_ALL))
            return null;

        var sayi = Math.Min(gerekli / IntPtr.Size, AzamiModul);
        var moduller = new List<YuklenenModul>(sayi);
        var tampon = new char[1024];

        for (var i = 0; i < sayi; i++)
        {
            var uzunluk = YerelProcess.GetModuleFileNameExW(handle, tanitici[i], tampon, tampon.Length);
            if (uzunluk <= 0) continue;

            var yol = new string(tampon, 0, uzunluk);
            var boyut = YerelProcess.GetModuleInformation(handle, tanitici[i],
                out var bilgi, Marshal.SizeOf<YerelProcess.MODULEINFO>())
                ? bilgi.SizeOfImage
                : 0;

            var (sahip, gecerli) = ImzaBilgisi(yol);
            moduller.Add(new YuklenenModul(Path.GetFileName(yol), yol, sahip, gecerli, boyut));
        }

        return moduller;
    }

    [SupportedOSPlatform("windows")]
    private static (string? Sahip, bool Gecerli) ImzaBilgisi(string yol)
    {
        try
        {
            using var sertifika = new X509Certificate2(X509Certificate.CreateFromSignedFile(yol));
            return (sertifika.GetNameInfo(X509NameType.SimpleName, false), true);
        }
        catch (Exception)
        {
            return (null, false);
        }
    }

    [SupportedOSPlatform("windows")]
    private static string? KomutSatiri(int processId)
    {
        using var handle = YerelProcess.OpenProcess(
            YerelProcess.PROCESS_QUERY_LIMITED_INFORMATION | YerelProcess.PROCESS_VM_READ,
            false, processId);
        if (handle.IsInvalid) return null;

        if (IntPtr.Size != 8) return null;
        if (YerelProcess.IsWow64Process(handle, out var wow64) && wow64) return null;

        var bilgi = new byte[48];
        if (YerelProcess.NtQueryInformationProcess(handle, 0, bilgi, bilgi.Length, out _) != 0)
            return null;

        var pebAdresi = OkuIsaretci(bilgi, YerelProcess.PebBaseAddressOfseti);
        if (pebAdresi == IntPtr.Zero) return null;

        var parametreAdresi = IsaretciOku(handle, pebAdresi + YerelProcess.ProcessParametersOfseti);
        if (parametreAdresi == IntPtr.Zero) return null;

        var unicodeString = new byte[16];
        if (!Oku(handle, parametreAdresi + YerelProcess.CommandLineOfseti, unicodeString))
            return null;

        var uzunluk = BitConverter.ToUInt16(unicodeString, 0);
        if (uzunluk == 0 || uzunluk > 32 * 1024) return null;

        var bufferAdresi = OkuIsaretci(unicodeString, 8);
        if (bufferAdresi == IntPtr.Zero) return null;

        var metin = new byte[uzunluk];
        if (!Oku(handle, bufferAdresi, metin)) return null;

        return System.Text.Encoding.Unicode.GetString(metin);
    }

    [SupportedOSPlatform("windows")]
    private static bool Oku(SafeProcessHandle handle, IntPtr adres, byte[] hedef)
        => YerelProcess.ReadProcessMemory(handle, adres, hedef, hedef.Length, out var okunan)
           && okunan.ToInt64() == hedef.Length;

    [SupportedOSPlatform("windows")]
    private static IntPtr IsaretciOku(SafeProcessHandle handle, IntPtr adres)
    {
        var tampon = new byte[8];
        return Oku(handle, adres, tampon) ? OkuIsaretci(tampon, 0) : IntPtr.Zero;
    }

    private static IntPtr OkuIsaretci(byte[] tampon, int ofset)
        => tampon.Length >= ofset + 8
            ? new IntPtr(BitConverter.ToInt64(tampon, ofset))
            : IntPtr.Zero;
}
