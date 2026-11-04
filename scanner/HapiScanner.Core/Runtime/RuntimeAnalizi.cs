using HapiScanner.Core.Adli;

namespace HapiScanner.Core.Runtime;

public sealed record RuntimeAnalizSonucu(
    RuntimeErisimi Erisim,
    RuntimeProcess? Hedef,
    JvmMetadata Jvm,
    IReadOnlyList<RuntimeAnomali> Anomaliler,
    int IncelenenModulSayisi,
    int DegerlendirilenHariciProcess,
    IReadOnlyList<string> Notlar)
{

    public IReadOnlyDictionary<string, int> AnomaliSayimi()
        => Anomaliler.GroupBy(a => a.Tur.ToString())
                     .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

    public bool AjanVarMi => Anomaliler.Any(a =>
        a.Tur is AnomaliTuru.JavaAjani or AnomaliTuru.NativeAjan);
}

public sealed class RuntimeAnalizi
{
    private readonly IProcessSaglayici _saglayici;
    private readonly Func<RuntimeProcess, bool> _bilinenSupheliImza;

    public RuntimeAnalizi(
        IProcessSaglayici saglayici,
        Func<RuntimeProcess, bool>? bilinenSupheliImza = null)
    {
        _saglayici = saglayici;
        _bilinenSupheliImza = bilinenSupheliImza ?? (_ => false);
    }

    private static readonly string[] KullaniciYazilabilirIsaretler =
    {
        @"\downloads", @"\indirilenler", @"\desktop", @"\masaustu",
        @"\appdata\local\temp", @"\windows\temp", @"\temp\", @"\tmp\",
        @"\users\public", @"\recycle.bin",
    };

    private static readonly string[] OyunYolIsaretleri =
    {
        @"\.minecraft", @"\minecraft", @"\multimc", @"\prismlauncher", @"\atlauncher",
        @"\curseforge", @"\modrinth", @"\program files",
    };

    public RuntimeAnalizSonucu Calistir(int? hedefProcessId)
    {
        var notlar = new List<string>();
        var processler = _saglayici.Processler();

        var hedef = hedefProcessId is { } pid
            ? processler.FirstOrDefault(p => p.ProcessId == pid)
            : null;

        if (hedef is null)
        {
            notlar.Add("HapiCraft baglantisiyla iliskili Minecraft process'i bulunamadi; " +
                       "runtime katmani degerlendirilemedi.");
            return new RuntimeAnalizSonucu(RuntimeErisimi.Yok, null, JvmMetadata.Bos(),
                Array.Empty<RuntimeAnomali>(), 0, 0, notlar);
        }

        var anomaliler = new List<RuntimeAnomali>();

        var jvm = JvmKomutSatiri.Coz(hedef.KomutSatiri);
        if (hedef.KomutSatiri is null)
            notlar.Add("Hedef process'in komut satiri okunamadi; JVM metadata degerlendirilemedi.");

        AjanlariDegerlendir(jvm, anomaliler);
        ClasspathDegerlendir(jvm, anomaliler);

        var moduller = _saglayici.Moduller(hedef.ProcessId);
        if (moduller is null)
            notlar.Add("Hedef process'in modul listesi okunamadi (yetki veya bitlik uyusmazligi); " +
                       "runtime kapsami dusuruldu.");
        else
            foreach (var modul in moduller)
            {
                var anomali = ModulSiniflandirici.Degerlendir(modul);
                if (anomali is not null) anomaliler.Add(anomali);
            }

        var hariciler = HaricileriDegerlendir(processler, hedef, anomaliler);

        var erisim = ErisimHesapla(moduller, hedef.KomutSatiri);

        return new RuntimeAnalizSonucu(erisim, hedef, jvm, anomaliler,
            moduller?.Count ?? 0, hariciler, notlar);
    }

    private static void AjanlariDegerlendir(JvmMetadata jvm, List<RuntimeAnomali> anomaliler)
    {
        foreach (var ajan in jvm.JavaAjanlari)
            anomaliler.Add(RuntimeAnomali.Olustur(AnomaliTuru.JavaAjani, DosyaAdi(ajan),
                "JVM'e -javaagent ile bir ajan takilmis."));

        foreach (var ajan in jvm.NativeAjanlar)
            anomaliler.Add(RuntimeAnomali.Olustur(AnomaliTuru.NativeAjan, DosyaAdi(ajan),
                "JVM'e native (JVMTI) ajan takilmis."));

        if (jvm.AttachMekanizmasiKapali)
            anomaliler.Add(RuntimeAnomali.Olustur(AnomaliTuru.AttachMekanizmasiKapali,
                "DisableAttachMechanism",
                "JVM attach mekanizmasi kapatilmis; runtime incelemesi kisitlanmis."));
    }

    private static void ClasspathDegerlendir(JvmMetadata jvm, List<RuntimeAnomali> anomaliler)
    {
        foreach (var girdi in jvm.ClasspathGirdileri)
        {
            var yol = girdi.Replace('/', '\\').ToLowerInvariant();
            if (OyunYolIsaretleri.Any(yol.Contains)) continue;
            if (!KullaniciYazilabilirIsaretler.Any(yol.Contains)) continue;

            anomaliler.Add(RuntimeAnomali.Olustur(AnomaliTuru.SiradisiClasspath, DosyaAdi(girdi),
                "Classpath'te oyun kurulumu disindaki, kullanicinin yazabildigi bir konumdan jar var."));
        }
    }

    private int HaricileriDegerlendir(
        IReadOnlyList<RuntimeProcess> processler, RuntimeProcess hedef, List<RuntimeAnomali> anomaliler)
    {
        var sayac = 0;

        foreach (var p in processler)
        {
            if (p.ProcessId == hedef.ProcessId) continue;
            sayac++;

            if (_bilinenSupheliImza(p))
            {
                anomaliler.Add(RuntimeAnomali.Olustur(AnomaliTuru.HariciProcessIliskisi, p.Ad,
                    "Kural paketinde tanimli bilinen imzayla eslesen harici process calisiyor."));
                continue;
            }

            if (TaklitMi(p))
                anomaliler.Add(RuntimeAnomali.Olustur(AnomaliTuru.HariciProcessIliskisi, p.Ad,
                    "Kendini Java/Minecraft bileseni gibi gosteren, kullanici yazilabilir " +
                    "dizinden calisan process."));
        }

        return sayac;
    }

    private static bool TaklitMi(RuntimeProcess p)
    {
        var ad = Path.GetFileNameWithoutExtension(p.Ad ?? string.Empty).ToLowerInvariant();
        if (ad is not ("javaw" or "java" or "minecraft" or "minecraftlauncher" or "jvm")) return false;

        var yol = (p.Yol ?? string.Empty).Replace('/', '\\').ToLowerInvariant();
        if (yol.Length == 0) return false;
        return KullaniciYazilabilirIsaretler.Any(yol.Contains);
    }

    private static RuntimeErisimi ErisimHesapla(IReadOnlyList<YuklenenModul>? moduller, string? komutSatiri)
    {
        if (moduller is null && komutSatiri is null) return RuntimeErisimi.Yok;
        if (moduller is null || komutSatiri is null) return RuntimeErisimi.Sinirli;
        return moduller.Count > 0 ? RuntimeErisimi.Tam : RuntimeErisimi.Sinirli;
    }

    private static string DosyaAdi(string yol)
    {
        var temiz = yol.Replace('/', '\\').TrimEnd('\\');
        var ad = temiz.Split('\\').LastOrDefault();
        return string.IsNullOrWhiteSpace(ad) ? yol : ad;
    }
}
