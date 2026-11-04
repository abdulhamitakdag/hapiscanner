namespace HapiScanner.Core.Model3B;

public readonly record struct Donus(double X = 0, double Y = 0, double Z = 0)
{
    public static Donus Yok => default;

    public static Donus Karistir(Donus a, Donus b, double t) => new(
        a.X + (b.X - a.X) * t,
        a.Y + (b.Y - a.Y) * t,
        a.Z + (b.Z - a.Z) * t);
}

public sealed record Poz(
    Donus Bas = default,
    Donus Govde = default,
    Donus SagKol = default,
    Donus SolKol = default,
    Donus SagBacak = default,
    Donus SolBacak = default,
    Donus Pelerin = default,

    double Yukseklik = 0,

    double ModelDonusu = 0,

    double ModelEgimi = 0,

    double EsyaDonusu = 0,
    double EsyaGorunurlugu = 1)
{
    public static Poz Durus { get; } = new();

    public Donus this[Eklem eklem] => eklem switch
    {
        Eklem.Bas => Bas,
        Eklem.Govde => Govde,
        Eklem.SagKol => SagKol,
        Eklem.SolKol => SolKol,
        Eklem.SagBacak => SagBacak,
        Eklem.SolBacak => SolBacak,
        Eklem.Pelerin => Pelerin,
        _ => Donus.Yok,
    };

    public static Poz Karistir(Poz a, Poz b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return new Poz(
            Donus.Karistir(a.Bas, b.Bas, t),
            Donus.Karistir(a.Govde, b.Govde, t),
            Donus.Karistir(a.SagKol, b.SagKol, t),
            Donus.Karistir(a.SolKol, b.SolKol, t),
            Donus.Karistir(a.SagBacak, b.SagBacak, t),
            Donus.Karistir(a.SolBacak, b.SolBacak, t),
            Donus.Karistir(a.Pelerin, b.Pelerin, t),
            a.Yukseklik + (b.Yukseklik - a.Yukseklik) * t,
            a.ModelDonusu + (b.ModelDonusu - a.ModelDonusu) * t,
            a.ModelEgimi + (b.ModelEgimi - a.ModelEgimi) * t,
            a.EsyaDonusu + (b.EsyaDonusu - a.EsyaDonusu) * t,
            a.EsyaGorunurlugu + (b.EsyaGorunurlugu - a.EsyaGorunurlugu) * t);
    }
}
