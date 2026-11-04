namespace HapiScanner.Core.Model3B;

public sealed record Yuz(
    string Ad,
    UvAlani Uv,
    Nokta3 Normal,
    Nokta3 SolUst,
    Nokta3 SagUst,
    Nokta3 SolAlt,
    Nokta3 SagAlt);

public static class Kutu
{
    public static IReadOnlyList<Yuz> Yuzler(ParcaTanimi parca)
    {
        var s = parca.Sisme;
        var gx = parca.Olcu.X + s * 2;
        var gy = parca.Olcu.Y + s * 2;
        var gz = parca.Olcu.Z + s * 2;

        var x0 = parca.MerkezPivota.X - gx / 2;
        var x1 = parca.MerkezPivota.X + gx / 2;
        var y0 = parca.MerkezPivota.Y - gy / 2;
        var y1 = parca.MerkezPivota.Y + gy / 2;
        var z0 = parca.MerkezPivota.Z - gz / 2;
        var z1 = parca.MerkezPivota.Z + gz / 2;

        Nokta3 N(double x, double y, double z) => new(x, y, z);

        return new[]
        {

            new Yuz("ust", parca.Uv.Ust, N(0, 1, 0),
                N(x0, y1, z0), N(x1, y1, z0), N(x0, y1, z1), N(x1, y1, z1)),

            new Yuz("alt", parca.Uv.Alt, N(0, -1, 0),
                N(x0, y0, z1), N(x1, y0, z1), N(x0, y0, z0), N(x1, y0, z0)),

            new Yuz("sag", parca.Uv.Sag, N(-1, 0, 0),
                N(x0, y1, z0), N(x0, y1, z1), N(x0, y0, z0), N(x0, y0, z1)),

            new Yuz("on", parca.Uv.On, N(0, 0, 1),
                N(x0, y1, z1), N(x1, y1, z1), N(x0, y0, z1), N(x1, y0, z1)),

            new Yuz("sol", parca.Uv.Sol, N(1, 0, 0),
                N(x1, y1, z1), N(x1, y1, z0), N(x1, y0, z1), N(x1, y0, z0)),

            new Yuz("arka", parca.Uv.Arka, N(0, 0, -1),
                N(x1, y1, z0), N(x0, y1, z0), N(x1, y0, z0), N(x0, y0, z0)),
        };
    }
}
