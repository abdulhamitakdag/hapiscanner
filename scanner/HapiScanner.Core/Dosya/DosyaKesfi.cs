namespace HapiScanner.Core.Dosya;

public sealed record TaramaKoku(string Yol, AdayNedeni Neden, int AzamiDerinlik = 6);

public interface IDosyaSistemi
{
    bool DizinVarMi(string yol);
    IEnumerable<string> AltDizinler(string yol);
    IEnumerable<DosyaBilgisi> Dosyalar(string yol);
    Stream? Ac(string yol);
}

public sealed class YerelDosyaSistemi : IDosyaSistemi
{
    public bool DizinVarMi(string yol) => Directory.Exists(yol);

    public IEnumerable<string> AltDizinler(string yol)
    {
        try
        {
            return Directory.EnumerateDirectories(yol);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    public IEnumerable<DosyaBilgisi> Dosyalar(string yol)
    {
        IEnumerable<string> adlar;
        try
        {
            adlar = Directory.EnumerateFiles(yol);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var ad in adlar)
        {
            DosyaBilgisi? bilgi = null;
            try
            {
                var f = new FileInfo(ad);
                bilgi = new DosyaBilgisi(f.FullName, f.Name, f.Length,
                                         f.LastWriteTimeUtc, f.CreationTimeUtc);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {

            }
            if (bilgi is not null) yield return bilgi;
        }
    }

    public Stream? Ac(string yol)
    {
        try
        {
            return new FileStream(yol, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

public sealed record KesifSonucu(
    IReadOnlyList<Aday> Adaylar,
    int GezilenDizin,
    int GorulenDosya,
    IReadOnlyList<string> AtlananKokler)
{

    public bool KapsamVarMi => GezilenDizin > 0;
}

public sealed class DosyaKesfi
{
    private readonly IDosyaSistemi _ds;
    private readonly AdaySuzgeci _suzgec;

    public DosyaKesfi(IDosyaSistemi ds, AdaySuzgeci? suzgec = null)
    {
        _ds = ds;
        _suzgec = suzgec ?? new AdaySuzgeci();
    }

    public int AzamiAday { get; init; } = 4000;
    public int AzamiDosya { get; init; } = 250_000;

    private static readonly string[] AtlanacakAdlar =
    {
        "node_modules", "$recycle.bin", "system volume information",
        "windowsapps", "winsxs", "assets", "saves", "resourcepacks",
        "screenshots", "shaderpacks", "logs", "crash-reports",
    };

    public KesifSonucu Calistir(
        IEnumerable<TaramaKoku> kokler,
        Action<Aday>? adayBulundu = null,
        CancellationToken iptal = default)
    {
        var adaylar = new List<Aday>();
        var atlanan = new List<string>();
        var gorulenYollar = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int gezilen = 0, gorulen = 0;

        foreach (var kok in kokler)
        {
            if (!_ds.DizinVarMi(kok.Yol))
            {
                atlanan.Add(kok.Yol);
                continue;
            }

            var kuyruk = new Queue<(string Yol, int Derinlik)>();
            kuyruk.Enqueue((kok.Yol, 0));

            while (kuyruk.Count > 0)
            {
                iptal.ThrowIfCancellationRequested();
                var (dizin, derinlik) = kuyruk.Dequeue();
                gezilen++;

                foreach (var dosya in _ds.Dosyalar(dizin))
                {
                    gorulen++;
                    if (gorulen > AzamiDosya) return Bitir();
                    if (!_suzgec.AdayMi(dosya)) continue;
                    if (!gorulenYollar.Add(dosya.Yol)) continue;

                    var aday = new Aday(dosya, kok.Neden);
                    adaylar.Add(aday);
                    adayBulundu?.Invoke(aday);
                    if (adaylar.Count >= AzamiAday) return Bitir();
                }

                if (derinlik >= kok.AzamiDerinlik) continue;
                foreach (var alt in _ds.AltDizinler(dizin))
                {
                    var ad = Path.GetFileName(alt.TrimEnd(Path.DirectorySeparatorChar));
                    if (AtlanacakAdlar.Contains(ad, StringComparer.OrdinalIgnoreCase)) continue;
                    kuyruk.Enqueue((alt, derinlik + 1));
                }
            }
        }

        return Bitir();

        KesifSonucu Bitir() => new(adaylar, gezilen, gorulen, atlanan);
    }
}
