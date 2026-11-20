using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

[Collection("Sequential")]
public class ManageControllerLinkLogin : IClassFixture<TestApplication>
{
    public ManageControllerLinkLogin(TestApplication factory)
    {
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public HttpClient Client { get; }

    [Fact]
    public async Task RedirectsAnonymousUserToLoginOnPost()
    {
        var getResponse = await Client.GetAsync("/identity/account/login");
        getResponse.EnsureSuccessStatusCode();
        var stringResponse = await getResponse.Content.ReadAsStringAsync();

        var keyValues = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("provider", "Google"),
            new KeyValuePair<string, string>(WebPageHelpers.TokenTag, WebPageHelpers.GetRequestVerificationToken(stringResponse))
        };
        var formContent = new FormUrlEncodedContent(keyValues);

        var response = await Client.PostAsync("/manage/link-login", formContent);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("login", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RejectsPostWithoutAntiForgeryToken()
    {
        var keyValues = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("provider", "Google")
        };
        var formContent = new FormUrlEncodedContent(keyValues);

        var response = await Client.PostAsync("/manage/link-login", formContent);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
