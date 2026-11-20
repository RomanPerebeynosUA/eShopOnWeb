using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

/// <summary>
/// Account and order flows through the full Web pipeline with the real SQL Server
/// identity and catalog databases.
/// </summary>
[Collection(SqlServerWebCollection.Name)]
[Trait("Category", "SqlServer")]
public class SqlServerAccountTests
{
    private readonly SqlServerWebApplication _factory;

    public SqlServerAccountTests(SqlServerWebApplication factory)
    {
        _factory = factory;
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    public HttpClient Client { get; }

    [Fact]
    public async Task ChangedPasswordIsStoredInTheIdentityDatabase()
    {
        var email = await WebTestUsers.CreateAndLoginAsync(_factory, Client);

        var response = await WebTestUsers.PostFormAsync(Client, "/manage/change-password", new Dictionary<string, string>
        {
            ["OldPassword"] = WebTestUsers.DefaultPassword,
            ["NewPassword"] = "NewPass@word2",
            ["ConfirmPassword"] = "NewPass@word2"
        }, "/manage/change-password");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(await WebTestUsers.WithUserManagerAsync(_factory, (um, u) => um.CheckPasswordAsync(u, "NewPass@word2"), email),
            "The new password should verify against the stored hash.");
        Assert.False(await WebTestUsers.WithUserManagerAsync(_factory, (um, u) => um.CheckPasswordAsync(u, WebTestUsers.DefaultPassword), email),
            "The old password should no longer verify.");
    }

    [Fact]
    public async Task OrderDetailsAreShownToTheBuyerOnly()
    {
        var owner = await WebTestUsers.CreateUserAsync(_factory);
        var order = await WebTestUsers.CreateOrderAsync(_factory, owner, "Sql Detail Mug", 7.25m, 3);

        var ownerClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await WebTestUsers.LoginAsync(ownerClient, owner);
        var ownResponse = await ownerClient.GetAsync($"/order/detail/{order.Id}");
        Assert.Equal(HttpStatusCode.OK, ownResponse.StatusCode);
        var html = await ownResponse.Content.ReadAsStringAsync();
        Assert.Contains("Sql Detail Mug", html);
        Assert.Contains("123 Test Street", html);

        await WebTestUsers.CreateAndLoginAsync(_factory, Client);
        var otherResponse = await Client.GetAsync($"/order/detail/{order.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, otherResponse.StatusCode);
        Assert.DoesNotContain("Sql Detail Mug", await otherResponse.Content.ReadAsStringAsync());
    }
}
