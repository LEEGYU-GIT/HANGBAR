using System.Security.Cryptography;
using System.Text;

namespace Hangbar.Core;

/// <summary>PKCE (RFC 7636) verifier and S256 challenge for the desktop Google sign-in (design §8-1).</summary>
public static class Pkce
{
    public static string CreateVerifier() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string ChallengeS256(string verifier) => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
