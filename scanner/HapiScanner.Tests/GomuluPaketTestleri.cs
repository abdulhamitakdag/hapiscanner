using HapiScanner.Core.Tarama;
using HapiScanner.Core.Tespit;

namespace HapiScanner.Tests;

public class GomuluPaketTestleri
{
    [Fact]
    public void GomuluPaketExeIcindeTasinir()
    {
        Assert.True(new GomuluKuralPaketiKaynagi().VarMi);
    }

    [Fact]
    public async Task GomuluPaketYayinciAnahtariylaKabulEdilir()
    {
        var (paket, imza) = await new GomuluKuralPaketiKaynagi()
            .GetirAsync(CancellationToken.None);

        var sonuc = new KuralPaketiYukleyici(Yayinci.PublicKeyler).Yukle(paket, imza);

        Assert.True(sonuc.Kabul, sonuc.Aciklama ?? sonuc.Neden.ToString());
    }

    [Fact]
    public void PaketDosyasiYokkenGomuluKaynagaDusulur()
    {
        var bos = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(bos);
        try
        {
            Assert.IsType<GomuluKuralPaketiKaynagi>(KuralPaketiKaynaklari.Bul(bos));
        }
        finally
        {
            Directory.Delete(bos, recursive: true);
        }
    }

    [Fact]
    public void PaketDosyasiVarkenDosyaKaynagiKullanilir()
    {
        var dizin = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dizin);
        try
        {
            File.WriteAllText(Path.Combine(dizin, Yayinci.PaketDosyasi), "{}");
            File.WriteAllText(Path.Combine(dizin, Yayinci.ImzaDosyasi), "aa");

            Assert.IsType<DosyaKuralPaketiKaynagi>(KuralPaketiKaynaklari.Bul(dizin));
        }
        finally
        {
            Directory.Delete(dizin, recursive: true);
        }
    }
}
