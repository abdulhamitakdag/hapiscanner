namespace HapiScanner.Core.Tespit;

public static class RulesetSurumu
{
    public static bool GecerliMi(string? surum) => Ayikla(surum) is not null;

    public static bool KucukMu(string? surum, string? asgari)
    {
        var a = Ayikla(surum) ?? new List<int> { 0 };
        var b = Ayikla(asgari) ?? new List<int> { 0 };

        var uzunluk = Math.Max(a.Count, b.Count);
        for (var i = 0; i < uzunluk; i++)
        {
            var sol = i < a.Count ? a[i] : 0;
            var sag = i < b.Count ? b[i] : 0;
            if (sol != sag) return sol < sag;
        }
        return false;
    }

    public static int Karsilastir(string? a, string? b)
    {
        if (KucukMu(a, b)) return -1;
        return KucukMu(b, a) ? 1 : 0;
    }

    private static List<int>? Ayikla(string? surum)
    {
        if (string.IsNullOrWhiteSpace(surum)) return null;

        var parcalar = new List<int>();
        foreach (var ham in surum.Split('.'))
        {
            var sayi = new string(ham.TakeWhile(char.IsAsciiDigit).ToArray());
            if (sayi.Length == 0) return null;
            if (!int.TryParse(sayi, out var deger)) return null;
            parcalar.Add(deger);
        }

        return parcalar.Count >= 2 ? parcalar : null;
    }
}
