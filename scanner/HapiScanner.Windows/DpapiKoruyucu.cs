using System.Runtime.Versioning;
using System.Security.Cryptography;
using HapiScanner.Core;

namespace HapiScanner.Windows;

[SupportedOSPlatform("windows")]
public sealed class DpapiKoruyucu : IAnahtarKoruyucu
{

    private static readonly byte[] Entropi =
        System.Text.Encoding.UTF8.GetBytes("HapiScanner.CihazAnahtari.v1");

    public byte[] Sarmala(byte[] ham)
        => ProtectedData.Protect(ham, Entropi, DataProtectionScope.CurrentUser);

    public byte[] Coz(byte[] sarmali)
        => ProtectedData.Unprotect(sarmali, Entropi, DataProtectionScope.CurrentUser);
}
