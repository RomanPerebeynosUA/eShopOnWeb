using BlazorShared.Models;
using Microsoft.eShopWeb;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace PublicApiIntegrationTests.CatalogItemEndpoints;

[TestClass]
public class DeleteCatalogItemEndpointTest
{
    [TestMethod]
    public async Task ReturnsSuccessGivenValidIdAndAdminUserToken()
    {
        // Arrange: delete a dedicated item, not a seed row other tests read.
        var client = CatalogItemApi.AdminClient();
        var item = await CatalogItemApi.CreateAsync(client, CatalogItemApi.UniqueName("delete me"));

        var response = await client.DeleteAsync($"api/catalog-items/{item.Id}");

        response.EnsureSuccessStatusCode();
        var model = (await response.Content.ReadAsStringAsync()).FromJson<DeleteCatalogItemResponse>();
        Assert.AreEqual("Deleted", model!.Status);
        Assert.IsNull(await CatalogItemApi.TryGetAsync(client, item.Id), "The deleted item should no longer be readable.");
    }

    [TestMethod]
    public async Task ReturnsNotFoundGivenInvalidIdAndAdminUserToken()
    {
        var client = CatalogItemApi.AdminClient();
        var before = await CatalogItemApi.ListAllAsync(client);
        var unknownId = before.Max(i => i.Id) + 1000;

        var response = await client.DeleteAsync($"api/catalog-items/{unknownId}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual(string.Empty, await response.Content.ReadAsStringAsync(), "A 404 should not report a deletion.");
        var after = await CatalogItemApi.ListAllAsync(client);
        CollectionAssert.AreEquivalent(before.Select(i => i.Id).ToArray(), after.Select(i => i.Id).ToArray(),
            "Deleting an unknown id must not remove any item.");
    }

    [TestMethod]
    public async Task ReturnsForbiddenGivenNormalUserTokenAndKeepsTheItem()
    {
        var item = await CatalogItemApi.CreateAsync(CatalogItemApi.AdminClient(), CatalogItemApi.UniqueName("keep me"));

        var response = await CatalogItemApi.NormalUserClient().DeleteAsync($"api/catalog-items/{item.Id}");

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.IsNotNull(await CatalogItemApi.TryGetAsync(ProgramTest.NewClient, item.Id), "A forbidden delete must leave the item in place.");
    }
}
