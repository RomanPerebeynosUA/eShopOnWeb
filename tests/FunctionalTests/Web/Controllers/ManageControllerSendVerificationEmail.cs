using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

[Collection("Sequential")]
public class ManageControllerSendVerificationEmail : IClassFixture<TestApplication>
{
    public ManageControllerSendVerificationEmail(TestApplication factory)
    {
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public HttpClient Client { get; }

    [Fact]
    public async Task PostWithoutAuthenticationRedirectsToLogin()
    {
        var getResponse = await Client.GetAsync("/manage/my-account");
        var token = WebPageHelpers.GetRequestVerificationToken(await getResponse.Content.ReadAsStringAsync());

        var keyValues = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>(WebPageHelpers.TokenTag, token)
        };
        var response = await Client.PostAsync("/manage/send-verification-email", new FormUrlEncodedContent(keyValues));

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("login", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
