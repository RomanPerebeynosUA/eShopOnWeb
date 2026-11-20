using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

[Collection("Sequential")]
public class ManageControllerAnonymousAccess : IClassFixture<TestApplication>
{
    public ManageControllerAnonymousAccess(TestApplication factory)
    {
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public HttpClient Client { get; }

    [Theory]
    [InlineData("/manage/change-password")]
    [InlineData("/manage/two-factor-authentication")]
    [InlineData("/manage/disable2fa-warning")]
    [InlineData("/manage/enable-authenticator")]
    [InlineData("/manage/show-recovery-codes")]
    [InlineData("/manage/reset-authenticator-warning")]
    [InlineData("/manage/generate-recovery-codes-warning")]
    public async Task GetRedirectsAnonymousUserToLoginWithReturnUrl(string url)
    {
        var response = await Client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? string.Empty;
        Assert.Contains("/identity/account/login", location, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"ReturnUrl={Uri.EscapeDataString(url)}", location, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/manage/set-password")]
    [InlineData("/manage/disable2fa")]
    [InlineData("/manage/enable-authenticator")]
    [InlineData("/manage/reset-authenticator")]
    [InlineData("/manage/generate-recovery-codes")]
    public async Task PostRedirectsAnonymousUserToLogin(string url)
    {
        var token = await WebTestUsers.GetAntiForgeryTokenAsync(Client, "/identity/account/login");
        var form = new FormUrlEncodedContent(new Dictionary<string, string> { [WebPageHelpers.TokenTag] = token });

        var response = await Client.PostAsync(url, form);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/identity/account/login", response.Headers.Location?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
