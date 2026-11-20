using System.Linq;
using System.Threading.Tasks;
using Microsoft.eShopWeb;
using Microsoft.eShopWeb.PublicApi.CatalogTypeEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.CatalogTypeEndpoints;

[TestClass]
public class CatalogTypeListEndpointTest
{
    [TestMethod]
    public async Task ReturnsAllSeededCatalogTypes()
    {
        var client = ProgramTest.NewClient;

        var response = await client.GetAsync("/api/catalog-types");

        response.EnsureSuccessStatusCode();
        var model = (await response.Content.ReadAsStringAsync()).FromJson<ListCatalogTypesResponse>();
        Assert.IsNotNull(model);
        CollectionAssert.AreEquivalent(
            new[] { "Mug", "T-Shirt", "Sheet", "USB Memory Stick" },
            model!.CatalogTypes.Select(t => t.Name).ToArray());
        Assert.IsTrue(model.CatalogTypes.All(t => t.Id > 0));
        Assert.AreEqual(model.CatalogTypes.Count, model.CatalogTypes.Select(t => t.Id).Distinct().Count());
    }
}
