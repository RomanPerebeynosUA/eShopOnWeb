using Microsoft.eShopWeb;
using Microsoft.eShopWeb.PublicApi.CatalogItemEndpoints;
using Microsoft.eShopWeb.Web.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace PublicApiIntegrationTests.CatalogItemEndpoints;

[TestClass]
public class CatalogItemListPagedEndpoint
{
    [TestMethod]
    public async Task ReturnsFirst10CatalogItems()
    {
        var client = ProgramTest.NewClient;
        var response = await client.GetAsync("/api/catalog-items?pageSize=10");
        response.EnsureSuccessStatusCode();
        var stringResponse = await response.Content.ReadAsStringAsync();
        var model = stringResponse.FromJson<CatalogIndexViewModel>();

        Assert.AreEqual(10, model!.CatalogItems.Count());
    }

    [TestMethod]
    public async Task ReturnsCorrectCatalogItemsGivenPageIndex1()
    {

        var pageSize = 10;
        var pageIndex = 1;

        var client = ProgramTest.NewClient;
        var response = await client.GetAsync($"/api/catalog-items");
        response.EnsureSuccessStatusCode();
        var stringResponse = await response.Content.ReadAsStringAsync();
        var model = stringResponse.FromJson<ListPagedCatalogItemResponse>();
        var totalItem = model!.CatalogItems.Count();

        var response2 = await client.GetAsync($"/api/catalog-items?pageSize={pageSize}&pageIndex={pageIndex}");
        response.EnsureSuccessStatusCode();
        var stringResponse2 = await response2.Content.ReadAsStringAsync();
        var model2 = stringResponse2.FromJson<ListPagedCatalogItemResponse>();

        var totalExpected = totalItem - (pageSize * pageIndex);

        Assert.AreEqual(totalExpected, model2!.CatalogItems.Count());
    }

    [DataTestMethod]
    [DataRow("catalog-items")]
    [DataRow("catalog-brands")]
    [DataRow("catalog-types")]
    [DataRow("catalog-items/1")]
    public async Task SuccessFullMutipleParallelCall(string endpointName)
    {
        var client = ProgramTest.NewClient;
        var tasks = new List<Task<HttpResponseMessage>>();

        for (int i = 0; i < 100; i++)
        {
            var task = client.GetAsync($"/api/{endpointName}");
            tasks.Add(task);
        }
        var responses = await Task.WhenAll(tasks.ToList());
        var totalKO = responses.Count(r => r.StatusCode != HttpStatusCode.OK);

        Assert.AreEqual(0, totalKO);

        // Assert on the content of every response, not only the status code.
        var bodies = new List<string>();
        foreach (var response in responses)
        {
            bodies.Add(await response.Content.ReadAsStringAsync());
        }

        Assert.AreEqual(100, bodies.Count);
        Assert.IsFalse(string.IsNullOrWhiteSpace(bodies[0]), "Response body must not be empty.");

        // Body must be a non-empty JSON object or array.
        using (var document = JsonDocument.Parse(bodies[0]))
        {
            var root = document.RootElement;
            Assert.IsTrue(
                root.ValueKind == JsonValueKind.Object || root.ValueKind == JsonValueKind.Array,
                $"Unexpected JSON root kind: {root.ValueKind}");
            if (root.ValueKind == JsonValueKind.Object)
            {
                Assert.IsTrue(root.EnumerateObject().Any(), "JSON object must have properties.");
            }
            else
            {
                Assert.IsTrue(root.GetArrayLength() > 0, "JSON array must have elements.");
            }
        }

        // The data is read-only here, so every parallel call must return the same payload
        // (same content and same ordering).
        Assert.AreEqual(1, bodies.Distinct().Count(), "Parallel calls returned differing payloads.");

        // Endpoint-specific value checks for the endpoints whose response types are known.
        if (endpointName == "catalog-items")
        {
            var model = bodies[0].FromJson<ListPagedCatalogItemResponse>();
            Assert.IsTrue(model!.CatalogItems.Any());
        }
        else if (endpointName == "catalog-items/1")
        {
            var model = bodies[0].FromJson<GetByIdCatalogItemResponse>();
            Assert.AreEqual(1, model!.CatalogItem.Id);
        }
    }
}
