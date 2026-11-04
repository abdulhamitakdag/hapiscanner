namespace HapiScanner.Core;

public interface IAnahtarKoruyucu
{
    byte[] Sarmala(byte[] ham);
    byte[] Coz(byte[] sarmali);
}

public sealed class SarmalamayanKoruyucu : IAnahtarKoruyucu
{
    public byte[] Sarmala(byte[] ham) => ham;
    public byte[] Coz(byte[] sarmali) => sarmali;
}

public sealed class KaliciAnahtarDeposu
{
    private readonly string _yol;
    private readonly IAnahtarKoruyucu _koruyucu;

    public KaliciAnahtarDeposu(string yol, IAnahtarKoruyucu? koruyucu = null)
    {
        _yol = yol;
        _koruyucu = koruyucu ?? new SarmalamayanKoruyucu();
    }

    public bool YenidenUretildi { get; private set; }

    public YazilimAnahtari Ac()
    {
        if (File.Exists(_yol))
        {
            try
            {
                var sarmali = File.ReadAllBytes(_yol);
                return YazilimAnahtari.Yukle(_koruyucu.Coz(sarmali));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException
                                           or System.Security.Cryptography.CryptographicException)
            {

                YenidenUretildi = true;
            }
        }

        var anahtar = new YazilimAnahtari();
        Yaz(anahtar);
        return anahtar;
    }

    private void Yaz(YazilimAnahtari anahtar)
    {
        try
        {
            var dizin = Path.GetDirectoryName(_yol);
            if (!string.IsNullOrEmpty(dizin)) Directory.CreateDirectory(dizin);
            File.WriteAllBytes(_yol, _koruyucu.Sarmala(anahtar.GizliAnahtariCikar()));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {

            YenidenUretildi = true;
        }
    }
}
