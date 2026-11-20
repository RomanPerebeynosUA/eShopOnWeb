using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.Web.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Pages.Basket;

[Collection("Sequential")]
public class CheckoutTest : CheckoutJourney<TestApplication>, IClassFixture<TestApplication>
{
    public CheckoutTest(TestApplication factory) : base(factory) { }
}

/// <summary>Same journey with the app's real SQL Server provider, migrations and seed.</summary>
[Collection(SqlServerWebCollection.Name)]
[Trait("Category", "SqlServer")]
public class SqlServerCheckoutTest : CheckoutJourney<SqlServerWebApplication>
{
    private readonly SqlServerWebApplication _sqlFactory;

    public SqlServerCheckoutTest(SqlServerWebApplication factory) : base(factory) => _sqlFactory = factory;

    [Fact]
    public void AppIsWiredToSqlServerForBothContexts()
    {
        using var scope = _sqlFactory.Services.CreateScope();

        Assert.True(scope.ServiceProvider.GetRequiredService<CatalogContext>().Database.IsSqlServer());
        Assert.True(scope.ServiceProvider.GetRequiredService<Microsoft.eShopWeb.Infrastructure.Identity.AppIdentityDbContext>().Database.IsSqlServer());
    }
}

/// <summary>
/// Anonymous basket, login, checkout, then the stored order and cleared basket are checked
/// in the app's own database.
/// </summary>
public abstract class CheckoutJourney<TFactory> where TFactory : WebApplicationFactory<IBasketViewModelService>
{
    private const int CatalogItemId = 2;
    private readonly TFactory _factory;

    protected CheckoutJourney(TFactory factory)
    {
        _factory = factory;
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true
        });
    }

    public HttpClient Client { get; }

    [Fact]
    public async Task SucessfullyPay()
    {
        // A fresh buyer, so the stored order can be found without guessing.
        var email = await WebTestUsers.CreateUserAsync(_factory);
        var catalogPrice = await WithCatalogAsync(db => db.CatalogItems.Where(i => i.Id == CatalogItemId).Select(i => i.Price).SingleAsync());

        // Load Home Page
        var response = await Client.GetAsync("/");
        response.EnsureSuccessStatusCode();
        var stringResponse = await response.Content.ReadAsStringAsync();

        // Add Item to Cart (as an anonymous visitor)
        var keyValues = new List<KeyValuePair<string, string>>
        {
            new("id", CatalogItemId.ToString()),
            new("name", "shirt"),
            new("price", "19.49"),
            new(WebPageHelpers.TokenTag, WebPageHelpers.GetRequestVerificationToken(stringResponse))
        };
        var postResponse = await Client.PostAsync("/basket/index", new FormUrlEncodedContent(keyValues));
        postResponse.EnsureSuccessStatusCode();
        Assert.Contains(".NET Black &amp; White Mug", await postResponse.Content.ReadAsStringAsync());

        // Log in: the anonymous basket moves to the buyer
        var loginResponse = await Client.GetAsync("/Identity/Account/Login");
        var loginKeyValues = new List<KeyValuePair<string, string>>
        {
            new("email", email),
            new("password", WebTestUsers.DefaultPassword),
            new(WebPageHelpers.TokenTag, WebPageHelpers.GetRequestVerificationToken(await loginResponse.Content.ReadAsStringAsync()))
        };
        var loginPostResponse = await Client.PostAsync("/Identity/Account/Login?ReturnUrl=%2FBasket%2FCheckout", new FormUrlEncodedContent(loginKeyValues));
        var loginStringResponse = await loginPostResponse.Content.ReadAsStringAsync();
        Assert.Equal(1, await WithCatalogAsync(db => db.Baskets.Where(b => b.BuyerId == email).SelectMany(b => b.Items).CountAsync()));

        // Basket checkout (Pay now)
        var checkOutKeyValues = new List<KeyValuePair<string, string>>
        {
            new("Items[0].Id", CatalogItemId.ToString()),
            new("Items[0].Quantity", "1"),
            new(WebPageHelpers.TokenTag, WebPageHelpers.GetRequestVerificationToken(loginStringResponse))
        };
        var checkOutResponse = await Client.PostAsync("/basket/checkout", new FormUrlEncodedContent(checkOutKeyValues));
        var stringCheckOutResponse = await checkOutResponse.Content.ReadAsStringAsync();

        Assert.Contains("/Basket/Success", checkOutResponse.RequestMessage!.RequestUri!.ToString());
        Assert.Contains("Thanks for your Order!", stringCheckOutResponse);

        // The order was stored for this buyer, with the catalog price (not the posted one).
        var orders = await WithCatalogAsync(db => db.Orders.Include(o => o.OrderItems).Where(o => o.BuyerId == email).ToListAsync());
        var order = Assert.Single(orders);
        var item = Assert.Single(order.OrderItems);
        Assert.Equal(CatalogItemId, item.ItemOrdered.CatalogItemId);
        Assert.Equal(1, item.Units);
        Assert.Equal(catalogPrice, item.UnitPrice);
        Assert.Equal(catalogPrice, order.Total());

        // The basket is cleared after checkout.
        Assert.False(await WithCatalogAsync(db => db.Baskets.AnyAsync(b => b.BuyerId == email)), "Checkout should delete the buyer's basket.");
    }

    private async Task<T> WithCatalogAsync<T>(Func<CatalogContext, Task<T>> query)
    {
        using var scope = _factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<CatalogContext>());
    }
}
