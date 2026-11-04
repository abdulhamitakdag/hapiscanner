using System.Windows;
using System.Windows.Threading;
using HapiScanner.App.Akis;
using HapiScanner.Core.Arayuz;

namespace HapiScanner.App;

public partial class Uygulama : Application
{

    public static AkisYoneticisi Yonetici { get; private set; } = null!;

    public static bool TanitimModu { get; private set; }

    public static string? KurulumUyarisi { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        TanitimModu = e.Args.Any(a =>
            a.Equals("--tanitim", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--demo", StringComparison.OrdinalIgnoreCase));

        string? uyari = null;
        ITaramaAkisi akis = TanitimModu
            ? new TanitimAkisi()
            : AkisKurucu.Kur(e.Args, out uyari);

        KurulumUyarisi = uyari;
        Yonetici = new AkisYoneticisi(akis, SynchronizationContext.Current);

        DispatcherUnhandledException += BeklenmedikHata;
    }

    private void BeklenmedikHata(object gonderen, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "Beklenmedik bir hata oluştu ve tarama durduruldu.\n\n" + e.Exception.Message,
            "HapiScanner", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
