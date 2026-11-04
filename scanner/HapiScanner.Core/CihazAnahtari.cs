using System.Security.Cryptography;

namespace HapiScanner.Core;

public enum GuvenceSeviyesi
{

    Tpm,

    Yazilim,
}

public interface ICihazAnahtari
{
    GuvenceSeviyesi Guvence { get; }

    string PublicKeyBase64();

    string Imzala(byte[] metin);
}

public sealed class YazilimAnahtari : ICihazAnahtari, IDisposable
{
    private readonly ECDsa _anahtar;

    public YazilimAnahtari()
    {

        _anahtar = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    }

    public YazilimAnahtari(ECDsa mevcut) => _anahtar = mevcut;

    public GuvenceSeviyesi Guvence => GuvenceSeviyesi.Yazilim;

    public string PublicKeyBase64()
        => Convert.ToBase64String(_anahtar.ExportSubjectPublicKeyInfo());

    public string Imzala(byte[] metin)
    {

        var imza = _anahtar.SignData(metin, HashAlgorithmName.SHA256,
                                     DSASignatureFormat.Rfc3279DerSequence);
        return Convert.ToBase64String(imza);
    }

    public byte[] GizliAnahtariCikar() => _anahtar.ExportPkcs8PrivateKey();

    public static YazilimAnahtari Yukle(byte[] pkcs8)
    {
        var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(pkcs8, out _);
        return new YazilimAnahtari(ecdsa);
    }

    public void Dispose() => _anahtar.Dispose();
}

public static class ParmakIzi
{

    public static string Hesapla(string publicKeyBase64)
    {
        var ham = Convert.FromBase64String(publicKeyBase64);
        var ozet = SHA256.HashData(ham);
        return Convert.ToHexString(ozet).ToLowerInvariant()[..32];
    }
}
