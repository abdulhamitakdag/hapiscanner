using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using HapiScanner.Core.Arayuz;
using System.Windows.Media;

namespace HapiScanner.App.Ekranlar;

public partial class TaramaEkrani : UserControl
{
    private AkisYoneticisi Yonetici => (AkisYoneticisi)DataContext;

    public TaramaEkrani()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => GunlugeAbone();
        Loaded += (_, _) => GunlugeAbone();
    }

    private INotifyCollectionChanged? _izlenen;

    private void GunlugeAbone()
    {
        if (DataContext is null) return;
        if (_izlenen is not null) _izlenen.CollectionChanged -= GunlukDegisti;
        _izlenen = Yonetici.Gunluk;
        _izlenen.CollectionChanged += GunlukDegisti;
    }

    private void GunlukDegisti(object? gonderen, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;
        Kaydirici()?.ScrollToEnd();
    }

    private ScrollViewer? Kaydirici()
    {
        var hedef = (DependencyObject?)GunlukListesi;
        while (hedef is not null)
        {
            if (VisualTreeHelper.GetChildrenCount(hedef) == 0) return null;
            var cocuk = VisualTreeHelper.GetChild(hedef, 0);
            if (cocuk is ScrollViewer kaydirici) return kaydirici;
            hedef = cocuk;
        }
        return null;
    }

    private void Iptal(object gonderen, RoutedEventArgs e) => Yonetici.Iptal();
}
