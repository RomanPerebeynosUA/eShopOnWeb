using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web;

/// <summary>
/// Pins the TOTP helper to the RFC 6238 SHA-1 test vectors, so 2FA tests fail on a helper bug
/// rather than intermittently on timing.
/// </summary>
public class WebTestUsersTotp
{
    // Base32 of the RFC 6238 ASCII seed "12345678901234567890".
    private const string RfcKey = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    public void MatchesRfc6238Vectors(long unixSeconds, string expected)
    {
        var code = WebTestUsers.ComputeTotp(RfcKey, DateTimeOffset.FromUnixTimeSeconds(unixSeconds));

        Assert.Equal(expected, code);
    }

    [Fact]
    public void IsStableWithinOneTimeStep()
    {
        var stepStart = DateTimeOffset.FromUnixTimeSeconds(1_700_000_010);

        Assert.Equal(WebTestUsers.ComputeTotp(RfcKey, stepStart), WebTestUsers.ComputeTotp(RfcKey, stepStart.AddSeconds(19)));
    }
}
