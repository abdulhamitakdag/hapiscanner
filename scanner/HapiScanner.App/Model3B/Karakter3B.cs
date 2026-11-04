using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using HapiScanner.Core.Arayuz;
using HapiScanner.Core.Model3B;

namespace HapiScanner.App.Model3B;

public sealed class Karakter3B : FrameworkElement
{

    private const double TamBoyGenisligi = 44;
    private const double PortreGenisligi = 26;
    private const double TamBoyMerkezi = 16;
    private const double PortreMerkezi = 23;

    private const double UvPayi = 0.02;

    private readonly Viewport3D _goruntu = new();
    private readonly OrthographicCamera _kamera = new();
    private readonly AnimasyonSurucusu _surucu = new();
    private readonly Stopwatch _saat = new();
    private readonly Dictionary<Eklem, Transform3DGroup> _eklemler = new();
    private readonly Transform3DGroup _kok = new();
    private readonly Dictionary<Esya, ImageBrush> _esyaFircalari = new();

    private TranslateTransform3D _kokKaydirma = new();
    private AxisAngleRotation3D _kokDonusu = new(new Vector3D(0, 1, 0), 0);
    private AxisAngleRotation3D _kokEgimi = new(new Vector3D(0, 0, 1), 0);
    private readonly Dictionary<Eklem, (AxisAngleRotation3D X, AxisAngleRotation3D Z, AxisAngleRotation3D Y)> _acilar = new();

    private GeometryModel3D? _esyaModeli;
    private AxisAngleRotation3D _esyaDonusu = new(new Vector3D(0, 0, 1), 0);
    private DiffuseMaterial? _esyaMalzemesi;
    private double _sonSaniye;
    private bool _kareyeBagli;

    public Karakter3B()
    {
        AddVisualChild(_goruntu);
        AddLogicalChild(_goruntu);
        RenderOptions.SetBitmapScalingMode(_goruntu, BitmapScalingMode.NearestNeighbor);
        Kur();
        IsVisibleChanged += (_, _) => KareBaginiTazele();
        Loaded += (_, _) => KareBaginiTazele();
        Unloaded += (_, _) => KareBaginiTazele();
    }

    public static readonly DependencyProperty EkranOzelligi = DependencyProperty.Register(
        nameof(AktifEkran), typeof(Ekran), typeof(Karakter3B),
        new PropertyMetadata(HapiScanner.Core.Arayuz.Ekran.Karsilama, AnimasyonDegisti));

    public static readonly DependencyProperty AsamaOzelligi = DependencyProperty.Register(
        nameof(Asama), typeof(Asama), typeof(Karakter3B),
        new PropertyMetadata(null, AnimasyonDegisti));

    public static readonly DependencyProperty SonucTonuOzelligi = DependencyProperty.Register(
        nameof(SonucTonu), typeof(string), typeof(Karakter3B),
        new PropertyMetadata(null, AnimasyonDegisti));

    public static readonly DependencyProperty IlerlemeOzelligi = DependencyProperty.Register(
        nameof(Ilerleme), typeof(double), typeof(Karakter3B),
        new PropertyMetadata(0.0));

    public Ekran AktifEkran
    {
        get => (Ekran)GetValue(EkranOzelligi);
        set => SetValue(EkranOzelligi, value);
    }

    public Asama? Asama
    {
        get => (Asama?)GetValue(AsamaOzelligi);
        set => SetValue(AsamaOzelligi, value);
    }

    public string? SonucTonu
    {
        get => (string?)GetValue(SonucTonuOzelligi);
        set => SetValue(SonucTonuOzelligi, value);
    }

    public double Ilerleme
    {
        get => (double)GetValue(IlerlemeOzelligi);
        set => SetValue(IlerlemeOzelligi, value);
    }

    private static void AnimasyonDegisti(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((Karakter3B)d).AnimasyonuSec();

    private void AnimasyonuSec()
    {
        var tur = Animasyonlar.Sec(AktifEkran, Asama, SonucTonu);
        _surucu.Gecis(tur);
        EsyayiSec(Animasyonlar.Tanim(tur).Esya);
        KamerayiAyarla(Animasyonlar.Tanim(tur).Yakinlik);
    }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => _goruntu;

    protected override Size MeasureOverride(Size kullanilabilir)
    {
        _goruntu.Measure(kullanilabilir);
        return new Size(
            double.IsInfinity(kullanilabilir.Width) ? 260 : kullanilabilir.Width,
            double.IsInfinity(kullanilabilir.Height) ? 340 : kullanilabilir.Height);
    }

    protected override Size ArrangeOverride(Size son)
    {
        _goruntu.Arrange(new Rect(son));
        return son;
    }

    private void Kur()
    {
        var skin = Doku("pack://application:,,,/Varliklar/kaynak/hapitolar_skin.png");
        var pelerin = Doku("pack://application:,,,/Varliklar/kaynak/hapitolar_pelerin.png");

        var ince = false;
        var parcalar = SkinYerlesimi.Parcalar(ince, eskiBicim: false, pelerinVar: true);

        var skinFircasi = Firca(PikselBuyut(skin, DokuBuyutme));
        var pelerinFircasi = Firca(PikselBuyut(pelerin, DokuBuyutme));

        var ebeveyn = Iskelet.Ebeveyn;

        var kokGrup = new Model3DGroup();
        var gruplar = new Dictionary<Eklem, Model3DGroup>();

        _kokKaydirma = new TranslateTransform3D();
        _kok.Children.Add(new RotateTransform3D(_kokEgimi));
        _kok.Children.Add(new RotateTransform3D(_kokDonusu));
        _kok.Children.Add(_kokKaydirma);
        kokGrup.Transform = _kok;

        foreach (var eklem in ebeveyn.Keys)
        {
            var yerelPivot = Iskelet.YerelPivot(parcalar, eklem);

            var x = new AxisAngleRotation3D(new Vector3D(1, 0, 0), 0);
            var z = new AxisAngleRotation3D(new Vector3D(0, 0, 1), 0);
            var y = new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0);
            _acilar[eklem] = (x, z, y);

            var donusum = new Transform3DGroup();
            donusum.Children.Add(new RotateTransform3D(x));
            donusum.Children.Add(new RotateTransform3D(z));
            donusum.Children.Add(new RotateTransform3D(y));
            donusum.Children.Add(new TranslateTransform3D(
                yerelPivot.X, yerelPivot.Y, yerelPivot.Z));

            _eklemler[eklem] = donusum;
            gruplar[eklem] = new Model3DGroup { Transform = donusum };
        }

        foreach (var (eklem, grup) in gruplar)
        {
            if (ebeveyn[eklem] is { } e) gruplar[e].Children.Add(grup);
            else kokGrup.Children.Add(grup);
        }

        foreach (var parca in parcalar)
        {
            var pelerinMi = parca.Eklem == Eklem.Pelerin;
            var mesh = KutuMesh(
                parca,
                pelerinMi ? pelerin.PixelWidth : skin.PixelWidth,
                pelerinMi ? pelerin.PixelHeight : skin.PixelHeight);

            var malzeme = new DiffuseMaterial(pelerinMi ? pelerinFircasi : skinFircasi);
            var model = new GeometryModel3D(mesh, malzeme);

            if (parca.IkinciKatman || pelerinMi) model.BackMaterial = malzeme;

            gruplar[parca.Eklem].Children.Add(model);
        }

        _esyaModeli = EsyaModeli();
        gruplar[Eklem.SagKol].Children.Add(_esyaModeli);

        var isiklar = new Model3DGroup();
        isiklar.Children.Add(new AmbientLight(Color.FromRgb(0x78, 0x7E, 0x7B)));
        isiklar.Children.Add(new DirectionalLight(
            Color.FromRgb(0xB4, 0xBC, 0xB6), new Vector3D(-0.45, -0.7, -1)));
        isiklar.Children.Add(new DirectionalLight(
            Color.FromRgb(0x3A, 0x52, 0x44), new Vector3D(0.8, -0.2, 0.6)));

        var sahne = new Model3DGroup();
        sahne.Children.Add(isiklar);
        sahne.Children.Add(kokGrup);

        _goruntu.Children.Add(new ModelVisual3D { Content = sahne });
        _goruntu.Camera = _kamera;
        _kamera.UpDirection = new Vector3D(0, 1, 0);
        KamerayiAyarla(0);
        AnimasyonuSec();
        Uygula(_surucu.AktifPoz);
    }

    private static BitmapImage Doku(string yol)
    {
        var resim = new BitmapImage();
        resim.BeginInit();
        resim.UriSource = new Uri(yol);
        resim.CacheOption = BitmapCacheOption.OnLoad;
        resim.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        resim.EndInit();
        resim.Freeze();
        return resim;
    }

    // WPF 3B malzemelerinde doku ornekleme daima dogrusaldir; BitmapScalingMode
    // malzemeye islemez. 64x64 skini piksel piksel buyuterek dogrusal
    // filtrelemenin bulandiracagi bir sey birakmiyoruz.
    private const int DokuBuyutme = 8;

    private static BitmapSource PikselBuyut(BitmapSource kaynak, int kat)
    {
        var g = kaynak.PixelWidth;
        var y = kaynak.PixelHeight;

        var bgra = new FormatConvertedBitmap(kaynak, PixelFormats.Bgra32, null, 0);

        var kaynakSatir = g * 4;
        var kaynakPiksel = new byte[kaynakSatir * y];
        bgra.CopyPixels(kaynakPiksel, kaynakSatir, 0);

        var hedefG = g * kat;
        var hedefY = y * kat;
        var hedefSatir = hedefG * 4;
        var hedefPiksel = new byte[hedefSatir * hedefY];

        for (var sy = 0; sy < y; sy++)
        {
            for (var sx = 0; sx < g; sx++)
            {
                var k = sy * kaynakSatir + sx * 4;
                for (var dy = 0; dy < kat; dy++)
                {
                    var satirTabani = (sy * kat + dy) * hedefSatir + sx * kat * 4;
                    for (var dx = 0; dx < kat; dx++)
                    {
                        var h = satirTabani + dx * 4;
                        hedefPiksel[h] = kaynakPiksel[k];
                        hedefPiksel[h + 1] = kaynakPiksel[k + 1];
                        hedefPiksel[h + 2] = kaynakPiksel[k + 2];
                        hedefPiksel[h + 3] = kaynakPiksel[k + 3];
                    }
                }
            }
        }

        var sonuc = BitmapSource.Create(
            hedefG, hedefY, 96, 96, PixelFormats.Bgra32, null, hedefPiksel, hedefSatir);
        sonuc.Freeze();
        return sonuc;
    }

    private static ImageBrush Firca(BitmapSource doku)
    {
        var firca = new ImageBrush(doku)
        {
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.Fill,
            TileMode = TileMode.None,
        };

        RenderOptions.SetBitmapScalingMode(firca, BitmapScalingMode.NearestNeighbor);
        RenderOptions.SetEdgeMode(firca, EdgeMode.Aliased);
        firca.Freeze();
        return firca;
    }

    private void KamerayiAyarla(double yakinlik)
    {
        var t = Math.Clamp(yakinlik, 0, 1);
        _kamera.Width = TamBoyGenisligi + (PortreGenisligi - TamBoyGenisligi) * t;
        var merkez = TamBoyMerkezi + (PortreMerkezi - TamBoyMerkezi) * t;
        _kamera.Position = new Point3D(0, merkez + 2.5, 80);
        _kamera.LookDirection = new Vector3D(0, -0.045, -1);
    }

    private static MeshGeometry3D KutuMesh(ParcaTanimi parca, int dokuGenisligi, int dokuYuksekligi)
    {
        var mesh = new MeshGeometry3D();

        foreach (var yuz in Kutu.Yuzler(parca))
        {
            var sol = Math.Min(yuz.Uv.X, yuz.Uv.Sag) + UvPayi;
            var sag = Math.Max(yuz.Uv.X, yuz.Uv.Sag) - UvPayi;
            var ust = yuz.Uv.Y + UvPayi;
            var alt = yuz.Uv.Alt - UvPayi;

            var (u0, u1) = yuz.Uv.Genislik < 0 ? (sag, sol) : (sol, sag);

            var taban = mesh.Positions.Count;
            foreach (var kose in new[] { yuz.SolUst, yuz.SagUst, yuz.SolAlt, yuz.SagAlt })
                mesh.Positions.Add(new Point3D(kose.X, kose.Y, kose.Z));

            var normal = new Vector3D(yuz.Normal.X, yuz.Normal.Y, yuz.Normal.Z);
            for (var i = 0; i < 4; i++) mesh.Normals.Add(normal);

            mesh.TextureCoordinates.Add(new Point(u0 / dokuGenisligi, ust / dokuYuksekligi));
            mesh.TextureCoordinates.Add(new Point(u1 / dokuGenisligi, ust / dokuYuksekligi));
            mesh.TextureCoordinates.Add(new Point(u0 / dokuGenisligi, alt / dokuYuksekligi));
            mesh.TextureCoordinates.Add(new Point(u1 / dokuGenisligi, alt / dokuYuksekligi));

            foreach (var i in new[] { 2, 3, 1, 2, 1, 0 }) mesh.TriangleIndices.Add(taban + i);
        }

        mesh.Freeze();
        return mesh;
    }

    private GeometryModel3D EsyaModeli()
    {
        const double boy = 9;
        var mesh = new MeshGeometry3D();
        mesh.Positions.Add(new Point3D(-boy / 2, boy / 2, 0));
        mesh.Positions.Add(new Point3D(boy / 2, boy / 2, 0));
        mesh.Positions.Add(new Point3D(-boy / 2, -boy / 2, 0));
        mesh.Positions.Add(new Point3D(boy / 2, -boy / 2, 0));
        for (var i = 0; i < 4; i++) mesh.Normals.Add(new Vector3D(0, 0, 1));
        mesh.TextureCoordinates.Add(new Point(0, 0));
        mesh.TextureCoordinates.Add(new Point(1, 0));
        mesh.TextureCoordinates.Add(new Point(0, 1));
        mesh.TextureCoordinates.Add(new Point(1, 1));
        foreach (var i in new[] { 2, 3, 1, 2, 1, 0 }) mesh.TriangleIndices.Add(i);
        mesh.Freeze();

        _esyaMalzemesi = new DiffuseMaterial(Brushes.Transparent);

        var donusum = new Transform3DGroup();
        donusum.Children.Add(new RotateTransform3D(_esyaDonusu));
        donusum.Children.Add(new TranslateTransform3D(0, -11.5, 4.5));

        return new GeometryModel3D(mesh, _esyaMalzemesi)
        {
            BackMaterial = _esyaMalzemesi,
            Transform = donusum,
        };
    }

    private static string? EsyaDosyasi(Esya esya) => esya switch
    {
        Esya.Kilit => "ikon_kilit.png",
        Esya.Buyutec => "ikon_buyutec.png",
        Esya.Dosya => "ikon_dosya.png",
        Esya.Kalkan => "ikon_kalkan.png",
        Esya.Sunucu => "ikon_sunucu.png",
        Esya.Onay => "ikon_onay.png",
        Esya.Uyari => "ikon_uyari.png",
        _ => null,
    };

    private void EsyayiSec(Esya esya)
    {
        if (_esyaMalzemesi is null) return;

        if (EsyaDosyasi(esya) is not { } dosya)
        {
            _esyaMalzemesi.Brush = Brushes.Transparent;
            return;
        }

        if (!_esyaFircalari.TryGetValue(esya, out var firca))
        {
            firca = new ImageBrush(Doku($"pack://application:,,,/Varliklar/{dosya}"))
            {
                Stretch = Stretch.Uniform,
            };
            RenderOptions.SetBitmapScalingMode(firca, BitmapScalingMode.NearestNeighbor);
            _esyaFircalari[esya] = firca;
        }

        _esyaMalzemesi.Brush = firca;
    }

    private void KareBaginiTazele()
    {
        var gerekli = IsVisible && IsLoaded;
        if (gerekli == _kareyeBagli) return;

        _kareyeBagli = gerekli;
        if (gerekli)
        {
            _saat.Restart();
            _sonSaniye = 0;
            CompositionTarget.Rendering += Kare;
        }
        else
        {
            CompositionTarget.Rendering -= Kare;
            _saat.Stop();
        }
    }

    private void Kare(object? gonderen, EventArgs e)
    {
        var simdi = _saat.Elapsed.TotalSeconds;
        var gecen = simdi - _sonSaniye;
        _sonSaniye = simdi;
        Uygula(_surucu.Guncelle(gecen, Ilerleme));
    }

    private void Uygula(Poz poz)
    {
        foreach (var (eklem, aci) in _acilar)
        {
            var d = poz[eklem];
            aci.X.Angle = d.X;
            aci.Z.Angle = d.Z;
            aci.Y.Angle = d.Y;
        }

        _kokKaydirma.OffsetY = poz.Yukseklik;
        _kokDonusu.Angle = poz.ModelDonusu;
        _kokEgimi.Angle = poz.ModelEgimi;
        _esyaDonusu.Angle = poz.EsyaDonusu;

        if (_esyaMalzemesi?.Brush is ImageBrush simge && !simge.IsFrozen)
            simge.Opacity = Math.Clamp(poz.EsyaGorunurlugu, 0, 1);
    }
}
