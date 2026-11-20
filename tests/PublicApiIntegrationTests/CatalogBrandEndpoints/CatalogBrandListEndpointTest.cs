using System.Linq;
using System.Threading.Tasks;
using Microsoft.eShopWeb;
using Microsoft.eShopWeb.PublicApi.CatalogBrandEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PublicApiIntegrationTests.CatalogBrandEndpoints;

[TestClass]
public class CatalogBrandListEndpointTest
{
    [TestMethod]
    public async Task ReturnsAllSeededCatalogBrands()
    {
        var client = ProgramTest.NewClient;

        var response = await client.GetAsync("/api/catalog-brands");

        response.EnsureSuccessStatusCode();
        var model = (await response.Content.ReadAsStringAsync()).FromJson<ListCatalogBrandsResponse>();
        Assert.IsNotNull(model);
        CollectionAssert.AreEquivalent(
            new[] { "Azure", ".NET", "Visual Studio", "SQL Server", "Other" },
            model!.CatalogBrands.Select(b => b.Name).ToArray());
        Assert.IsTrue(model.CatalogBrands.All(b => b.Id > 0));
        Assert.AreEqual(model.CatalogBrands.Count, model.CatalogBrands.Select(b => b.Id).Distinct().Count());
    }

    [TestMethod]
    public async Task IsAvailableWithoutAuthentication()
    {
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = null;

        var response = await client.GetAsync("/api/catalog-brands");

        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        StringAssert.StartsWith(response.Content.Headers.ContentType?.MediaType, "application/json");
    }
}
