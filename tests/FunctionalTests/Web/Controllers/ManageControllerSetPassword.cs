using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

[Collection("Sequential")]
public class ManageControllerSetPassword : IClassFixture<TestApplication>
{
    public ManageControllerSetPassword(TestApplication factory)
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
        var response = await Client.GetAsync("/manage/set-password");

        Assert.Contains("/identity/account/login", response.Headers.Location?.ToString() ?? string.Empty);
    }
}
