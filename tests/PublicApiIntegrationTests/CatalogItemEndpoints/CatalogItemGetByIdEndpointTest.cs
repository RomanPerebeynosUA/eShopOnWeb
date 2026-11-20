using Microsoft.eShopWeb;
using Microsoft.eShopWeb.PublicApi.CatalogItemEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace PublicApiIntegrationTests.CatalogItemEndpoints;

[TestClass]
public class CatalogItemGetByIdEndpointTest
{
    [TestMethod]
    public async Task ReturnsItemGivenValidId()
    {
        var response = await ProgramTest.NewClient.GetAsync("api/catalog-items/5");
        response.EnsureSuccessStatusCode();
        var stringResponse = await response.Content.ReadAsStringAsync();
        var model = stringResponse.FromJson<GetByIdCatalogItemResponse>();

        Assert.AreEqual(5, model!.CatalogItem.Id);
        Assert.AreEqual("Roslyn Red Sheet", model.CatalogItem.Name);
    }

    [TestMethod]
    public async Task ReturnsNotFoundGivenInvalidId()
    {
        var client = ProgramTest.NewClient;
        var unknownId = (await CatalogItemApi.ListAllAsync(client)).Max(i => i.Id) + 1000;

        var response = await client.GetAsync($"api/catalog-items/{unknownId}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual(string.Empty, await response.Content.ReadAsStringAsync(), "A 404 should not carry a catalog item.");
    }

    [TestMethod]
    public async Task ReturnsNotFoundGivenIdOfDeletedItem()
    {
        var admin = CatalogItemApi.AdminClient();
        var item = await CatalogItemApi.CreateAsync(admin, CatalogItemApi.UniqueName("get deleted"));
        (await admin.DeleteAsync($"api/catalog-items/{item.Id}")).EnsureSuccessStatusCode();

        var response = await ProgramTest.NewClient.GetAsync($"api/catalog-items/{item.Id}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual(string.Empty, await response.Content.ReadAsStringAsync());
    }
}
