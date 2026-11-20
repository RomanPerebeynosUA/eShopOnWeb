using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

[Collection("Sequential")]
public class ManageControllerResetAuthenticator : IClassFixture<TestApplication>
{
    private readonly TestApplication _factory;

    public ManageControllerResetAuthenticator(TestApplication factory)
    {
        _factory = factory;
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public HttpClient Client { get; }

    [Fact]
    public async Task WarningPageExplainsReset()
    {
        await WebTestUsers.CreateAndLoginAsync(_factory, Client);

        var response = await Client.GetAsync("/manage/reset-authenticator-warning");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Reset authenticator key", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PostResetsKeyDisables2faAndRedirectsToEnableAuthenticator()
    {
        var email = await WebTestUsers.CreateAndLoginAsync(_factory, Client);
        await EnableTwoFactorAsync(email);
        var originalKey = await WebTestUsers.WithUserManagerAsync(_factory, (um, user) => um.GetAuthenticatorKeyAsync(user), email);

        var response = await WebTestUsers.PostFormAsync(Client, "/manage/reset-authenticator", tokenPageUrl: "/manage/reset-authenticator-warning");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/manage/enable-authenticator", response.Headers.Location?.OriginalString);
        var user = await WebTestUsers.GetUserAsync(_factory, email);
        Assert.False(user.TwoFactorEnabled);
        var newKey = await WebTestUsers.WithUserManagerAsync(_factory, (um, u) => um.GetAuthenticatorKeyAsync(u), email);
        Assert.False(string.IsNullOrEmpty(newKey));
        Assert.NotEqual(originalKey, newKey);
    }

    private Task EnableTwoFactorAsync(string email) =>
        WebTestUsers.WithUserManagerAsync(_factory, async (um, user) =>
        {
            await um.ResetAuthenticatorKeyAsync(user);
            await um.SetTwoFactorEnabledAsync(user, true);
            await um.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
            return true;
        }, email);
}
