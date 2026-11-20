using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.eShopWeb.ApplicationCore.Constants;
using Microsoft.eShopWeb.PublicApi.AuthEndpoints;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.PublicApi.AuthEndpoints;

[Collection("Sequential")]
public class AuthenticateEndpointTests : IClassFixture<TestApiApplication>
{
    public AuthenticateEndpointTests(TestApiApplication factory)
    {
        Client = factory.CreateClient();
    }

    public HttpClient Client { get; }

    [Theory]
    [InlineData("demouser@microsoft.com", AuthorizationConstants.DEFAULT_PASSWORD, true)]
    [InlineData("demouser@microsoft.com", "badpassword", false)]
    [InlineData("baduser@microsoft.com", "badpassword", false)]
    public async Task ReturnsExpectedResultGivenCredentials(string testUsername, string testPassword, bool expectedResult)
    {
        var request = new AuthenticateRequest()
        {
            Username = testUsername,
            Password = testPassword
        };
        var jsonContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var response = await Client.PostAsync("api/authenticate", jsonContent);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var model = (await response.Content.ReadAsStringAsync()).FromJson<AuthenticateResponse>();
        Assert.NotNull(model);
        Assert.Equal(expectedResult, model!.Result);
        Assert.Equal(testUsername, model.Username);
        Assert.Equal(expectedResult, !string.IsNullOrEmpty(model.Token));
    }
}
