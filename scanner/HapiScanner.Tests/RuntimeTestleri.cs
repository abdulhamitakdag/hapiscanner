using HapiScanner.Core.Adli;
using HapiScanner.Core.Runtime;
using HapiScanner.Core.Tespit;

namespace HapiScanner.Tests;

internal sealed class SahteProcessSaglayici : IProcessSaglayici
{
    private readonly IReadOnlyList<RuntimeProcess> _processler;
    private readonly IReadOnlyList<YuklenenModul>? _moduller;

    public SahteProcessSaglayici(
        IReadOnlyList<RuntimeProcess> processler,
        IReadOnlyList<YuklenenModul>? moduller = null)
    {
        _processler = processler;
        _moduller = moduller;
    }

    public IReadOnlyList<RuntimeProcess> Processler() => _processler;
    public IReadOnlyList<YuklenenModul>? Moduller(int processId) => _moduller;
}

internal static class RuntimeYardimci
{
    internal const string GercekciKomut =
        @"C:\Users\oyuncu\AppData\Roaming\.minecraft\runtime\java-runtime-delta\bin\javaw.exe " +
        @"-Xmx4G -XX:+UseG1GC -Djava.library.path=""C:\Users\oyuncu\AppData\Roaming\.minecraft\bin\natives"" " +
        @"-cp ""C:\Users\oyuncu\AppData\Roaming\.minecraft\libraries\lwjgl.jar;" +
        @"C:\Users\oyuncu\AppData\Roaming\.minecraft\versions\1.21\1.21.jar"" " +
        @"net.minecraft.client.main.Main --username Oyuncu";

    internal static RuntimeProcess Hedef(string? komut = GercekciKomut, int pid = 4242)
        => new(pid, "javaw",
            @"C:\Users\oyuncu\AppData\Roaming\.minecraft\runtime\java-runtime-delta\bin\javaw.exe",
            komut, DateTimeOffset.UtcNow);

    internal static YuklenenModul Modul(string ad, string? yol, string? sahip = null, bool imza = false)
        => new(ad, yol, sahip, imza, 1024);

    internal static List<YuklenenModul> MesruModuller() => new()
    {
        Modul("ntdll.dll", @"C:\Windows\System32\ntdll.dll", "Microsoft Windows", true),
        Modul("kernel32.dll", @"C:\Windows\System32\kernel32.dll", "Microsoft Windows", true),
        Modul("jvm.dll", @"C:\Users\o\AppData\Roaming\.minecraft\runtime\java-runtime-delta\bin\server\jvm.dll"),
        Modul("lwjgl64.dll", @"C:\Users\o\AppData\Roaming\.minecraft\bin\natives\lwjgl64.dll"),
        Modul("OpenAL64.dll", @"C:\Users\o\AppData\Roaming\.minecraft\bin\natives\OpenAL64.dll"),

        Modul("DiscordHook64.dll", @"C:\Users\o\AppData\Local\Discord\app-1.0\DiscordHook64.dll",
            "Discord Inc.", true),
        Modul("graphics-hook64.dll", @"C:\Program Files\obs-studio\data\obs-plugins\graphics-hook64.dll",
            "OBS Project", true),
        Modul("nvoglv64.dll", @"C:\Windows\System32\DriverStore\nvoglv64.dll", "NVIDIA Corporation", true),
        Modul("aswhook.dll", @"C:\Program Files\Avast Software\aswhook.dll", "Avast Software", true),
        Modul("RTSSHooks64.dll", @"C:\Program Files (x86)\RivaTuner\RTSSHooks64.dll", "Guru3D", true),
    };
}

public class JvmKomutSatiriTestleri
{
    [Fact]
    public void GercekciLauncherKomutuCozulur()
    {
        var jvm = JvmKomutSatiri.Coz(RuntimeYardimci.GercekciKomut);

        Assert.Contains("-Xmx4G", jvm.Argumanlar);
        Assert.Equal(2, jvm.ClasspathGirdileri.Count);
        Assert.Contains(jvm.ClasspathGirdileri, g => g.EndsWith("1.21.jar"));
        Assert.Empty(jvm.JavaAjanlari);
        Assert.Empty(jvm.NativeAjanlar);
        Assert.False(jvm.AjanVarMi);
    }

    [Fact]
    public void JavaAjaniYakalanir()
    {
        var jvm = JvmKomutSatiri.Coz(@"javaw.exe -javaagent:C:\Users\o\Downloads\ajan.jar=gizli -Xmx2G");

        Assert.Equal(@"C:\Users\o\Downloads\ajan.jar", Assert.Single(jvm.JavaAjanlari));
        Assert.True(jvm.AjanVarMi);
    }

    [Theory]
    [InlineData("-agentpath:")]
    [InlineData("-agentlib:")]
    public void NativeAjanYakalanir(string onek)
    {
        var jvm = JvmKomutSatiri.Coz($@"javaw.exe {onek}C:\Temp\jvmti.dll");
        Assert.Equal(@"C:\Temp\jvmti.dll", Assert.Single(jvm.NativeAjanlar));
    }

    [Fact]
    public void TirnakliYollardakiBosluklarBozulmaz()
    {
        var jvm = JvmKomutSatiri.Coz(@"javaw.exe -cp ""C:\Program Files\oyun\a.jar;C:\yol b\c.jar""");

        Assert.Equal(2, jvm.ClasspathGirdileri.Count);
        Assert.Contains(@"C:\Program Files\oyun\a.jar", jvm.ClasspathGirdileri);
        Assert.Contains(@"C:\yol b\c.jar", jvm.ClasspathGirdileri);
    }

    [Fact]
    public void AttachMekanizmasiKapatilmasiGorulur()
    {
        Assert.True(JvmKomutSatiri.Coz("javaw.exe -XX:+DisableAttachMechanism").AttachMekanizmasiKapali);
        Assert.False(JvmKomutSatiri.Coz("javaw.exe -XX:-DisableAttachMechanism").AttachMekanizmasiKapali);
    }

    [Fact]
    public void DjavaClassPathDaCozulur()
    {
        var jvm = JvmKomutSatiri.Coz(@"javaw.exe -Djava.class.path=C:\oyun\a.jar;C:\oyun\b.jar");
        Assert.Equal(2, jvm.ClasspathGirdileri.Count);
    }

    [Fact]
    public void BosVeyaNullKomutCokmez()
    {
        Assert.Empty(JvmKomutSatiri.Coz(null).Argumanlar);
        Assert.Empty(JvmKomutSatiri.Coz("   ").ClasspathGirdileri);
    }
}

public class ModulSiniflandiriciTestleri
{
    [Fact]
    public void MesruModullerAnomaliUretmez()
    {
        foreach (var modul in RuntimeYardimci.MesruModuller())
        {
            var anomali = ModulSiniflandirici.Degerlendir(modul);
            Assert.Null(anomali);
        }
    }

    [Fact]
    public void OyununImzasizNativeleriAnomaliDegil()
    {
        var modul = RuntimeYardimci.Modul("lwjgl64.dll",
            @"C:\Users\o\AppData\Roaming\.minecraft\bin\natives\lwjgl64.dll");

        Assert.Equal(ModulSinifi.BilinenMesruYazilim, ModulSiniflandirici.Siniflandir(modul));
        Assert.Null(ModulSiniflandirici.Degerlendir(modul));
    }

    [Fact]
    public void IndirilenlerdenYuklenenModulAnomali()
    {
        var modul = RuntimeYardimci.Modul("bilinmeyen.dll", @"C:\Users\o\Downloads\bilinmeyen.dll");
        var anomali = ModulSiniflandirici.Degerlendir(modul);

        Assert.NotNull(anomali);
        Assert.Equal(AnomaliTuru.KullaniciYazilabilirYoldanModul, anomali!.Tur);
    }

    [Fact]
    public void TempDizininiAnomaliSayar()
    {
        var modul = RuntimeYardimci.Modul("x.dll", @"C:\Users\o\AppData\Local\Temp\x.dll");
        Assert.Equal(AnomaliTuru.KullaniciYazilabilirYoldanModul,
            ModulSiniflandirici.Degerlendir(modul)!.Tur);
    }

    [Fact]
    public void TanimsizKonumdanImzasizModulAnomali()
    {
        var modul = RuntimeYardimci.Modul("acayip.dll", @"D:\baskayer\acayip.dll");
        Assert.Equal(AnomaliTuru.ImzasizModul, ModulSiniflandirici.Degerlendir(modul)!.Tur);
    }

    [Fact]
    public void TanimsizKonumdanImzaliModulAnomaliDegil()
    {
        var modul = RuntimeYardimci.Modul("araç.dll", @"D:\araclar\araç.dll", "Bir Sirket", imza: true);
        Assert.Null(ModulSiniflandirici.Degerlendir(modul));
    }

    [Fact]
    public void YolBilinmiyorsaTahminYurutulmez()
    {
        var modul = RuntimeYardimci.Modul("gizli.dll", null);
        var anomali = ModulSiniflandirici.Degerlendir(modul);
        Assert.Equal(AnomaliTuru.ImzasizModul, anomali!.Tur);
    }
}

public class RuntimeAnaliziTestleri
{
    private static RuntimeAnalizi Analiz(
        IReadOnlyList<RuntimeProcess> processler,
        IReadOnlyList<YuklenenModul>? moduller,
        Func<RuntimeProcess, bool>? imza = null)
        => new(new SahteProcessSaglayici(processler, moduller), imza);

    [Fact]
    public void TemizPcAnomaliUretmez()
    {
        var hedef = RuntimeYardimci.Hedef();
        var sonuc = Analiz(new[] { hedef }, RuntimeYardimci.MesruModuller()).Calistir(hedef.ProcessId);

        Assert.Equal(RuntimeErisimi.Tam, sonuc.Erisim);
        Assert.Empty(sonuc.Anomaliler);
        Assert.Equal(10, sonuc.IncelenenModulSayisi);
    }

    [Fact]
    public void ProcessBulunamazsaKapsamYok()
    {
        var sonuc = Analiz(Array.Empty<RuntimeProcess>(), null).Calistir(1234);

        Assert.Equal(RuntimeErisimi.Yok, sonuc.Erisim);
        Assert.Null(sonuc.Hedef);
        Assert.Empty(sonuc.Anomaliler);
        Assert.Contains(sonuc.Notlar, n => n.Contains("bulunamadi"));
    }

    [Fact]
    public void ModulListesiOkunamazsaKapsamSinirli()
    {
        var hedef = RuntimeYardimci.Hedef();
        var sonuc = Analiz(new[] { hedef }, null).Calistir(hedef.ProcessId);

        Assert.Equal(RuntimeErisimi.Sinirli, sonuc.Erisim);
        Assert.Contains(sonuc.Notlar, n => n.Contains("modul listesi okunamadi"));
    }

    [Fact]
    public void KomutSatiriOkunamazsaKapsamSinirli()
    {
        var hedef = RuntimeYardimci.Hedef(komut: null);
        var sonuc = Analiz(new[] { hedef }, RuntimeYardimci.MesruModuller()).Calistir(hedef.ProcessId);

        Assert.Equal(RuntimeErisimi.Sinirli, sonuc.Erisim);
        Assert.Contains(sonuc.Notlar, n => n.Contains("komut satiri okunamadi"));
    }

    [Fact]
    public void JavaAjaniAnomaliUretir()
    {
        var hedef = RuntimeYardimci.Hedef(
            @"javaw.exe -javaagent:C:\Users\o\Downloads\ghost.jar -cp C:\oyun\1.21.jar net.minecraft.client.main.Main");
        var sonuc = Analiz(new[] { hedef }, RuntimeYardimci.MesruModuller()).Calistir(hedef.ProcessId);

        Assert.True(sonuc.AjanVarMi);
        var anomali = Assert.Single(sonuc.Anomaliler, a => a.Tur == AnomaliTuru.JavaAjani);
        Assert.Equal("ghost.jar", anomali.Ad);
    }

    [Fact]
    public void IndirilenlerdenGelenClasspathGirdisiAnomali()
    {
        var hedef = RuntimeYardimci.Hedef(
            @"javaw.exe -cp ""C:\Users\o\AppData\Roaming\.minecraft\versions\1.21\1.21.jar;C:\Users\o\Downloads\ek.jar""");
        var sonuc = Analiz(new[] { hedef }, RuntimeYardimci.MesruModuller()).Calistir(hedef.ProcessId);

        var anomali = Assert.Single(sonuc.Anomaliler, a => a.Tur == AnomaliTuru.SiradisiClasspath);
        Assert.Equal("ek.jar", anomali.Ad);
    }

    [Fact]
    public void OyunDiziniClasspathAnomaliDegil()
    {
        var hedef = RuntimeYardimci.Hedef();
        var sonuc = Analiz(new[] { hedef }, RuntimeYardimci.MesruModuller()).Calistir(hedef.ProcessId);

        Assert.DoesNotContain(sonuc.Anomaliler, a => a.Tur == AnomaliTuru.SiradisiClasspath);
    }

    [Fact]
    public void TanidikProcesslerHariciIliskiUretmez()
    {
        var hedef = RuntimeYardimci.Hedef();
        var digerleri = new[]
        {
            hedef,
            new RuntimeProcess(1, "chrome", @"C:\Program Files\Google\Chrome\chrome.exe"),
            new RuntimeProcess(2, "Discord", @"C:\Users\o\AppData\Local\Discord\Discord.exe"),
            new RuntimeProcess(3, "obs64", @"C:\Program Files\obs-studio\bin\64bit\obs64.exe"),
            new RuntimeProcess(4, "explorer", @"C:\Windows\explorer.exe"),
        };

        var sonuc = Analiz(digerleri, RuntimeYardimci.MesruModuller()).Calistir(hedef.ProcessId);

        Assert.Empty(sonuc.Anomaliler);
        Assert.Equal(4, sonuc.DegerlendirilenHariciProcess);
    }

    [Fact]
    public void TempDizinindenCalisanTaklitJavaAnomali()
    {
        var hedef = RuntimeYardimci.Hedef();
        var sonuc = Analiz(new[]
        {
            hedef,
            new RuntimeProcess(9, "javaw", @"C:\Users\o\AppData\Local\Temp\javaw.exe"),
        }, RuntimeYardimci.MesruModuller()).Calistir(hedef.ProcessId);

        Assert.Equal(AnomaliTuru.HariciProcessIliskisi,
            Assert.Single(sonuc.Anomaliler).Tur);
    }

    [Fact]
    public void KuralPaketiImzasiHariciProcessiYakalar()
    {
        var hedef = RuntimeYardimci.Hedef();
        var sonuc = Analiz(
            new[] { hedef, new RuntimeProcess(7, "tikla", @"C:\Program Files\tikla\tikla.exe") },
            RuntimeYardimci.MesruModuller(),
            imza: p => p.Ad.Equals("tikla", StringComparison.OrdinalIgnoreCase))
            .Calistir(hedef.ProcessId);

        Assert.Equal(AnomaliTuru.HariciProcessIliskisi, Assert.Single(sonuc.Anomaliler).Tur);
    }

    [Fact]
    public void VarsayilanOlarakIsimBenzerligiyleTahminYurutulmez()
    {
        var hedef = RuntimeYardimci.Hedef();
        var sonuc = Analiz(new[]
        {
            hedef,
            new RuntimeProcess(5, "autoclicker", @"C:\Program Files\MouseTool\autoclicker.exe"),
        }, RuntimeYardimci.MesruModuller()).Calistir(hedef.ProcessId);

        Assert.Empty(sonuc.Anomaliler);
    }

    [Fact]
    public void AnomaliSayimiYolVeyaProcessListesiIcermez()
    {
        var hedef = RuntimeYardimci.Hedef(
            @"javaw.exe -javaagent:C:\Users\gizliad\Downloads\ghost.jar");
        var sonuc = Analiz(new[] { hedef }, RuntimeYardimci.MesruModuller()).Calistir(hedef.ProcessId);

        var sayim = sonuc.AnomaliSayimi();
        Assert.Equal(1, sayim[nameof(AnomaliTuru.JavaAjani)]);
        Assert.DoesNotContain("gizliad", string.Join('|', sayim.Keys));
        Assert.All(sonuc.Anomaliler, a => Assert.Equal(16, a.OrtukKimlik.Length));
    }
}

public class RuntimeKuralEntegrasyonTestleri
{

    private static KuralMotoru Motor()
    {
        var kural = new Kural("R-RT-1", "runtime ajan", "CLIENT", "HIGH", 1,
            new List<Gosterge>
            {
                new(GostergeTipi.RuntimeAnomalisi, new List<string> { nameof(AnomaliTuru.JavaAjani) }),
                new(GostergeTipi.JvmAjani, new List<string> { "ghost.jar" }),
            });

        return new KuralMotoru(new KuralPaketi("test.1", new List<Kural> { kural }));
    }

    private static RuntimeAnalizSonucu RuntimeSonucu(string komut)
    {
        var hedef = RuntimeYardimci.Hedef(komut);
        return new RuntimeAnalizi(
            new SahteProcessSaglayici(new[] { hedef }, RuntimeYardimci.MesruModuller()))
            .Calistir(hedef.ProcessId);
    }

    private static AdliKapsam TamKapsam() => new AdliKapsamHesaplayici().Hesapla(
        new[]
        {
            new BirimDegerlendirmesi(
                new BirimBilgisi(@"C:\", "NTFS"),
                new MftOkumaSonucu(MftSonucTuru.Okundu, 1000),
                new UsnOkumaSonucu(UsnSonucTuru.Okundu,
                    new UsnJournalDurumu(1, 1, 100, 1024, DateTimeOffset.UtcNow.AddDays(-200)),
                    Array.Empty<UsnKaydi>())),
        },
        AyricalikDurumu.Yonetici, RuntimeErisimi.Tam);

    [Fact]
    public void TekRuntimeAnomalisiIsaretlemeUretmez()
    {
        var sonuc = RuntimeSonucu(@"javaw.exe -javaagent:C:\Users\o\Downloads\ghost.jar");
        var set = RuntimeGostergeleri.Doldur(sonuc);
        var eslesmeler = Motor().Degerlendir(set);

        var eslesme = Assert.Single(eslesmeler);
        Assert.NotEqual(Guven.Yuksek, eslesme.Guven);
        Assert.False(eslesme.IsaretlenirMi);

        Assert.Equal(Sonuc.Supheli, SonucKarari.Belirle(eslesmeler, TamKapsam()));
    }

    [Fact]
    public void RuntimeAnomalisiOlmayanTemizPcEslesmeUretmez()
    {
        var set = RuntimeGostergeleri.Doldur(RuntimeSonucu(RuntimeYardimci.GercekciKomut));
        Assert.Empty(Motor().Degerlendir(set));
    }

    [Fact]
    public void RuntimeGostergeleriDiskGostergeleriyleBirlesebilir()
    {
        var set = new GostergeSeti { OrtukKimlik = "abc" };
        set.Ekle(GostergeTipi.Sha256, "deadbeef");
        RuntimeGostergeleri.Doldur(RuntimeSonucu(@"javaw.exe -javaagent:C:\Users\o\Downloads\ghost.jar"), set);

        Assert.True(set.Icerir(GostergeTipi.Sha256, "deadbeef"));
        Assert.True(set.Icerir(GostergeTipi.JvmAjani, "ghost.jar"));
        Assert.True(set.Icerir(GostergeTipi.RuntimeAnomalisi, nameof(AnomaliTuru.JavaAjani)));
    }
}

public class ClasspathAyirmaTestleri
{
    [Fact]
    public void WindowsSurucuHarfiAyiriciSanilmaz()
    {
        var jvm = JvmKomutSatiri.Coz(@"javaw.exe -cp C:\oyun\a.jar;D:\oyun\b.jar");

        Assert.Equal(2, jvm.ClasspathGirdileri.Count);
        Assert.Contains(@"C:\oyun\a.jar", jvm.ClasspathGirdileri);
        Assert.Contains(@"D:\oyun\b.jar", jvm.ClasspathGirdileri);
    }

    [Fact]
    public void PosixAyiriciDaCalisir()
    {
        var jvm = JvmKomutSatiri.Coz("java -cp /opt/oyun/a.jar:/opt/oyun/b.jar");

        Assert.Equal(2, jvm.ClasspathGirdileri.Count);
        Assert.Contains("/opt/oyun/b.jar", jvm.ClasspathGirdileri);
    }
}
