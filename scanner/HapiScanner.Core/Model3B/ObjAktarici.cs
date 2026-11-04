using System.Globalization;
using System.Text;

namespace HapiScanner.Core.Model3B;

public static class ObjAktarici
{
    private static string S(double d) => d.ToString("0.#####", CultureInfo.InvariantCulture);

    public static string Yaz(
        IEnumerable<ParcaTanimi> parcalar,
        Poz poz,
        int dokuGenisligi = SkinYerlesimi.DokuGenisligi,
        int dokuYuksekligi = SkinYerlesimi.DokuYuksekligi,
        int pelerinGenisligi = 64,
        int pelerinYuksekligi = 32)
    {
        var liste = parcalar as IList<ParcaTanimi> ?? parcalar.ToList();
        var donusumler = Iskelet.Donusumler(liste, poz);

        var sb = new StringBuilder();
        sb.AppendLine("# HapiScanner 3B karakter onizlemesi (uretilmis dosya)");
        var sayac = 0;

        foreach (var parca in liste)
        {
            var m = donusumler[parca.Eklem];
            var pelerinMi = parca.Eklem == Eklem.Pelerin;
            var dg = pelerinMi ? pelerinGenisligi : dokuGenisligi;
            var dy = pelerinMi ? pelerinYuksekligi : dokuYuksekligi;

            sb.AppendLine($"o {parca.Ad}");
            sb.AppendLine(pelerinMi ? "usemtl pelerin" : "usemtl skin");

            foreach (var yuz in Kutu.Yuzler(parca))
            {
                foreach (var kose in new[] { yuz.SolAlt, yuz.SagAlt, yuz.SagUst, yuz.SolUst })
                {
                    var d = m.Uygula(kose);
                    sb.AppendLine($"v {S(d.X)} {S(d.Y)} {S(d.Z)}");
                }

                var sol = Math.Min(yuz.Uv.X, yuz.Uv.Sag) / (double)dg;
                var sag = Math.Max(yuz.Uv.X, yuz.Uv.Sag) / (double)dg;
                var (u0, u1) = yuz.Uv.Genislik < 0 ? (sag, sol) : (sol, sag);

                var vUst = 1 - yuz.Uv.Y / (double)dy;
                var vAlt = 1 - yuz.Uv.Alt / (double)dy;

                sb.AppendLine($"vt {S(u0)} {S(vAlt)}");
                sb.AppendLine($"vt {S(u1)} {S(vAlt)}");
                sb.AppendLine($"vt {S(u1)} {S(vUst)}");
                sb.AppendLine($"vt {S(u0)} {S(vUst)}");

                var t = sayac * 4;
                sb.AppendLine(
                    $"f {t + 1}/{t + 1} {t + 2}/{t + 2} {t + 3}/{t + 3} {t + 4}/{t + 4}");
                sayac++;
            }
        }

        return sb.ToString();
    }
}
