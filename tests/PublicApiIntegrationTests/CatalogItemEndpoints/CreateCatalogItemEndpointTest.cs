using Microsoft.eShopWeb;
using Microsoft.eShopWeb.PublicApi.CatalogItemEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PublicApiIntegrationTests.AuthEndpoints;

[TestClass]
public class CreateCatalogItemEndpointTest
{
    private int _testBrandId = 1;
    private int _testTypeId = 2;
    private string _testDescription = "test description";
    private string _testName = "test name";
    private decimal _testPrice = 1.23m;


    [TestMethod]
    public async Task ReturnsNotAuthorizedGivenNormalUserToken()
    {
        const string forbiddenItemName = "forbidden create should not persist";
        var jsonContent = GetValidNewItemJson(forbiddenItemName);
        var token = ApiTokenHelper.GetNormalUserToken();
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsync("api/catalog-items", jsonContent);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);

        // Follow-up read: the rejected request must not have created an item.
        var readClient = ProgramTest.NewClient;
        var listResponse = await readClient.GetAsync("api/catalog-items");
        listResponse.EnsureSuccessStatusCode();
        var stringResponse = await listResponse.Content.ReadAsStringAsync();
        var model = stringResponse.FromJson<ListPagedCatalogItemResponse>();

        Assert.IsFalse(model!.CatalogItems.Any(i => i.Name == forbiddenItemName));
    }

    [TestMethod]
    public async Task ReturnsSuccessGivenValidNewItemAndAdminUserToken()
    {
        var jsonContent = GetValidNewItemJson();
        var adminToken = ApiTokenHelper.GetAdminUserToken();
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.PostAsync("api/catalog-items", jsonContent);
        response.EnsureSuccessStatusCode();
        var stringResponse = await response.Content.ReadAsStringAsync();
        var model = stringResponse.FromJson<CreateCatalogItemResponse>();

        Assert.AreEqual(_testBrandId, model!.CatalogItem.CatalogBrandId);
        Assert.AreEqual(_testTypeId, model.CatalogItem.CatalogTypeId);
        Assert.AreEqual(_testDescription, model.CatalogItem.Description);
        Assert.AreEqual(_testName, model.CatalogItem.Name);
        Assert.AreEqual(_testPrice, model.CatalogItem.Price);
    }

    [TestMethod]
    public async Task ReturnsConflictGivenDuplicateNameAndStoresOnlyOneItem()
    {
        var client = CatalogItemApi.AdminClient();
        var name = CatalogItemApi.UniqueName("duplicate");
        await CatalogItemApi.CreateAsync(client, name);

        var response = await client.PostAsync("api/catalog-items", CatalogItemApi.Json(CatalogItemApi.NewItem(name)));

        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        StringAssert.Contains(await response.Content.ReadAsStringAsync(), $"A catalogItem with name {name} already exists");
        Assert.AreEqual(1, (await CatalogItemApi.ListAllAsync(client)).Count(i => i.Name == name), "The duplicate must not be stored.");
    }

    [TestMethod]
    public async Task ReturnsBadRequestGivenMalformedJsonAndStoresNothing()
    {
        var client = CatalogItemApi.AdminClient();
        var malformed = new StringContent("{\"name\": \"broken\", \"price\": ", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("api/catalog-items", malformed);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode, await response.Content.ReadAsStringAsync());
        Assert.IsFalse((await CatalogItemApi.ListAllAsync(client)).Any(i => i.Name == "broken"), "A malformed request must not create an item.");
    }

    [TestMethod]
    public async Task ReturnsBadRequestGivenEmptyNameAndStoresNothing()
    {
        var client = CatalogItemApi.AdminClient();
        var description = CatalogItemApi.UniqueName("empty name");
        var request = CatalogItemApi.NewItem(string.Empty);
        request.Description = description;

        var response = await client.PostAsync("api/catalog-items", CatalogItemApi.Json(request));

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode, await response.Content.ReadAsStringAsync());
        Assert.IsFalse((await CatalogItemApi.ListAllAsync(client)).Any(i => i.Description == description), "An invalid item must not be stored.");
    }

    private StringContent GetValidNewItemJson(string? name = null)
    {
        var request = new CreateCatalogItemRequest()
        {
            CatalogBrandId = _testBrandId,
            CatalogTypeId = _testTypeId,
            Description = _testDescription,
            Name = name ?? _testName,
            Price = _testPrice
        };
        var jsonContent = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        return jsonContent;
    }
}
