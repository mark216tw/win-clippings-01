using System.Security.Cryptography;

namespace Clippings.Core.Helpers;

public static class HashUtil
{
    public static string Sha256Hex(ReadOnlySpan<byte> data)
        => Convert.ToHexStringLower(SHA256.HashData(data));

    public static string Sha256Hex(string text)
        => Sha256Hex(System.Text.Encoding.UTF8.GetBytes(text));
}
