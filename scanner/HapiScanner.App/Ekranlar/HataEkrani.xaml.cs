using System.Windows;
using System.Windows.Controls;
using HapiScanner.Core.Arayuz;

namespace HapiScanner.App.Ekranlar;

public partial class HataEkrani : UserControl
{
    private AkisYoneticisi Yonetici => (AkisYoneticisi)DataContext;

    public HataEkrani() => InitializeComponent();

    private void TekrarDene(object gonderen, RoutedEventArgs e) => Yonetici.Geri();

    private void Kapat(object gonderen, RoutedEventArgs e) => Window.GetWindow(this)?.Close();
}
