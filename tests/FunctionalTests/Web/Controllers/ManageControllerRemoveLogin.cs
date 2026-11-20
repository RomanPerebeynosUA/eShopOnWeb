using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

[Collection("Sequential")]
public class ManageControllerRemoveLogin : IClassFixture<TestApplication>
{
    private readonly TestApplication _factory;

    public ManageControllerRemoveLogin(TestApplication factory)
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
        var token = await WebTestUsers.GetAntiForgeryTokenAsync(Client, "/identity/account/login");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["LoginProvider"] = "Google",
            ["ProviderKey"] = "any",
            [WebPageHelpers.TokenTag] = token
        });

        var response = await Client.PostAsync("/manage/remove-login", form);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/identity/account/login", response.Headers.Location?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RemovesTheExternalLoginAndRedirectsToExternalLogins()
    {
        var email = await WebTestUsers.CreateAndLoginAsync(_factory, Client);
        await WebTestUsers.WithUserManagerAsync(_factory,
            (um, user) => um.AddLoginAsync(user, new UserLoginInfo("Google", "google-key-remove", "Google")), email);

        var response = await WebTestUsers.PostFormAsync(Client, "/manage/remove-login", new Dictionary<string, string>
        {
            ["LoginProvider"] = "Google",
            ["ProviderKey"] = "google-key-remove"
        }, "/manage/external-logins");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/manage/external-logins", response.Headers.Location?.OriginalString);
        var logins = await WebTestUsers.WithUserManagerAsync(_factory, (um, user) => um.GetLoginsAsync(user), email);
        Assert.Empty(logins);

        var page = await (await Client.GetAsync("/manage/external-logins")).Content.ReadAsStringAsync();
        Assert.Contains("The external login was removed.", page);
        Assert.DoesNotContain("google-key-remove", page);
    }

    [Fact]
    public async Task RejectsPostWithoutAntiForgeryTokenAndKeepsLogin()
    {
        var email = await WebTestUsers.CreateAndLoginAsync(_factory, Client);
        await WebTestUsers.WithUserManagerAsync(_factory,
            (um, user) => um.AddLoginAsync(user, new UserLoginInfo("Google", "google-key-keep", "Google")), email);
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["LoginProvider"] = "Google",
            ["ProviderKey"] = "google-key-keep"
        });

        var response = await Client.PostAsync("/manage/remove-login", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var logins = await WebTestUsers.WithUserManagerAsync(_factory, (um, user) => um.GetLoginsAsync(user), email);
        Assert.Single(logins, l => l.LoginProvider == "Google" && l.ProviderKey == "google-key-keep");
    }
}
