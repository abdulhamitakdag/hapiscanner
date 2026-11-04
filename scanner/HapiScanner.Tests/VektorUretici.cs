using System.Text;
using System.Text.Json;
using HapiScanner.Core;

namespace HapiScanner.Tests;

public class VektorUretici
{
    private const string Yol = "../../../../test-vectors/imza_vektoru.json";

    [Fact]
    public void VektorUret()
    {
        if (Environment.GetEnvironmentVariable("HAPISCANNER_WRITE_VECTORS") != "1")
            return;

        using var anahtar = new YazilimAnahtari();
        var kayitlar = new List<object>();

        void Ekle(string ad, byte[] metin)
        {
            kayitlar.Add(new
            {
                name = ad,
                payload = Encoding.UTF8.GetString(metin),
                signature = anahtar.Imzala(metin),
            });
        }

        Ekle("heartbeat", Imza.HeartbeatMetni(
            "7f3a9c1e-0000-4000-8000-000000000001",
            "a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90",
            1, "filesystem", "2026-11-03T12:00:00.000000+00:00"));

        Ekle("report", Imza.RaporMetni(
            "7f3a9c1e-0000-4000-8000-000000000001",
            "0f1e2d3c4b5a69788796a5b4c3d2e1f00f1e2d3c4b5a69788796a5b4c3d2e1f0",
            ScannerBilgisi.Surum, "2026.11.03.1",
            DogrulamaIstemcisi.RaporHash("{\"kanit\":[]}"),
            "2026-11-03T12:00:05.000000+00:00"));

        var govde = new
        {
            note = "C# tarafindan uretildi, Python backend dogrular. Elle duzenlemeyin.",
            scanner_version = ScannerBilgisi.Surum,
            algorithm = "ECDSA P-256 / SHA-256 / DER",
            public_key = anahtar.PublicKeyBase64(),
            fingerprint = ParmakIzi.Hesapla(anahtar.PublicKeyBase64()),
            report_body = "{\"kanit\":[]}",
            report_hash = DogrulamaIstemcisi.RaporHash("{\"kanit\":[]}"),
            cases = kayitlar,
        };

        var hedef = Path.GetFullPath(Yol);
        Directory.CreateDirectory(Path.GetDirectoryName(hedef)!);
        File.WriteAllText(hedef, JsonSerializer.Serialize(govde,
            new JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact]
    public void VektordekiPayloadlarGuncelKodlaAyni()
    {
        var hedef = Path.GetFullPath(Yol);
        if (!File.Exists(hedef)) return;

        using var belge = JsonDocument.Parse(File.ReadAllText(hedef));
        var kok = belge.RootElement;

        Assert.Equal(kok.GetProperty("report_hash").GetString(),
                     DogrulamaIstemcisi.RaporHash(kok.GetProperty("report_body").GetString()!));

        foreach (var durum in kok.GetProperty("cases").EnumerateArray())
        {
            var ad = durum.GetProperty("name").GetString();
            var beklenen = durum.GetProperty("payload").GetString();
            var uretilen = ad switch
            {
                "heartbeat" => Encoding.UTF8.GetString(Imza.HeartbeatMetni(
                    "7f3a9c1e-0000-4000-8000-000000000001",
                    "a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90",
                    1, "filesystem", "2026-11-03T12:00:00.000000+00:00")),
                "report" => Encoding.UTF8.GetString(Imza.RaporMetni(
                    "7f3a9c1e-0000-4000-8000-000000000001",
                    "0f1e2d3c4b5a69788796a5b4c3d2e1f00f1e2d3c4b5a69788796a5b4c3d2e1f0",
                    kok.GetProperty("scanner_version").GetString()!, "2026.11.03.1",
                    kok.GetProperty("report_hash").GetString()!,
                    "2026-11-03T12:00:05.000000+00:00")),
                _ => null,
            };

            Assert.Equal(beklenen, uretilen);
        }
    }
}
