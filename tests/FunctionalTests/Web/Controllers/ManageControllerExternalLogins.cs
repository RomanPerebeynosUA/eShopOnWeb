using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

[Collection("Sequential")]
public class ManageControllerExternalLogins : IClassFixture<TestApplication>
{
    public ManageControllerExternalLogins(TestApplication factory)
    {
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public HttpClient Client { get; }

    [Fact]
    public async Task RedirectsToLoginWhenAnonymousOnGet()
    {
        var response = await Client.GetAsync("/manage/external-logins");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/identity/account/login", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExternalLoginsPageRequiresAuthenticationAndDoesNotRenderLoginsForAnonymousUser()
    {
        var response = await Client.GetAsync("/manage/external-logins");
        var body = await response.Content.ReadAsStringAsync();

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("Manage your external logins", body);
    }
}
