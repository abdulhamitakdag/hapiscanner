using System.Windows;
using System.Windows.Controls;
using HapiScanner.Core.Arayuz;
using System.Windows.Input;

namespace HapiScanner.App.Ekranlar;

public partial class KodEkrani : UserControl
{
    private AkisYoneticisi Yonetici => (AkisYoneticisi)DataContext;

    public KodEkrani()
    {
        InitializeComponent();
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true) KodKutusuAlani.Focus();
        };
    }

    private async void Baglan(object gonderen, RoutedEventArgs e)
        => await Yonetici.BaglanVeTaraAsync();

    private async void KodaBasildi(object gonderen, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || !Yonetici.BaglanilabilirMi) return;
        e.Handled = true;
        await Yonetici.BaglanVeTaraAsync();
    }

    private void Geri(object gonderen, RoutedEventArgs e) => Yonetici.Geri();
}
