using System.Windows;
using System.Windows.Controls;
using HapiScanner.Core.Arayuz;

namespace HapiScanner.App.Ekranlar;

public partial class SonucEkrani : UserControl
{
    private AkisYoneticisi Yonetici => (AkisYoneticisi)DataContext;

    public SonucEkrani() => InitializeComponent();

    private void Kapat(object gonderen, RoutedEventArgs e) => Window.GetWindow(this)?.Close();
}
