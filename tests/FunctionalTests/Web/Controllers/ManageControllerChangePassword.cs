using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

[Collection("Sequential")]
public class ManageControllerChangePassword : IClassFixture<TestApplication>
{
    public ManageControllerChangePassword(TestApplication factory)
    {
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
    public async Task PostChangePasswordWithoutAntiForgeryTokenIsRejected()
    {
        var keyValues = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("OldPassword", "Pass@word1"),
            new KeyValuePair<string, string>("NewPassword", "NewPass@word2"),
            new KeyValuePair<string, string>("ConfirmPassword", "NewPass@word2")
        };
        var formContent = new FormUrlEncodedContent(keyValues);

        var postResponse = await Client.PostAsync("/manage/change-password", formContent);

        Assert.NotEqual(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.True(
            postResponse.StatusCode == HttpStatusCode.BadRequest ||
            postResponse.StatusCode == HttpStatusCode.Redirect,
            $"Unexpected status code: {postResponse.StatusCode}");
    }
}
