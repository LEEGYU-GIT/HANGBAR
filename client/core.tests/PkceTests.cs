using Hangbar.Core;

namespace Hangbar.Core.Tests;

public class PkceTests
{
    [Fact]
    public void Challenge_matches_rfc7636_appendix_b() =>
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
            Pkce.ChallengeS256("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));

    [Fact]
    public void Verifier_is_43_chars_base64url()
    {
        var v = Pkce.CreateVerifier();
        Assert.Equal(43, v.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", v);
    }

    [Fact]
    public void Verifiers_are_not_repeated() =>
        Assert.NotEqual(Pkce.CreateVerifier(), Pkce.CreateVerifier());
}
