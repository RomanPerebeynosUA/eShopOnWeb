using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.Infrastructure.Data;
using Xunit;

namespace Microsoft.eShopWeb.IntegrationTests.SqlServer;

/// <summary>
/// Catalog persistence against real SQL Server: migrations, constraints, query
/// translation and transactions that the in-memory provider does not enforce.
/// </summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "SqlServer")]
public class CatalogContextSqlServerTests
{
    private readonly SqlServerFixture _fixture;

    public CatalogContextSqlServerTests(SqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task MigrationsAreFullyAppliedAndSeedDataIsPresent()
    {
        await using var db = _fixture.CreateCatalogContext();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(5, await db.CatalogBrands.CountAsync());
        Assert.Equal(4, await db.CatalogTypes.CountAsync());
        Assert.True(await db.CatalogItems.CountAsync() >= 12, "The seeder should insert the 12 sample products.");
    }

    [Fact]
    public async Task RejectsCatalogItemNameLongerThanTheColumn()
    {
        await using var db = _fixture.CreateCatalogContext();
        var tooLong = new string('x', 51);
        db.CatalogItems.Add(new CatalogItem(1, 1, "description", tooLong, 1m, "pic.png"));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        Assert.Contains("truncated", ex.InnerException?.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        await using var verify = _fixture.CreateCatalogContext();
        Assert.False(await verify.CatalogItems.AnyAsync(i => i.Name == tooLong));
    }

    [Fact]
    public async Task RejectsCatalogItemWithUnknownBrand()
    {
        await using var db = _fixture.CreateCatalogContext();
        var name = $"orphan {Guid.NewGuid():N}";
        db.CatalogItems.Add(new CatalogItem(1, 9999, "description", name, 1m, "pic.png"));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        Assert.Contains("FOREIGN KEY", ex.InnerException?.Message ?? string.Empty);
        await using var verify = _fixture.CreateCatalogContext();
        Assert.False(await verify.CatalogItems.AnyAsync(i => i.Name == name));
    }

    [Fact]
    public async Task PaginatedFilterSpecificationTranslatesToSql()
    {
        await using var db = _fixture.CreateCatalogContext();
        var repository = new EfRepository<CatalogItem>(db);
        var expected = await db.CatalogItems.Where(i => i.CatalogTypeId == 2).OrderBy(i => i.Id).Select(i => i.Id).ToListAsync();

        var filteredCount = await repository.CountAsync(new CatalogFilterSpecification(null, 2));
        var page = await repository.ListAsync(new CatalogFilterPaginatedSpecification(0, 100, null, 2));

        Assert.Equal(expected.Count, filteredCount);
        Assert.Equal(expected, page.Select(i => i.Id).OrderBy(id => id).ToList());
        Assert.All(page, i => Assert.Equal(2, i.CatalogTypeId));
    }

    [Fact]
    public async Task OrderWithOwnedAddressAndItemsRoundTripsThroughANewContext()
    {
        var buyerId = $"buyer-{Guid.NewGuid():N}";
        var items = new List<OrderItem>
        {
            new(new CatalogItemOrdered(1, "Mug", "mug.png"), 5.50m, 2),
            new(new CatalogItemOrdered(2, "Shirt", "shirt.png"), 19.49m, 1)
        };
        var address = new Address("1 Main St", "Kyiv", "KY", "Ukraine", "01001");

        int orderId;
        await using (var write = _fixture.CreateCatalogContext())
        {
            var saved = await new EfRepository<Order>(write).AddAsync(new Order(buyerId, address, items));
            orderId = saved.Id;
        }

        await using var read = _fixture.CreateCatalogContext();
        var order = await new EfRepository<Order>(read).FirstOrDefaultAsync(new OrderWithItemsByIdSpec(orderId));

        Assert.NotNull(order);
        Assert.Equal(buyerId, order!.BuyerId);
        Assert.Equal("1 Main St", order.ShipToAddress.Street);
        Assert.Equal("01001", order.ShipToAddress.ZipCode);
        Assert.Equal(2, order.OrderItems.Count);
        Assert.Equal(30.49m, order.Total());
        Assert.Contains(order.OrderItems, i => i.ItemOrdered.ProductName == "Shirt" && i.UnitPrice == 19.49m && i.Units == 1);
    }

    [Fact]
    public async Task RolledBackTransactionLeavesNoOrder()
    {
        var buyerId = $"rollback-{Guid.NewGuid():N}";
        await using (var db = _fixture.CreateCatalogContext())
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            db.Orders.Add(new Order(buyerId, new Address("s", "c", "st", "co", "z"),
                new List<OrderItem> { new(new CatalogItemOrdered(1, "Mug", "mug.png"), 1m, 1) }));
            await db.SaveChangesAsync();
            Assert.True(await db.Orders.AnyAsync(o => o.BuyerId == buyerId), "The order is visible inside the transaction.");

            await transaction.RollbackAsync();
        }

        await using var verify = _fixture.CreateCatalogContext();
        Assert.False(await verify.Orders.AnyAsync(o => o.BuyerId == buyerId), "A rolled-back order must not be stored.");
    }

    [Fact]
    public async Task BasketItemsAreLoadedAndEmptyQuantitiesRemoved()
    {
        var buyerId = $"basket-{Guid.NewGuid():N}";
        int basketId;
        await using (var write = _fixture.CreateCatalogContext())
        {
            var basket = new Basket(buyerId);
            basket.AddItem(1, 10m, 2);
            basket.AddItem(2, 20m, 1);
            write.Baskets.Add(basket);
            await write.SaveChangesAsync();
            basketId = basket.Id;
        }

        await using (var update = _fixture.CreateCatalogContext())
        {
            var repository = new EfRepository<Basket>(update);
            var basket = await repository.FirstOrDefaultAsync(new BasketWithItemsSpecification(basketId));
            Assert.NotNull(basket);
            basket!.Items.Single(i => i.CatalogItemId == 2).SetQuantity(0);
            basket.RemoveEmptyItems();
            await repository.UpdateAsync(basket);
        }

        await using var read = _fixture.CreateCatalogContext();
        var stored = await new EfRepository<Basket>(read).FirstOrDefaultAsync(new BasketWithItemsSpecification(buyerId));
        var item = Assert.Single(stored!.Items);
        Assert.Equal(1, item.CatalogItemId);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(1, await read.Set<BasketItem>().CountAsync(i => i.BasketId == basketId));
    }
}
