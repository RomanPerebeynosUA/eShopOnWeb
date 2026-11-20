using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

[Collection("Sequential")]
public class OrderControllerDetail : IClassFixture<TestApplication>
{
    private readonly TestApplication _factory;

    public OrderControllerDetail(TestApplication factory)
    {
        _factory = factory;
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public HttpClient Client { get; }

    [Fact]
    public async Task RedirectsAnonymousUserToLogin()
    {
        var response = await Client.GetAsync("/order/detail/1");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.OriginalString ?? string.Empty);
    }

    [Fact]
    public async Task ShowsOwnOrderWithItemsAddressAndTotal()
    {
        var email = await WebTestUsers.CreateAndLoginAsync(_factory, Client);
        var order = await WebTestUsers.CreateOrderAsync(_factory, email, "Detail Test Mug", 7.25m, 3);

        var response = await Client.GetAsync($"/order/detail/{order.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains($">{order.Id}</section>", html);
        Assert.Contains("Detail Test Mug", html);
        Assert.Contains("123 Test Street", html);
        Assert.Contains("Testville", html);
        Assert.Contains($"$ {(7.25m * 3).ToString("N2")}", html);
    }

    [Fact]
    public async Task ReturnsBadRequestForUnknownOrder()
    {
        await WebTestUsers.CreateAndLoginAsync(_factory, Client);

        var response = await Client.GetAsync("/order/detail/999999");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("No such order found for this user.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DoesNotShowAnotherUsersOrder()
    {
        var owner = await WebTestUsers.CreateUserAsync(_factory);
        var order = await WebTestUsers.CreateOrderAsync(_factory, owner, "Someone Elses Sheet", 9.99m, 1);
        await WebTestUsers.CreateAndLoginAsync(_factory, Client);

        var response = await Client.GetAsync($"/order/detail/{order.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("Someone Elses Sheet", await response.Content.ReadAsStringAsync());
    }
}
