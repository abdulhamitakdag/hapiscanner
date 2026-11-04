using System.Windows;
using System.Windows.Controls;
using HapiScanner.Core.Arayuz;

namespace HapiScanner.App.Ekranlar;

public partial class GizlilikEkrani : UserControl
{
    private AkisYoneticisi Yonetici => (AkisYoneticisi)DataContext;

    public GizlilikEkrani() => InitializeComponent();

    private void Devam(object gonderen, RoutedEventArgs e) => Yonetici.GizlilikOnayla();

    private void Geri(object gonderen, RoutedEventArgs e) => Yonetici.Geri();
}
