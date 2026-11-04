namespace HapiScanner.Core.Runtime;

public static class JvmKomutSatiri
{
    public static JvmMetadata Coz(string? komutSatiri)
    {
        if (string.IsNullOrWhiteSpace(komutSatiri)) return JvmMetadata.Bos();

        var parcalar = Parcala(komutSatiri);
        var argumanlar = new List<string>();
        var classpath = new List<string>();
        var javaAjanlari = new List<string>();
        var nativeAjanlar = new List<string>();
        var attachKapali = false;

        for (var i = 0; i < parcalar.Count; i++)
        {
            var p = parcalar[i];

            if (p.StartsWith("-javaagent:", StringComparison.OrdinalIgnoreCase))
            {
                javaAjanlari.Add(AjanYolu(p["-javaagent:".Length..]));
                argumanlar.Add(p);
                continue;
            }

            if (p.StartsWith("-agentpath:", StringComparison.OrdinalIgnoreCase) ||
                p.StartsWith("-agentlib:", StringComparison.OrdinalIgnoreCase))
            {
                nativeAjanlar.Add(AjanYolu(p[(p.IndexOf(':') + 1)..]));
                argumanlar.Add(p);
                continue;
            }

            if (p.StartsWith("-Djava.class.path=", StringComparison.OrdinalIgnoreCase))
            {
                classpath.AddRange(ClasspathAyir(p["-Djava.class.path=".Length..]));
                argumanlar.Add(p);
                continue;
            }

            if (p.Equals("-XX:-DisableAttachMechanism", StringComparison.OrdinalIgnoreCase))
            {
                argumanlar.Add(p);
                continue;
            }

            if (p.Equals("-XX:+DisableAttachMechanism", StringComparison.OrdinalIgnoreCase))
            {
                attachKapali = true;
                argumanlar.Add(p);
                continue;
            }

            if (p is "-cp" or "-classpath" or "--class-path" && i + 1 < parcalar.Count)
            {
                classpath.AddRange(ClasspathAyir(parcalar[++i]));
                argumanlar.Add(p);
                continue;
            }

            if (p.StartsWith('-')) argumanlar.Add(p);
        }

        return new JvmMetadata(argumanlar, Tekille(classpath),
            Tekille(javaAjanlari), Tekille(nativeAjanlar), attachKapali);
    }

    private static string AjanYolu(string ham)
    {
        var esittir = ham.IndexOf('=');
        var yol = esittir >= 0 ? ham[..esittir] : ham;
        return yol.Trim('"').Trim();
    }

    private static IEnumerable<string> ClasspathAyir(string ham)
    {
        foreach (var kaba in ham.Trim('"').Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            if (SurucuHarfiVarMi(kaba))
            {
                var girdi = kaba.Trim();
                if (girdi.Length > 0) yield return girdi;
                continue;
            }

            foreach (var girdi in kaba.Split(':', StringSplitOptions.RemoveEmptyEntries))
            {
                var temiz = girdi.Trim();
                if (temiz.Length > 0) yield return temiz;
            }
        }
    }

    private static bool SurucuHarfiVarMi(string yol)
    {
        var temiz = yol.TrimStart();
        return temiz.Length >= 3 && char.IsAsciiLetter(temiz[0])
               && temiz[1] == ':' && temiz[2] is '\\' or '/';
    }

    private static List<string> Tekille(IEnumerable<string> girdiler)
        => girdiler.Where(g => !string.IsNullOrWhiteSpace(g))
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .ToList();

    internal static List<string> Parcala(string satir)
    {
        var parcalar = new List<string>();
        var simdiki = new System.Text.StringBuilder();
        var tirnakta = false;

        foreach (var karakter in satir)
        {
            switch (karakter)
            {
                case '"':
                    tirnakta = !tirnakta;
                    break;
                case ' ' or '\t' when !tirnakta:
                    if (simdiki.Length > 0)
                    {
                        parcalar.Add(simdiki.ToString());
                        simdiki.Clear();
                    }
                    break;
                default:
                    simdiki.Append(karakter);
                    break;
            }
        }

        if (simdiki.Length > 0) parcalar.Add(simdiki.ToString());
        return parcalar;
    }
}
