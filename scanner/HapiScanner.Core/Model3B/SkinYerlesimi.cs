namespace HapiScanner.Core.Model3B;

public sealed record UvAlani(int X, int Y, int Genislik, int Yukseklik)
{
    public int Sag => X + Genislik;
    public int Alt => Y + Yukseklik;

    public UvAlani Aynala() => this with { X = Sag, Genislik = -Genislik };
}

public sealed record KutuUv(
    UvAlani Ust,
    UvAlani Alt,
    UvAlani Sag,
    UvAlani On,
    UvAlani Sol,
    UvAlani Arka)
{
    public IReadOnlyList<UvAlani> Tumu => new[] { Ust, Alt, Sag, On, Sol, Arka };

    public KutuUv Aynala() => new(
        Ust.Aynala(), Alt.Aynala(),

        Sol.Aynala(), On.Aynala(), Sag.Aynala(), Arka.Aynala());
}

public readonly record struct Nokta3(double X, double Y, double Z)
{
    public static Nokta3 Sifir => new(0, 0, 0);
    public Nokta3 Topla(Nokta3 d) => new(X + d.X, Y + d.Y, Z + d.Z);
}

public enum Eklem
{
    Bas,
    Govde,
    SagKol,
    SolKol,
    SagBacak,
    SolBacak,
    Pelerin,
}

public sealed record ParcaTanimi(
    string Ad,
    Eklem Eklem,
    Nokta3 Olcu,

    Nokta3 Pivot,

    Nokta3 MerkezPivota,
    KutuUv Uv,

    double Sisme = 0,
    bool IkinciKatman = false);

public static class SkinYerlesimi
{
    public const int DokuGenisligi = 64;
    public const int EskiDokuYuksekligi = 32;
    public const int DokuYuksekligi = 64;

    public const double KatmanSismesi = 0.26;

    private static KutuUv Kutu(int x, int y, int g, int yuk, int d) => new(
        Ust: new UvAlani(x + d, y, g, d),
        Alt: new UvAlani(x + d + g, y, g, d),
        Sag: new UvAlani(x, y + d, d, yuk),
        On: new UvAlani(x + d, y + d, g, yuk),
        Sol: new UvAlani(x + d + g, y + d, d, yuk),
        Arka: new UvAlani(x + d + g + d, y + d, g, yuk));

    public static double KolGenisligi(bool ince) => ince ? 3 : 4;

    public const double OmuzYuksekligi = 24;

    public const double KalcaYuksekligi = 12;

    public const double Boy = 32;

    public static IReadOnlyList<ParcaTanimi> Parcalar(
        bool ince = false,
        bool eskiBicim = false,
        bool pelerinVar = false)
    {
        var kg = KolGenisligi(ince);
        var kolBoyu = ince ? 12 : 12;
        var liste = new List<ParcaTanimi>();

        liste.Add(new ParcaTanimi(
            "bas", Eklem.Bas,
            new Nokta3(8, 8, 8),
            new Nokta3(0, OmuzYuksekligi, 0),
            new Nokta3(0, 4, 0),
            Kutu(0, 0, 8, 8, 8)));

        liste.Add(new ParcaTanimi(
            "bas_katman", Eklem.Bas,
            new Nokta3(8, 8, 8),
            new Nokta3(0, OmuzYuksekligi, 0),
            new Nokta3(0, 4, 0),
            Kutu(32, 0, 8, 8, 8),
            KatmanSismesi, IkinciKatman: true));

        liste.Add(new ParcaTanimi(
            "govde", Eklem.Govde,
            new Nokta3(8, 12, 4),
            new Nokta3(0, KalcaYuksekligi, 0),
            new Nokta3(0, 6, 0),
            Kutu(16, 16, 8, 12, 4)));

        var omuzX = 5.0;

        var kolMerkezi = (4 + kg / 2) - omuzX;
        liste.Add(new ParcaTanimi(
            "sag_kol", Eklem.SagKol,
            new Nokta3(kg, kolBoyu, 4),
            new Nokta3(-omuzX, OmuzYuksekligi - 2, 0),
            new Nokta3(-kolMerkezi, -(kolBoyu / 2.0) + 2, 0),
            Kutu(40, 16, (int)kg, kolBoyu, 4)));

        liste.Add(new ParcaTanimi(
            "sol_kol", Eklem.SolKol,
            new Nokta3(kg, kolBoyu, 4),
            new Nokta3(omuzX, OmuzYuksekligi - 2, 0),
            new Nokta3(kolMerkezi, -(kolBoyu / 2.0) + 2, 0),
            eskiBicim ? Kutu(40, 16, (int)kg, kolBoyu, 4).Aynala()
                      : Kutu(32, 48, (int)kg, kolBoyu, 4)));

        liste.Add(new ParcaTanimi(
            "sag_bacak", Eklem.SagBacak,
            new Nokta3(4, 12, 4),
            new Nokta3(-2, KalcaYuksekligi, 0),
            new Nokta3(0, -6, 0),
            Kutu(0, 16, 4, 12, 4)));

        liste.Add(new ParcaTanimi(
            "sol_bacak", Eklem.SolBacak,
            new Nokta3(4, 12, 4),
            new Nokta3(2, KalcaYuksekligi, 0),
            new Nokta3(0, -6, 0),
            eskiBicim ? Kutu(0, 16, 4, 12, 4).Aynala()
                      : Kutu(16, 48, 4, 12, 4)));

        if (!eskiBicim)
        {
            liste.Add(new ParcaTanimi(
                "govde_katman", Eklem.Govde,
                new Nokta3(8, 12, 4),
                new Nokta3(0, KalcaYuksekligi, 0),
                new Nokta3(0, 6, 0),
                Kutu(16, 32, 8, 12, 4),
                KatmanSismesi, IkinciKatman: true));

            liste.Add(new ParcaTanimi(
                "sag_kol_katman", Eklem.SagKol,
                new Nokta3(kg, kolBoyu, 4),
                new Nokta3(-omuzX, OmuzYuksekligi - 2, 0),
                new Nokta3(-kolMerkezi, -(kolBoyu / 2.0) + 2, 0),
                Kutu(40, 32, (int)kg, kolBoyu, 4),
                KatmanSismesi, IkinciKatman: true));

            liste.Add(new ParcaTanimi(
                "sol_kol_katman", Eklem.SolKol,
                new Nokta3(kg, kolBoyu, 4),
                new Nokta3(omuzX, OmuzYuksekligi - 2, 0),
                new Nokta3(kolMerkezi, -(kolBoyu / 2.0) + 2, 0),
                Kutu(48, 48, (int)kg, kolBoyu, 4),
                KatmanSismesi, IkinciKatman: true));

            liste.Add(new ParcaTanimi(
                "sag_bacak_katman", Eklem.SagBacak,
                new Nokta3(4, 12, 4),
                new Nokta3(-2, KalcaYuksekligi, 0),
                new Nokta3(0, -6, 0),
                Kutu(0, 32, 4, 12, 4),
                KatmanSismesi, IkinciKatman: true));

            liste.Add(new ParcaTanimi(
                "sol_bacak_katman", Eklem.SolBacak,
                new Nokta3(4, 12, 4),
                new Nokta3(2, KalcaYuksekligi, 0),
                new Nokta3(0, -6, 0),
                Kutu(0, 48, 4, 12, 4),
                KatmanSismesi, IkinciKatman: true));
        }

        if (pelerinVar)
        {

            liste.Add(new ParcaTanimi(
                "pelerin", Eklem.Pelerin,
                new Nokta3(10, 16, 1),
                new Nokta3(0, OmuzYuksekligi - 1, -2.2),
                new Nokta3(0, -8, -0.5),
                new KutuUv(
                    Ust: new UvAlani(1, 0, 10, 1),
                    Alt: new UvAlani(11, 0, 10, 1),
                    Sag: new UvAlani(0, 1, 1, 16),

                    On: new UvAlani(12, 1, 10, 16),
                    Sol: new UvAlani(11, 1, 1, 16),
                    Arka: new UvAlani(1, 1, 10, 16))));
        }

        return liste;
    }

    public static IReadOnlyList<string> UvDenetle(
        IEnumerable<ParcaTanimi> parcalar, int genislik, int yukseklik)
    {
        var hatalar = new List<string>();
        foreach (var p in parcalar)
        {
            if (p.Ad == "pelerin") continue;
            foreach (var uv in p.Uv.Tumu)
            {
                var sol = Math.Min(uv.X, uv.Sag);
                var sag = Math.Max(uv.X, uv.Sag);
                if (sol < 0 || uv.Y < 0 || sag > genislik || uv.Alt > yukseklik)
                    hatalar.Add($"{p.Ad}: {uv}");
            }
        }
        return hatalar;
    }
}
