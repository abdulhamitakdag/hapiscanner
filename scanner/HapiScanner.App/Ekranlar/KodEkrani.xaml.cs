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

    private bool _bicimleniyor;

    private void KodDegisti(object gonderen, TextChangedEventArgs e)
    {
        if (_bicimleniyor || gonderen is not TextBox kutu) return;

        var bicimli = HapiScanner.Core.Token.Bicimle(kutu.Text);
        if (bicimli == kutu.Text) return;

        _bicimleniyor = true;
        try
        {
            kutu.Text = bicimli;
            kutu.CaretIndex = bicimli.Length;
        }
        finally
        {
            _bicimleniyor = false;
        }
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
