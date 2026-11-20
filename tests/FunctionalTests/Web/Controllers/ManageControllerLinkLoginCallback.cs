using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

[Collection("Sequential")]
public class ManageControllerLinkLoginCallback : IClassFixture<TestApplication>
{
    private readonly TestApplication _factory;

    public ManageControllerLinkLoginCallback(TestApplication factory)
    {
        _factory = factory;
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public HttpClient Client { get; }

    [Fact]
    public async Task RedirectsAnonymousUserToLogin()
    {
        var response = await Client.GetAsync("/manage/link-login-callback");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/identity/account/login", response.Headers.Location?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FailsWithoutExternalLoginInfoAndAddsNoLogin()
    {
        // No external provider round-trip happened, so there is no external login cookie:
        // the callback must refuse to link anything.
        var email = await WebTestUsers.CreateAndLoginAsync(_factory, Client);

        var response = await Client.GetAsync("/manage/link-login-callback");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Unexpected error occurred loading external login info", await response.Content.ReadAsStringAsync());
        var logins = await WebTestUsers.WithUserManagerAsync(_factory, (um, user) => um.GetLoginsAsync(user), email);
        Assert.Empty(logins);
    }
}
