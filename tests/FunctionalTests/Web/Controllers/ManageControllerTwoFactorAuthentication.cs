using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

[Collection("Sequential")]
public class ManageControllerTwoFactorAuthentication : IClassFixture<TestApplication>
{
    private readonly TestApplication _factory;

    public ManageControllerTwoFactorAuthentication(TestApplication factory)
    {
        _factory = factory;
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public HttpClient Client { get; }

    [Fact]
    public async Task ShowsAddAuthenticatorForUserWithout2fa()
    {
        await WebTestUsers.CreateAndLoginAsync(_factory, Client);

        var response = await Client.GetAsync("/manage/two-factor-authentication");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<h4>Two-factor authentication</h4>", html);
        Assert.Contains("Add authenticator app", html);
        Assert.DoesNotContain("Disable 2FA", html);
        Assert.DoesNotContain("Reset authenticator key", html);
    }

    [Fact]
    public async Task ShowsDisableAndResetOptionsForUserWith2fa()
    {
        var email = await WebTestUsers.CreateAndLoginAsync(_factory, Client);
        await EnableTwoFactorAsync(email);

        var response = await Client.GetAsync("/manage/two-factor-authentication");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Disable 2FA", html);
        Assert.Contains("Reset recovery codes", html);
        Assert.Contains("Configure authenticator app", html);
        Assert.Contains("Reset authenticator key", html);
        Assert.DoesNotContain("recovery codes left", html);
    }

    [Fact]
    public async Task Disable2faWarningShowsConfirmationForUserWith2fa()
    {
        var email = await WebTestUsers.CreateAndLoginAsync(_factory, Client);
        await EnableTwoFactorAsync(email);

        var response = await Client.GetAsync("/manage/disable2fa-warning");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Disable two-factor authentication (2FA)", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Disable2faWarningFailsForUserWithout2fa()
    {
        var email = await WebTestUsers.CreateAndLoginAsync(_factory, Client);

        var response = await Client.GetAsync("/manage/disable2fa-warning");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Unexpected error occured disabling 2FA for user with ID", body);
        Assert.DoesNotContain("action=\"/manage/disable2fa\"", body);
        Assert.False(await WebTestUsers.WithUserManagerAsync(_factory, (um, user) => um.GetTwoFactorEnabledAsync(user), email));
    }

    [Fact]
    public async Task Disable2faTurnsOffTwoFactorAndRedirects()
    {
        var email = await WebTestUsers.CreateAndLoginAsync(_factory, Client);
        await EnableTwoFactorAsync(email);

        var response = await WebTestUsers.PostFormAsync(Client, "/manage/disable2fa", tokenPageUrl: "/manage/disable2fa-warning");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/manage/two-factor-authentication", response.Headers.Location?.OriginalString);
        var user = await WebTestUsers.GetUserAsync(_factory, email);
        Assert.False(user.TwoFactorEnabled);
    }

    [Fact]
    public async Task Disable2faWithoutAntiForgeryTokenIsRejectedAndKeeps2fa()
    {
        var email = await WebTestUsers.CreateAndLoginAsync(_factory, Client);
        await EnableTwoFactorAsync(email);

        var response = await Client.PostAsync("/manage/disable2fa", new FormUrlEncodedContent(new Dictionary<string, string>()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var user = await WebTestUsers.GetUserAsync(_factory, email);
        Assert.True(user.TwoFactorEnabled);
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
