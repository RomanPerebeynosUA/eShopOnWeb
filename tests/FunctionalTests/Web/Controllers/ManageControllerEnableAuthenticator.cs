using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

[Collection("Sequential")]
public class ManageControllerEnableAuthenticator : IClassFixture<TestApplication>
{
    private readonly TestApplication _factory;

    public ManageControllerEnableAuthenticator(TestApplication factory)
    {
        _factory = factory;
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public HttpClient Client { get; }

    [Fact]
    public async Task GetShowsSharedKeyStoredForUser()
    {
        var email = await WebTestUsers.CreateAndLoginAsync(_factory, Client);

        var response = await Client.GetAsync("/manage/enable-authenticator");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        var storedKey = await WebTestUsers.WithUserManagerAsync(_factory, (um, user) => um.GetAuthenticatorKeyAsync(user), email);
        Assert.False(string.IsNullOrEmpty(storedKey));
        var shownKey = Regex.Match(html, "<kbd>([a-z0-9 ]+)</kbd>").Groups[1].Value;
        Assert.Equal(storedKey, shownKey.Replace(" ", string.Empty).ToUpperInvariant());
        Assert.Contains("otpauth://totp/eShopOnWeb:", WebUtility.HtmlDecode(html));
    }

    [Fact]
    public async Task PostWithValidCodeEnables2faAndShowsRecoveryCodes()
    {
        var email = await WebTestUsers.CreateAndLoginAsync(_factory, Client);
        var token = await WebTestUsers.GetAntiForgeryTokenAsync(Client, "/manage/enable-authenticator");
        var key = await WebTestUsers.WithUserManagerAsync(_factory, (um, user) => um.GetAuthenticatorKeyAsync(user), email);

        var response = await Client.PostAsync("/manage/enable-authenticator", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Code"] = WebTestUsers.ComputeTotp(key!),
            [WebPageHelpers.TokenTag] = token
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/manage/show-recovery-codes", response.Headers.Location?.OriginalString);
        var user = await WebTestUsers.GetUserAsync(_factory, email);
        Assert.True(user.TwoFactorEnabled);
        Assert.Equal(10, await WebTestUsers.WithUserManagerAsync(_factory, (um, u) => um.CountRecoveryCodesAsync(u), email));

        var codesPage = await Client.GetAsync("/manage/show-recovery-codes");
        Assert.Equal(HttpStatusCode.OK, codesPage.StatusCode);
        var html = await codesPage.Content.ReadAsStringAsync();
        Assert.Contains("Recovery codes", html);
        Assert.Equal(10, Regex.Matches(html, "<code>[^<]+</code>").Count);
    }

    [Fact]
    public async Task PostWithInvalidCodeShowsErrorAndLeaves2faDisabled()
    {
        var email = await WebTestUsers.CreateAndLoginAsync(_factory, Client);
        var token = await WebTestUsers.GetAntiForgeryTokenAsync(Client, "/manage/enable-authenticator");
        var key = await WebTestUsers.WithUserManagerAsync(_factory, (um, user) => um.GetAuthenticatorKeyAsync(user), email);
        var validCode = int.Parse(WebTestUsers.ComputeTotp(key!));
        var invalidCode = ((validCode + 500_000) % 1_000_000).ToString("D6");

        var response = await Client.PostAsync("/manage/enable-authenticator", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Code"] = invalidCode,
            [WebPageHelpers.TokenTag] = token
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Verification code is invalid.", await response.Content.ReadAsStringAsync());
        var user = await WebTestUsers.GetUserAsync(_factory, email);
        Assert.False(user.TwoFactorEnabled);
        Assert.Equal(0, await WebTestUsers.WithUserManagerAsync(_factory, (um, u) => um.CountRecoveryCodesAsync(u), email));
    }

    [Fact]
    public async Task ShowRecoveryCodesWithoutFreshCodesRedirectsToTwoFactorPage()
    {
        await WebTestUsers.CreateAndLoginAsync(_factory, Client);

        var response = await Client.GetAsync("/manage/show-recovery-codes");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/manage/two-factor-authentication", response.Headers.Location?.OriginalString);
    }
}
