using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

[Collection("Sequential")]
public class ManageControllerSetPassword : IClassFixture<TestApplication>
{
    private readonly TestApplication _factory;

    public ManageControllerSetPassword(TestApplication factory)
    {
        _factory = factory;
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public HttpClient Client { get; }

    [Fact]
    public async Task RedirectsAnonymousUserToLoginOnGet()
    {
        var response = await Client.GetAsync("/manage/set-password");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/identity/account/login", response.Headers.Location?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetRedirectsToChangePasswordForUserWhoAlreadyHasPassword()
    {
        await WebTestUsers.CreateAndLoginAsync(_factory, Client);

        var response = await Client.GetAsync("/manage/set-password");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/manage/change-password", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task GetShowsSetPasswordFormForUserWithoutPassword()
    {
        var email = await WebTestUsers.CreateAndLoginAsync(_factory, Client);
        await WebTestUsers.WithUserManagerAsync(_factory, (um, user) => um.RemovePasswordAsync(user), email);

        var response = await Client.GetAsync("/manage/set-password");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<h4>Set your password</h4>", html);
        Assert.Contains("You do not have a local username/password for this site.", html);
        Assert.Contains("name=\"NewPassword\"", html);
    }

    [Fact]
    public async Task PostSetsPasswordForUserWithoutPassword()
    {
        var email = await WebTestUsers.CreateAndLoginAsync(_factory, Client);
        await WebTestUsers.WithUserManagerAsync(_factory, (um, user) => um.RemovePasswordAsync(user), email);

        var response = await WebTestUsers.PostFormAsync(Client, "/manage/set-password", new Dictionary<string, string>
        {
            ["NewPassword"] = "Brand@NewPass1",
            ["ConfirmPassword"] = "Brand@NewPass1"
        }, "/manage/set-password");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/manage/set-password", response.Headers.Location?.OriginalString);
        Assert.True(await WebTestUsers.WithUserManagerAsync(_factory, (um, user) => um.CheckPasswordAsync(user, "Brand@NewPass1"), email));
    }

    [Fact]
    public async Task PostWithMismatchedConfirmationDoesNotSetPassword()
    {
        var email = await WebTestUsers.CreateAndLoginAsync(_factory, Client);
        await WebTestUsers.WithUserManagerAsync(_factory, (um, user) => um.RemovePasswordAsync(user), email);

        var response = await WebTestUsers.PostFormAsync(Client, "/manage/set-password", new Dictionary<string, string>
        {
            ["NewPassword"] = "Brand@NewPass1",
            ["ConfirmPassword"] = "Different@Pass1"
        }, "/manage/set-password");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("The new password and confirmation password do not match.", await response.Content.ReadAsStringAsync());
        Assert.False(await WebTestUsers.WithUserManagerAsync(_factory, (um, user) => um.HasPasswordAsync(user), email));
    }
}
