using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

[Collection("Sequential")]
public class OrderIndexOnGet : IClassFixture<TestApplication>
{
    private readonly TestApplication _factory;

    public OrderIndexOnGet(TestApplication factory)
    {
        _factory = factory;
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public HttpClient Client { get; }

    [Fact]
    public async Task ReturnsRedirectGivenAnonymousUser()
    {
        var response = await Client.GetAsync("/order/my-orders");
        var redirectLocation = response!.Headers.Location!.OriginalString;

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", redirectLocation);
    }

    [Fact]
    public async Task ListsOnlyTheSignedInUsersOrders()
    {
        var otherUser = await WebTestUsers.CreateUserAsync(_factory);
        var otherOrder = await WebTestUsers.CreateOrderAsync(_factory, otherUser, "Other Users Mug", 5m, 1);
        var email = await WebTestUsers.CreateAndLoginAsync(_factory, Client);
        var order = await WebTestUsers.CreateOrderAsync(_factory, email, "My Orders Mug", 4.5m, 2);

        var response = await Client.GetAsync("/order/my-orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("My Order History", html);
        Assert.Contains($"href=\"/order/detail/{order.Id}\"", html);
        Assert.Contains($"$ {4.5m * 2}", html);
        Assert.DoesNotContain($"href=\"/order/detail/{otherOrder.Id}\"", html);
    }
}
