namespace HapiScanner.Core.Model3B;

public readonly struct Matris4
{
    private readonly double[,] _m;

    private Matris4(double[,] m) => _m = m;

    public static Matris4 Birim => new(new double[,]
    {
        { 1, 0, 0, 0 },
        { 0, 1, 0, 0 },
        { 0, 0, 1, 0 },
        { 0, 0, 0, 1 },
    });

    public static Matris4 Oteleme(Nokta3 d) => new(new double[,]
    {
        { 1, 0, 0, 0 },
        { 0, 1, 0, 0 },
        { 0, 0, 1, 0 },
        { d.X, d.Y, d.Z, 1 },
    });

    public static Matris4 DonusX(double derece)
    {
        var (s, c) = Math.SinCos(derece * Math.PI / 180);
        return new(new double[,]
        {
            { 1, 0, 0, 0 },
            { 0, c, s, 0 },
            { 0, -s, c, 0 },
            { 0, 0, 0, 1 },
        });
    }

    public static Matris4 DonusY(double derece)
    {
        var (s, c) = Math.SinCos(derece * Math.PI / 180);
        return new(new double[,]
        {
            { c, 0, -s, 0 },
            { 0, 1, 0, 0 },
            { s, 0, c, 0 },
            { 0, 0, 0, 1 },
        });
    }

    public static Matris4 DonusZ(double derece)
    {
        var (s, c) = Math.SinCos(derece * Math.PI / 180);
        return new(new double[,]
        {
            { c, s, 0, 0 },
            { -s, c, 0, 0 },
            { 0, 0, 1, 0 },
            { 0, 0, 0, 1 },
        });
    }

    public static Matris4 operator *(Matris4 a, Matris4 b)
    {
        var s = new double[4, 4];
        for (var i = 0; i < 4; i++)
            for (var j = 0; j < 4; j++)
            {
                var t = 0.0;
                for (var k = 0; k < 4; k++) t += a._m[i, k] * b._m[k, j];
                s[i, j] = t;
            }
        return new Matris4(s);
    }

    public Nokta3 Uygula(Nokta3 p) => new(
        p.X * _m[0, 0] + p.Y * _m[1, 0] + p.Z * _m[2, 0] + _m[3, 0],
        p.X * _m[0, 1] + p.Y * _m[1, 1] + p.Z * _m[2, 1] + _m[3, 1],
        p.X * _m[0, 2] + p.Y * _m[1, 2] + p.Z * _m[2, 2] + _m[3, 2]);
}

public static class Iskelet
{
    public static IReadOnlyDictionary<Eklem, Eklem?> Ebeveyn { get; } =
        new Dictionary<Eklem, Eklem?>
        {
            [Eklem.Govde] = null,
            [Eklem.Bas] = Eklem.Govde,
            [Eklem.SagKol] = Eklem.Govde,
            [Eklem.SolKol] = Eklem.Govde,
            [Eklem.Pelerin] = Eklem.Govde,
            [Eklem.SagBacak] = null,
            [Eklem.SolBacak] = null,
        };

    public static Nokta3 Pivot(IEnumerable<ParcaTanimi> parcalar, Eklem eklem)
    {
        foreach (var p in parcalar)
            if (p.Eklem == eklem) return p.Pivot;
        return Nokta3.Sifir;
    }

    public static Nokta3 YerelPivot(IEnumerable<ParcaTanimi> parcalar, Eklem eklem)
    {
        var liste = parcalar as IList<ParcaTanimi> ?? parcalar.ToList();
        var pivot = Pivot(liste, eklem);
        if (Ebeveyn[eklem] is not { } ebeveyn) return pivot;
        var ebeveynPivot = Pivot(liste, ebeveyn);
        return new Nokta3(
            pivot.X - ebeveynPivot.X,
            pivot.Y - ebeveynPivot.Y,
            pivot.Z - ebeveynPivot.Z);
    }

    public static Dictionary<Eklem, Matris4> Donusumler(
        IEnumerable<ParcaTanimi> parcalar, Poz poz)
    {
        var liste = parcalar as IList<ParcaTanimi> ?? parcalar.ToList();
        var kok = Matris4.DonusZ(poz.ModelEgimi)
                  * Matris4.DonusY(poz.ModelDonusu)
                  * Matris4.Oteleme(new Nokta3(0, poz.Yukseklik, 0));

        var sonuc = new Dictionary<Eklem, Matris4>();

        Matris4 Coz(Eklem eklem)
        {
            if (sonuc.TryGetValue(eklem, out var hazir)) return hazir;

            var d = poz[eklem];
            var yerel = Matris4.DonusX(d.X)
                        * Matris4.DonusZ(d.Z)
                        * Matris4.DonusY(d.Y)
                        * Matris4.Oteleme(YerelPivot(liste, eklem));

            var tam = Ebeveyn[eklem] is { } ebeveyn
                ? yerel * Coz(ebeveyn)
                : yerel * kok;

            sonuc[eklem] = tam;
            return tam;
        }

        foreach (var eklem in Ebeveyn.Keys) Coz(eklem);
        return sonuc;
    }
}
