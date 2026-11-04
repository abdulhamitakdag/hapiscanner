using System.Buffers.Binary;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using HapiScanner.Core;
using HapiScanner.Core.Runtime;

namespace HapiScanner.Windows;

public sealed class WindowsBaglantiBulucu : IOyunBaglantiBulucu
{
    private readonly IProcessSaglayici _processler;

    public WindowsBaglantiBulucu(IProcessSaglayici? processler = null)
        => _processler = processler ?? new WindowsProcessSaglayici();

    public OyunBaglantiSonucu Bul(IReadOnlyList<string> hapicraftEndpointleri)
    {
        var adaylar = _processler.Processler()
            .Select(p => new AdayProcess(p.ProcessId, p.Ad, p.KomutSatiri))
            .ToList();

        var baglantilar = Baglantilar();
        return BaglantiEslestirici.Eslestir(adaylar, baglantilar, hapicraftEndpointleri);
    }

    private IReadOnlyList<TcpBaglantisi> Baglantilar()
    {
        if (!OperatingSystem.IsWindows()) return Array.Empty<TcpBaglantisi>();
        try
        {
            var liste = new List<TcpBaglantisi>();
            liste.AddRange(Tablo(AF_INET));
            liste.AddRange(Tablo(AF_INET6));
            return liste;
        }
        catch (Exception)
        {

            return Array.Empty<TcpBaglantisi>();
        }
    }

    private const int AF_INET = 2;
    private const int AF_INET6 = 23;

    private const int TCP_TABLE_OWNER_PID_CONNECTIONS = 4;

    private const int MIB_TCP_STATE_ESTAB = 5;

    [SupportedOSPlatform("windows")]
    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        byte[]? pTcpTable, ref int pdwSize, bool bOrder, int ulAf, int TableClass, uint Reserved);

    [SupportedOSPlatform("windows")]
    private static IEnumerable<TcpBaglantisi> Tablo(int aileSurumu)
    {
        var boyut = 0;
        GetExtendedTcpTable(null, ref boyut, true, aileSurumu,
            TCP_TABLE_OWNER_PID_CONNECTIONS, 0);
        if (boyut <= 0) return Array.Empty<TcpBaglantisi>();

        var tampon = new byte[boyut];
        if (GetExtendedTcpTable(tampon, ref boyut, true, aileSurumu,
                TCP_TABLE_OWNER_PID_CONNECTIONS, 0) != 0)
            return Array.Empty<TcpBaglantisi>();

        return aileSurumu == AF_INET ? Ipv4Coz(tampon) : Ipv6Coz(tampon);
    }

    private static List<TcpBaglantisi> Ipv4Coz(byte[] tampon)
    {
        var sonuc = new List<TcpBaglantisi>();
        var sayi = (int)BinaryPrimitives.ReadUInt32LittleEndian(tampon);
        const int satirBoyutu = 24;

        for (var i = 0; i < sayi; i++)
        {
            var konum = 4 + (i * satirBoyutu);
            if (konum + satirBoyutu > tampon.Length) break;

            var durum = (int)BinaryPrimitives.ReadUInt32LittleEndian(tampon.AsSpan(konum));
            var uzakAdres = new IPAddress(tampon.AsSpan(konum + 12, 4).ToArray());
            var uzakPort = PortCoz(tampon, konum + 16);
            var pid = (int)BinaryPrimitives.ReadUInt32LittleEndian(tampon.AsSpan(konum + 20));

            sonuc.Add(new TcpBaglantisi(pid, uzakAdres.ToString(), uzakPort, DurumAdi(durum)));
        }

        return sonuc;
    }

    private static List<TcpBaglantisi> Ipv6Coz(byte[] tampon)
    {
        var sonuc = new List<TcpBaglantisi>();
        var sayi = (int)BinaryPrimitives.ReadUInt32LittleEndian(tampon);
        const int satirBoyutu = 56;

        for (var i = 0; i < sayi; i++)
        {
            var konum = 4 + (i * satirBoyutu);
            if (konum + satirBoyutu > tampon.Length) break;

            var uzakAdres = new IPAddress(tampon.AsSpan(konum + 24, 16).ToArray());
            var uzakPort = PortCoz(tampon, konum + 44);
            var durum = (int)BinaryPrimitives.ReadUInt32LittleEndian(tampon.AsSpan(konum + 48));
            var pid = (int)BinaryPrimitives.ReadUInt32LittleEndian(tampon.AsSpan(konum + 52));

            sonuc.Add(new TcpBaglantisi(pid, uzakAdres.ToString(), uzakPort, DurumAdi(durum)));
        }

        return sonuc;
    }

    private static int PortCoz(byte[] tampon, int konum)
        => (tampon[konum] << 8) | tampon[konum + 1];

    private static string DurumAdi(int durum)
        => durum == MIB_TCP_STATE_ESTAB ? "ESTABLISHED" : durum.ToString();
}
