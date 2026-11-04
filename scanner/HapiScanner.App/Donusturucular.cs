using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using HapiScanner.Core.Arayuz;

namespace HapiScanner.App;

public sealed class EkranGorunurlugu : IValueConverter
{
    public object Convert(object? value, Type hedef, object? parametre, CultureInfo kultur)
    {
        if (value is not Ekran aktif || parametre is not string ad) return Visibility.Collapsed;
        return Enum.TryParse<Ekran>(ad, out var beklenen) && beklenen == aktif
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type hedef, object? parametre, CultureInfo kultur)
        => Binding.DoNothing;
}

public sealed class EkranlardanBiri : IValueConverter
{
    public object Convert(object? value, Type hedef, object? parametre, CultureInfo kultur)
    {
        if (value is not Ekran aktif || parametre is not string liste) return Visibility.Collapsed;
        foreach (var ad in liste.Split('|', StringSplitOptions.RemoveEmptyEntries))
            if (Enum.TryParse<Ekran>(ad.Trim(), out var e) && e == aktif)
                return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type hedef, object? parametre, CultureInfo kultur)
        => Binding.DoNothing;
}

public sealed class DoluysaGorunur : IValueConverter
{
    public object Convert(object? value, Type hedef, object? parametre, CultureInfo kultur)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type hedef, object? parametre, CultureInfo kultur)
        => Binding.DoNothing;
}

public sealed class TersBool : IValueConverter
{
    public object Convert(object? value, Type hedef, object? parametre, CultureInfo kultur)
        => value is bool b && !b;

    public object ConvertBack(object? value, Type hedef, object? parametre, CultureInfo kultur)
        => value is bool b && !b;
}

public sealed class YuzdeSutunu : IValueConverter
{
    public object Convert(object? value, Type hedef, object? parametre, CultureInfo kultur)
    {
        var yuzde = value is int i ? Math.Clamp(i, 0, 100) : 0;
        var ters = (parametre as string) == "ters";
        return new GridLength(ters ? 100 - yuzde : yuzde, GridUnitType.Star);
    }

    public object ConvertBack(object? value, Type hedef, object? parametre, CultureInfo kultur)
        => Binding.DoNothing;
}

public sealed class AsamaRengi : IValueConverter
{
    public object Convert(object? value, Type hedef, object? parametre, CultureInfo kultur)
    {
        var anahtar = value switch
        {
            AsamaDurumu.Bitti => "MarkaYesil",
            AsamaDurumu.Calisiyor => "Metin",
            AsamaDurumu.Basarisiz => "Kotu",
            AsamaDurumu.Atlandi => "MetinSolgun",
            _ => "MetinSolgun",
        };
        return Application.Current.TryFindResource(anahtar) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type hedef, object? parametre, CultureInfo kultur)
        => Binding.DoNothing;
}

public sealed class TonRengi : IValueConverter
{
    public object Convert(object? value, Type hedef, object? parametre, CultureInfo kultur)
    {
        var anahtar = (value as string) switch
        {
            "iyi" => "MarkaYesil",
            "uyari" => "Uyari",
            "kotu" => "Kotu",
            _ => "MetinSolgun",
        };
        return Application.Current.TryFindResource(anahtar) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type hedef, object? parametre, CultureInfo kultur)
        => Binding.DoNothing;
}

public sealed class TonIkonu : IValueConverter
{
    public object? Convert(object? value, Type hedef, object? parametre, CultureInfo kultur)
    {
        var anahtar = (value as string) switch
        {
            "iyi" => "IkonOnay",
            "uyari" => "IkonUyari",
            "kotu" => "IkonUyari",
            _ => "IkonKalkan",
        };
        return Application.Current.TryFindResource(anahtar);
    }

    public object ConvertBack(object? value, Type hedef, object? parametre, CultureInfo kultur)
        => Binding.DoNothing;
}

public sealed class SifirsaGorunur : IValueConverter
{
    public object Convert(object? value, Type hedef, object? parametre, CultureInfo kultur)
        => value is int sayi && sayi == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type hedef, object? parametre, CultureInfo kultur)
        => Binding.DoNothing;
}

public sealed class AdimRengi : IMultiValueConverter
{
    public object Convert(object?[] degerler, Type hedef, object? parametre, CultureInfo kultur)
    {
        var anahtar = "MetinSolgun";
        if (degerler.Length >= 2 && degerler[0] is Ekran aktif &&
            degerler[1] is string ad && Enum.TryParse<Ekran>(ad, out var adim))
        {

            var normal = aktif switch
            {
                Ekran.Baglaniyor => Ekran.Tarama,
                Ekran.Hata => Ekran.Tarama,
                _ => aktif,
            };
            anahtar = adim == normal ? "MarkaYesil"
                : (int)adim < (int)normal ? "MetinIkincil"
                : "MetinSolgun";
        }
        return Application.Current.TryFindResource(anahtar) as Brush ?? Brushes.Gray;
    }

    public object[] ConvertBack(object? value, Type[] hedefler, object? parametre, CultureInfo kultur)
        => throw new NotSupportedException();
}
