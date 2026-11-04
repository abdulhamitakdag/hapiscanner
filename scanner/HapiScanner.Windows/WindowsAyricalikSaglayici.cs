using System.Runtime.Versioning;
using System.Security.Principal;
using HapiScanner.Core.Adli;

namespace HapiScanner.Windows;

public sealed class WindowsAyricalikSaglayici : IAyricalikSaglayici
{
    [SupportedOSPlatform("windows")]
    private static AyricalikDurumu WindowsDurumu()
    {
        using var kimlik = WindowsIdentity.GetCurrent();
        var asil = new WindowsPrincipal(kimlik);
        return asil.IsInRole(WindowsBuiltInRole.Administrator)
            ? AyricalikDurumu.Yonetici
            : AyricalikDurumu.Standart;
    }

    public AyricalikDurumu Durum()
    {
        if (!OperatingSystem.IsWindows()) return AyricalikDurumu.Bilinmeyen;
        try
        {
            return WindowsDurumu();
        }
        catch (Exception hata) when (hata is PlatformNotSupportedException or UnauthorizedAccessException)
        {
            return AyricalikDurumu.Bilinmeyen;
        }
    }
}
