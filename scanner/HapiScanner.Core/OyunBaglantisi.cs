namespace HapiScanner.Core;

public enum ProcessDurumu
{
    Bulundu,
    Bulunamadi,
}

public enum BaglantiDurumu
{
    Bulundu,
    Bulunamadi,
}

public sealed record OyunBaglantiSonucu(
    ProcessDurumu Process,
    BaglantiDurumu Baglanti,
    IReadOnlyList<int> AdayProcessler,
    IReadOnlyList<string> EslesenEndpointler)
{
    public static OyunBaglantiSonucu HicYok() => new(
        ProcessDurumu.Bulunamadi, BaglantiDurumu.Bulunamadi,
        Array.Empty<int>(), Array.Empty<string>());

    public bool DogrulanabilirMi => Process == ProcessDurumu.Bulundu
                                    && Baglanti == BaglantiDurumu.Bulundu;

    public string Ozet => $"process={Process} baglanti={Baglanti} " +
                          $"aday={AdayProcessler.Count} eslesme={EslesenEndpointler.Count}";
}

public interface IOyunBaglantiBulucu
{

    OyunBaglantiSonucu Bul(IReadOnlyList<string> hapicraftEndpointleri);
}

public sealed record TcpBaglantisi(int ProcessId, string UzakAdres, int UzakPort, string Durum);

public sealed record AdayProcess(int ProcessId, string Ad, string? KomutSatiri);

public static class BaglantiEslestirici
{
    private static readonly string[] JavaIsimleri =
    {
        "javaw", "java", "javaw.exe", "java.exe",
    };

    public static bool AdayMi(AdayProcess p)
    {
        var ad = Path.GetFileNameWithoutExtension(p.Ad ?? string.Empty).ToLowerInvariant();
        if (JavaIsimleri.Any(j => Path.GetFileNameWithoutExtension(j) == ad))
            return true;

        var komut = p.KomutSatiri?.ToLowerInvariant() ?? string.Empty;
        return komut.Contains("minecraft") || komut.Contains("net.minecraft")
               || komut.Contains("-xmx") || komut.Contains("fabric")
               || komut.Contains("forge") || komut.Contains("neoforge");
    }

    public static OyunBaglantiSonucu Eslestir(
        IReadOnlyList<AdayProcess> processler,
        IReadOnlyList<TcpBaglantisi> baglantilar,
        IReadOnlyList<string> endpointler)
    {
        var adaylar = processler.Where(AdayMi).ToList();
        var adayIdler = adaylar.Select(p => p.ProcessId).Distinct().ToList();

        var hedefler = endpointler.Select(Coz).Where(h => h is not null)
                                  .Select(h => h!.Value).ToList();

        var eslesen = new List<string>();
        foreach (var b in baglantilar)
        {
            if (!b.Durum.Equals("ESTABLISHED", StringComparison.OrdinalIgnoreCase)) continue;
            if (!adayIdler.Contains(b.ProcessId)) continue;
            foreach (var (adres, port) in hedefler)
            {
                if (b.UzakPort != port) continue;
                if (!AdresEsit(b.UzakAdres, adres)) continue;
                eslesen.Add($"{b.UzakAdres}:{b.UzakPort}");
            }
        }

        return new OyunBaglantiSonucu(
            adayIdler.Count > 0 ? ProcessDurumu.Bulundu : ProcessDurumu.Bulunamadi,
            eslesen.Count > 0 ? BaglantiDurumu.Bulundu : BaglantiDurumu.Bulunamadi,
            adayIdler,
            eslesen.Distinct().ToList());
    }

    private static (string Adres, int Port)? Coz(string endpoint)
    {

        if (endpoint.StartsWith('['))
        {
            var kapanis = endpoint.IndexOf(']');
            if (kapanis < 0) return null;
            var adres6 = endpoint.Substring(1, kapanis - 1);
            var kalan = endpoint[(kapanis + 1)..].TrimStart(':');
            return int.TryParse(kalan, out var p6) ? (adres6, p6) : (adres6, 25565);
        }
        var parcalar = endpoint.Split(':');
        if (parcalar.Length == 1) return (parcalar[0], 25565);
        return int.TryParse(parcalar[^1], out var p) ? (parcalar[0], p) : null;
    }

    private static bool AdresEsit(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;

        if (System.Net.IPAddress.TryParse(a, out var ipA)
            && System.Net.IPAddress.TryParse(b, out var ipB))
        {
            return ipA.Equals(ipB)
                   || (ipA.IsIPv4MappedToIPv6 && ipA.MapToIPv4().Equals(ipB))
                   || (ipB.IsIPv4MappedToIPv6 && ipB.MapToIPv4().Equals(ipA));
        }
        return false;
    }
}
