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
    public async Task RedirectsAnonymousUserToLoginOnGet()
    {
        var response = await Client.GetAsync("/manage/change-password");

        Assert.True(
            response.StatusCode == HttpStatusCode.Redirect ||
            response.StatusCode == HttpStatusCode.Found,
            $"Expected redirect for anonymous user but got {response.StatusCode}.");
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("login", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReturnsNotFoundForUnknownManageRoute()
    {
        var response = await Client.GetAsync("/manage/change-password-does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
