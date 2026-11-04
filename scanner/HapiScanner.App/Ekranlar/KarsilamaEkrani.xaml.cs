using System.Windows;
using System.Windows.Controls;
using HapiScanner.Core.Arayuz;

namespace HapiScanner.App.Ekranlar;

public partial class KarsilamaEkrani : UserControl
{
    private AkisYoneticisi Yonetici => (AkisYoneticisi)DataContext;

    public KarsilamaEkrani() => InitializeComponent();

    private void Basla(object gonderen, RoutedEventArgs e) => Yonetici.Basla();
}
