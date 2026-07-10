using System.Security.Cryptography;
using System.Text;

namespace HeadlessCoder.Auth;

/// <summary>Small security helpers shared by the access-control middleware.</summary>
public static class SecurityUtil
{
    /// <summary>
    /// Constant-time string comparison over the UTF-8 bytes, used to compare the
    /// access key / auth cookie without leaking length-independent timing.
    /// </summary>
    public static bool FixedEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return ba.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
