using System.Text;

namespace HapiScanner.Core;

public static class Imza
{
    public const string BaglamHeartbeat = "heartbeat";
    public const string BaglamRapor = "report";
    public const string BaglamKuralPaketi = "ruleset";

    public static byte[] Metin(string baglam, IReadOnlyDictionary<string, string> alanlar)
    {
        if (string.IsNullOrEmpty(baglam))
            throw new ArgumentException("baglam bos olamaz", nameof(baglam));

        var govde = new StringBuilder(baglam);

        foreach (var ad in alanlar.Keys.OrderBy(a => a, StringComparer.Ordinal))
        {
            govde.Append('|').Append(ad).Append('=').Append(alanlar[ad]);
        }
        return Encoding.UTF8.GetBytes(govde.ToString());
    }

    public static byte[] HeartbeatMetni(string verificationId, string nonce, long siraNo,
                                        string asama, string zaman)
        => Metin(BaglamHeartbeat, new Dictionary<string, string>
        {
            ["verification_id"] = verificationId,
            ["nonce"] = nonce,
            ["sequence"] = siraNo.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["stage"] = asama,
            ["timestamp"] = zaman,
        });

    public static byte[] KuralPaketiMetni(string paketOzeti, string rulesetSurumu)
        => Metin(BaglamKuralPaketi, new Dictionary<string, string>
        {
            ["package_sha256"] = paketOzeti,
            ["ruleset_version"] = rulesetSurumu,
        });

    public static byte[] RaporMetni(string verificationId, string nonce, string scannerSurumu,
                                    string rulesetSurumu, string raporHash, string zaman)
        => Metin(BaglamRapor, new Dictionary<string, string>
        {
            ["verification_id"] = verificationId,
            ["nonce"] = nonce,
            ["scanner_version"] = scannerSurumu,
            ["ruleset_version"] = rulesetSurumu,
            ["report_hash"] = raporHash,
            ["timestamp"] = zaman,
        });
}
