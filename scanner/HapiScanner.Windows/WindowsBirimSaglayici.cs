using HapiScanner.Core.Adli;

namespace HapiScanner.Windows;

public sealed class WindowsBirimSaglayici : IBirimSaglayici
{

    private static readonly string[] IlgiIsaretleri =
    {
        "Users",
        ".minecraft",
        "Minecraft",
        "MultiMC",
        "PrismLauncher",
        "ATLauncher",
        "curseforge",
        "TLauncher",
        "XMRig",
        "Games",
    };

    public IReadOnlyList<BirimBilgisi> Birimler()
    {
        var liste = new List<BirimBilgisi>();

        foreach (var surucu in DriveInfo.GetDrives())
        {
            if (!surucu.IsReady) continue;
            if (surucu.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;

            string bicim;
            try
            {
                bicim = surucu.DriveFormat;
            }
            catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
            {
                bicim = string.Empty;
            }

            liste.Add(new BirimBilgisi(
                surucu.Name,
                bicim,
                Ilgili: IlgiliMi(surucu.RootDirectory.FullName),
                CikarilabilirMi: surucu.DriveType == DriveType.Removable));
        }

        return liste;
    }

    private static bool IlgiliMi(string kok)
    {
        foreach (var isaret in IlgiIsaretleri)
        {
            try
            {
                if (Directory.Exists(Path.Combine(kok, isaret))) return true;
            }
            catch (Exception hata) when (hata is IOException or UnauthorizedAccessException
                                         or ArgumentException)
            {

            }
        }
        return false;
    }
}
