namespace HapiScanner.Core.Model3B;

public sealed class AnimasyonSurucusu
{

    public const double EnBuyukAdim = 0.1;

    private AnimasyonTuru _tur;
    private double _sure;
    private Poz? _gecisBaslangici;
    private double _gecisGecen;

    public AnimasyonSurucusu(AnimasyonTuru baslangic = AnimasyonTuru.Karsilama)
    {
        _tur = baslangic;
        AktifPoz = Animasyonlar.Hesapla(baslangic, 0);
    }

    public AnimasyonTuru Tur => _tur;
    public AnimasyonTanimi Tanim => Animasyonlar.Tanim(_tur);
    public Poz AktifPoz { get; private set; }
    public bool GecisVar => _gecisBaslangici is not null;

    public void Gecis(AnimasyonTuru tur)
    {
        if (tur == _tur) return;
        _gecisBaslangici = AktifPoz;
        _gecisGecen = 0;
        _tur = tur;
        _sure = 0;
    }

    public Poz Guncelle(double gecenSaniye, double ilerleme = 0)
    {
        var dt = Math.Clamp(gecenSaniye, 0, EnBuyukAdim);
        _sure += dt;

        var hedef = Animasyonlar.Hesapla(_tur, _sure, ilerleme);

        if (_gecisBaslangici is { } baslangic)
        {
            _gecisGecen += dt;
            var t = _gecisGecen / Animasyonlar.GecisSuresi;
            if (t >= 1)
            {
                _gecisBaslangici = null;
                AktifPoz = hedef;
            }
            else
            {

                var yumusak = t * t * (3 - 2 * t);
                AktifPoz = Poz.Karistir(baslangic, hedef, yumusak);
            }
        }
        else
        {
            AktifPoz = hedef;
        }

        return AktifPoz;
    }
}
