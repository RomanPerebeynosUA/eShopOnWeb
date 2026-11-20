using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

[Collection("Sequential")]
public class ManageControllerExternalLogins : IClassFixture<TestApplication>
{
    private readonly TestApplication _factory;

    public ManageControllerExternalLogins(TestApplication factory)
    {
        _factory = factory;
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public HttpClient Client { get; }

    [Fact]
    public async Task RedirectsToLoginWhenAnonymousOnGet()
    {
        var response = await Client.GetAsync("/manage/external-logins");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/identity/account/login", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ReturnUrl=%2Fmanage%2Fexternal-logins", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ShowsPageWithoutRegisteredLoginsForUserWithNoExternalLogins()
    {
        await WebTestUsers.CreateAndLoginAsync(_factory, Client);

        var response = await Client.GetAsync("/manage/external-logins");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Manage your external logins", html);
        Assert.DoesNotContain("Registered Logins", html);
    }

    [Fact]
    public async Task ListsRegisteredExternalLoginWithRemoveButton()
    {
        var email = await WebTestUsers.CreateAndLoginAsync(_factory, Client);
        await WebTestUsers.WithUserManagerAsync(_factory,
            (um, user) => um.AddLoginAsync(user, new UserLoginInfo("Google", "google-key-ext", "Google")), email);

        var response = await Client.GetAsync("/manage/external-logins");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Registered Logins", html);
        Assert.Contains("<td>Google</td>", html);
        Assert.Contains("value=\"google-key-ext\"", html);
        Assert.Contains("Remove this Google login from your account", html);
    }
}
