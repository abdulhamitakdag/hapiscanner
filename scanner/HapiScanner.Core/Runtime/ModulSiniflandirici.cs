namespace HapiScanner.Core.Runtime;

public enum ModulSinifi
{

    Sistem,

    JavaCalismaOrtami,

    OyunKurulumu,

    BilinenMesruYazilim,

    KullaniciYazilabilir,

    Bilinmeyen,
}

public static class ModulSiniflandirici
{
    private static readonly string[] SistemYollari =
    {
        @"\windows\system32", @"\windows\syswow64", @"\windows\winsxs",
        @"\windows\assembly", @"\windows\microsoft.net", @"\windows\systemapps",
    };

    private static readonly string[] JavaYolIsaretleri =
    {
        @"\jre\bin", @"\jdk\bin", @"\bin\server", @"\bin\client",
        @"\java\", @"\runtime\", @"\zulu", @"\temurin", @"\graalvm", @"\microsoft-jdk",
    };

    private static readonly string[] OyunYolIsaretleri =
    {
        @"\.minecraft", @"\minecraft\", @"\multimc", @"\prismlauncher", @"\atlauncher",
        @"\curseforge", @"\modrinth", @"\natives", @"\bin\natives",
        @"\minecraftlauncher", @"\xboxgames\minecraft",
    };

    private static readonly string[] MesruModulAdlari =
    {
        "discord_overlay", "discord_hook", "discordhook", "overlay64", "overlay32",
        "obs-hook", "graphics-hook", "obs-vulkan", "rtsshooks", "nvoglv", "nvspcap",
        "nvcuda", "atioglxx", "amdihk", "amfrt", "igd10iumd", "opengl32sw",
        "steam_api", "gameoverlayrenderer", "galaxy", "epicoverlay", "eossdk",
        "rzchromasdk", "lgs_hook", "logihooks", "sysinfo", "ccl", "msdia",
        "aswhook", "avghookx", "bdhkm", "sbiedll", "kasperskyhook", "mcafeehook",
        "nahimicosd", "ssoverlay", "xinput", "dxgi", "d3d11", "d3d9", "vulkan-1",
        "lwjgl", "lwjgl64", "openal", "openal64", "glfw", "jemalloc", "sqlite-jni",
        "jawt", "awt", "fontmanager", "freetype", "javaaccessbridge", "jsound",
        "management", "net", "nio", "verify", "zip", "jvm", "msvcp", "vcruntime",
        "ucrtbase", "api-ms-win",
    };

    private static readonly string[] MesruImzaSahipleri =
    {
        "microsoft", "oracle", "azul systems", "eclipse", "amazon", "nvidia", "amd",
        "intel", "advanced micro devices", "valve", "discord", "epic games",
        "logitech", "razer", "realtek", "mojang", "red hat", "bellsoft", "google",
        "avast", "avg", "bitdefender", "kaspersky", "mcafee", "eset", "malwarebytes",
        "obs project", "guru3d", "steelseries", "corsair",
    };

    private static readonly string[] KullaniciYazilabilirIsaretler =
    {
        @"\downloads", @"\indirilenler", @"\desktop", @"\masaüstü", @"\masaustu",
        @"\appdata\local\temp", @"\windows\temp", @"\temp\", @"\tmp\",
        @"\users\public", @"\recycle.bin", @"\onedrive\belgeler", @"\documents\",
    };

    public static ModulSinifi Siniflandir(YuklenenModul modul)
    {
        var yol = (modul.Yol ?? string.Empty).Replace('/', '\\').ToLowerInvariant();
        var ad = Path.GetFileNameWithoutExtension(modul.Ad ?? string.Empty).ToLowerInvariant();

        if (MesruAdMi(ad) || MesruImzaMi(modul)) return ModulSinifi.BilinenMesruYazilim;

        if (yol.Length == 0) return ModulSinifi.Bilinmeyen;
        if (SistemYollari.Any(yol.Contains)) return ModulSinifi.Sistem;
        if (JavaYolIsaretleri.Any(yol.Contains)) return ModulSinifi.JavaCalismaOrtami;
        if (OyunYolIsaretleri.Any(yol.Contains)) return ModulSinifi.OyunKurulumu;
        if (KullaniciYazilabilirIsaretler.Any(yol.Contains)) return ModulSinifi.KullaniciYazilabilir;

        if (yol.Contains(@"\program files")) return ModulSinifi.BilinenMesruYazilim;

        return ModulSinifi.Bilinmeyen;
    }

    public static RuntimeAnomali? Degerlendir(YuklenenModul modul)
    {
        var sinif = Siniflandir(modul);

        switch (sinif)
        {
            case ModulSinifi.Sistem:
            case ModulSinifi.JavaCalismaOrtami:
            case ModulSinifi.OyunKurulumu:
            case ModulSinifi.BilinenMesruYazilim:
                return null;

            case ModulSinifi.KullaniciYazilabilir:
                return RuntimeAnomali.Olustur(AnomaliTuru.KullaniciYazilabilirYoldanModul,
                    modul.Ad,
                    "Oyun process'ine kullanicinin yazabildigi bir dizinden modul yuklenmis.");

            default:
                return modul.ImzaGecerliMi
                    ? null
                    : RuntimeAnomali.Olustur(AnomaliTuru.ImzasizModul, modul.Ad,
                        "Tanimlanamayan konumdan imzasiz modul yuklenmis.");
        }
    }

    private static bool MesruAdMi(string ad)
        => ad.Length > 0 && MesruModulAdlari.Any(m => ad.StartsWith(m, StringComparison.Ordinal));

    private static bool MesruImzaMi(YuklenenModul modul)
    {
        if (!modul.ImzaGecerliMi || string.IsNullOrWhiteSpace(modul.ImzaSahibi)) return false;
        var sahip = modul.ImzaSahibi.ToLowerInvariant();
        return MesruImzaSahipleri.Any(sahip.Contains);
    }
}
