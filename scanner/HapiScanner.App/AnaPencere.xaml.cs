using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using HapiScanner.Core.Arayuz;

namespace HapiScanner.App;

public partial class AnaPencere : Window
{
    // AllowsTransparency acikken WPF pencereyi yazilimda cizer ve Viewport3D
    // bulanik kalir. Pencere opak; yuvarlak koseyi DWM'den istiyoruz.
    private const int YuvarlakKose = 33;
    private const int Yuvarlak = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr pencere, int ozellik, ref int deger, int boyut);

    public AnaPencere()
    {
        InitializeComponent();
        DataContext = Uygulama.Yonetici;
        SourceInitialized += (_, _) => KoseleriYuvarla();
    }

    private void KoseleriYuvarla()
    {
        try
        {
            var tutamac = new WindowInteropHelper(this).Handle;
            var deger = Yuvarlak;
            DwmSetWindowAttribute(tutamac, YuvarlakKose, ref deger, sizeof(int));
        }
        catch (DllNotFoundException)
        {
            // Windows 10 ve oncesinde kose duz kalir; sorun degil.
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    public AkisYoneticisi Yonetici => Uygulama.Yonetici;

    public bool TanitimModu => Uygulama.TanitimModu;

    private void BaslikSurukle(object gonderen, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void Kucult(object gonderen, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Kapat(object gonderen, RoutedEventArgs e)
    {

        Yonetici.Iptal();
        Close();
    }
}
