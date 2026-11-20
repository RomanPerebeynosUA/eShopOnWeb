using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

[Collection("Sequential")]
public class ManageControllerChangePassword : IClassFixture<TestApplication>
{
    private readonly TestApplication _factory;

    public ManageControllerChangePassword(TestApplication factory)
    {
        _factory = factory;
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public HttpClient Client { get; }

    [Fact]
    public async Task PostChangePasswordWithoutAuthenticationRedirectsToLogin()
    {
        var getResponse = await Client.GetAsync("/identity/account/login");
        getResponse.EnsureSuccessStatusCode();
        var stringResponse = await getResponse.Content.ReadAsStringAsync();

        var keyValues = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("OldPassword", "Pass@word1"),
            new KeyValuePair<string, string>("NewPassword", "NewPass@word2"),
            new KeyValuePair<string, string>("ConfirmPassword", "NewPass@word2"),
            new KeyValuePair<string, string>(WebPageHelpers.TokenTag, WebPageHelpers.GetRequestVerificationToken(stringResponse))
        };
        var formContent = new FormUrlEncodedContent(keyValues);

        var postResponse = await Client.PostAsync("/manage/change-password", formContent);

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.NotNull(postResponse.Headers.Location);
        Assert.Contains("login", postResponse.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostChangePasswordWithoutAntiForgeryTokenIsRejectedAndPasswordIsUnchanged()
    {
        var email = await WebTestUsers.CreateAndLoginAsync(_factory, Client);
        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["OldPassword"] = WebTestUsers.DefaultPassword,
            ["NewPassword"] = "NewPass@word2",
            ["ConfirmPassword"] = "NewPass@word2"
        });

        var postResponse = await Client.PostAsync("/manage/change-password", formContent);

        Assert.Equal(HttpStatusCode.BadRequest, postResponse.StatusCode);
        Assert.True(await PasswordMatchesAsync(email, WebTestUsers.DefaultPassword));
        Assert.False(await PasswordMatchesAsync(email, "NewPass@word2"));
    }

    [Fact]
    public async Task GetShowsChangePasswordFormForUserWithPassword()
    {
        await WebTestUsers.CreateAndLoginAsync(_factory, Client);

        var response = await Client.GetAsync("/manage/change-password");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<h4>Change password</h4>", html);
        Assert.Contains("name=\"OldPassword\"", html);
        Assert.Contains("name=\"NewPassword\"", html);
        Assert.Contains("name=\"ConfirmPassword\"", html);
    }

    [Fact]
    public async Task GetRedirectsToSetPasswordForUserWithoutPassword()
    {
        var email = await WebTestUsers.CreateAndLoginAsync(_factory, Client);
        await WebTestUsers.WithUserManagerAsync(_factory, (um, user) => um.RemovePasswordAsync(user), email);

        var response = await Client.GetAsync("/manage/change-password");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/manage/set-password", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task PostWithValidCurrentPasswordChangesPassword()
    {
        var email = await WebTestUsers.CreateAndLoginAsync(_factory, Client);

        var response = await WebTestUsers.PostFormAsync(Client, "/manage/change-password", new Dictionary<string, string>
        {
            ["OldPassword"] = WebTestUsers.DefaultPassword,
            ["NewPassword"] = "NewPass@word2",
            ["ConfirmPassword"] = "NewPass@word2"
        }, "/manage/change-password");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/manage/change-password", response.Headers.Location?.OriginalString);
        Assert.True(await PasswordMatchesAsync(email, "NewPass@word2"));
        Assert.False(await PasswordMatchesAsync(email, WebTestUsers.DefaultPassword));

        var page = await (await Client.GetAsync("/manage/change-password")).Content.ReadAsStringAsync();
        Assert.Contains("Your password has been changed.", page);
    }

    [Fact]
    public async Task PostWithWrongCurrentPasswordShowsErrorAndKeepsPassword()
    {
        var email = await WebTestUsers.CreateAndLoginAsync(_factory, Client);

        var response = await WebTestUsers.PostFormAsync(Client, "/manage/change-password", new Dictionary<string, string>
        {
            ["OldPassword"] = "Wrong@password1",
            ["NewPassword"] = "NewPass@word2",
            ["ConfirmPassword"] = "NewPass@word2"
        }, "/manage/change-password");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Incorrect password.", html);
        Assert.True(await PasswordMatchesAsync(email, WebTestUsers.DefaultPassword));
        Assert.False(await PasswordMatchesAsync(email, "NewPass@word2"));
    }

    private Task<bool> PasswordMatchesAsync(string email, string password) =>
        WebTestUsers.WithUserManagerAsync(_factory, (um, user) => um.CheckPasswordAsync(user, password), email);
}
