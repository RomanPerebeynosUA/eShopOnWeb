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

namespace PublicApiIntegrationTests.CatalogItemEndpoints;

[TestClass]
public class UpdateCatalogItemEndpointTest
{
    private readonly int _testBrandId = 1;
    private readonly int _testTypeId = 2;
    private readonly string _updatedName = "updated test name";
    private readonly string _updatedDescription = "updated test description";
    private readonly decimal _updatedPrice = 4.56m;

    [TestMethod]
    public async Task ReturnsNotAuthorizedGivenNormalUserToken()
    {
        var token = ApiTokenHelper.GetNormalUserToken();
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Arrange: snapshot the seed item (id 1) before the rejected request.
        var before = await GetItemAsync(client, 1);

        var response = await client.PutAsync("api/catalog-items", GetUpdateJson(1));

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);

        // Assert: the rejected update must leave the item unchanged.
        var after = await GetItemAsync(client, 1);

        Assert.AreEqual(before.CatalogItem.Id, after.CatalogItem.Id);
        Assert.AreEqual(before.CatalogItem.Name, after.CatalogItem.Name);
        Assert.AreEqual(before.CatalogItem.Description, after.CatalogItem.Description);
        Assert.AreEqual(before.CatalogItem.Price, after.CatalogItem.Price);
        Assert.AreEqual(before.CatalogItem.CatalogBrandId, after.CatalogItem.CatalogBrandId);
        Assert.AreEqual(before.CatalogItem.CatalogTypeId, after.CatalogItem.CatalogTypeId);
    }

    [TestMethod]
    public async Task ReturnsNotFoundGivenNonExistentItemIdAndAdminUserToken()
    {
        var adminToken = ApiTokenHelper.GetAdminUserToken();
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var unknownId = (await CatalogItemApi.ListAllAsync(client)).Max(i => i.Id) + 1000;
        var name = CatalogItemApi.UniqueName("update unknown");
        var request = new
        {
            Id = unknownId,
            CatalogBrandId = _testBrandId,
            CatalogTypeId = _testTypeId,
            Description = _updatedDescription,
            Name = name,
            Price = _updatedPrice
        };

        var response = await client.PutAsync("api/catalog-items", CatalogItemApi.Json(request));

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual(string.Empty, await response.Content.ReadAsStringAsync(), "A 404 should not return an item.");
        Assert.IsFalse((await CatalogItemApi.ListAllAsync(client)).Any(i => i.Name == name), "Updating an unknown id must not create an item.");
        Assert.IsNull(await CatalogItemApi.TryGetAsync(client, unknownId));
    }

    [TestMethod]
    public async Task RejectsNonPositivePriceAndKeepsStoredItem()
    {
        var client = CatalogItemApi.AdminClient();
        var item = await CatalogItemApi.CreateAsync(client, CatalogItemApi.UniqueName("bad price"), 3.21m);
        var request = new
        {
            item.Id,
            CatalogBrandId = _testBrandId,
            CatalogTypeId = _testTypeId,
            Description = "should not be stored",
            Name = CatalogItemApi.UniqueName("bad price renamed"),
            Price = 0m
        };

        var response = await client.PutAsync("api/catalog-items", CatalogItemApi.Json(request));

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode, await response.Content.ReadAsStringAsync());
        var stored = await CatalogItemApi.TryGetAsync(client, item.Id);
        Assert.IsNotNull(stored);
        Assert.AreEqual(item.Name, stored.Name, "A rejected update must not rename the item.");
        Assert.AreEqual(3.21m, stored.Price, "A rejected update must not change the price.");
    }

    [TestMethod]
    public async Task ReturnsUpdatedItemAndPersistsChangesGivenValidRequestAndAdminUserToken()
    {
        var adminToken = ApiTokenHelper.GetAdminUserToken();
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Arrange: create a dedicated item so shared seed data is not modified.
        var itemId = await CreateItemAsync(client);

        // Act
        var response = await client.PutAsync("api/catalog-items", GetUpdateJson(itemId));
        response.EnsureSuccessStatusCode();
        var stringResponse = await response.Content.ReadAsStringAsync();
        var model = stringResponse.FromJson<UpdateCatalogItemResponse>();

        // Assert: response values
        Assert.AreEqual(itemId, model!.CatalogItem.Id);
        Assert.AreEqual(_updatedName, model.CatalogItem.Name);
        Assert.AreEqual(_updatedDescription, model.CatalogItem.Description);
        Assert.AreEqual(_updatedPrice, model.CatalogItem.Price);
        Assert.AreEqual(_testBrandId, model.CatalogItem.CatalogBrandId);
        Assert.AreEqual(_testTypeId, model.CatalogItem.CatalogTypeId);

        // Assert: persisted state, read back through the API
        var getResponse = await client.GetAsync($"api/catalog-items/{itemId}");
        getResponse.EnsureSuccessStatusCode();
        var getString = await getResponse.Content.ReadAsStringAsync();
        var persisted = getString.FromJson<GetByIdCatalogItemResponse>();

        Assert.AreEqual(itemId, persisted!.CatalogItem.Id);
        Assert.AreEqual(_updatedName, persisted.CatalogItem.Name);
        Assert.AreEqual(_updatedDescription, persisted.CatalogItem.Description);
        Assert.AreEqual(_updatedPrice, persisted.CatalogItem.Price);
        Assert.AreEqual(_testBrandId, persisted.CatalogItem.CatalogBrandId);
        Assert.AreEqual(_testTypeId, persisted.CatalogItem.CatalogTypeId);
    }

    private async Task<int> CreateItemAsync(HttpClient client)
    {
        var request = new CreateCatalogItemRequest()
        {
            CatalogBrandId = _testBrandId,
            CatalogTypeId = _testTypeId,
            Description = "original description",
            Name = "original name",
            Price = 1.23m
        };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("api/catalog-items", content);
        response.EnsureSuccessStatusCode();
        var stringResponse = await response.Content.ReadAsStringAsync();
        var model = stringResponse.FromJson<CreateCatalogItemResponse>();

        return model!.CatalogItem.Id;
    }

    private async Task<GetByIdCatalogItemResponse> GetItemAsync(HttpClient client, int id)
    {
        var response = await client.GetAsync($"api/catalog-items/{id}");
        response.EnsureSuccessStatusCode();
        var stringResponse = await response.Content.ReadAsStringAsync();
        return stringResponse.FromJson<GetByIdCatalogItemResponse>()!;
    }

    private StringContent GetUpdateJson(int id)
    {
        var request = new
        {
            Id = id,
            CatalogBrandId = _testBrandId,
            CatalogTypeId = _testTypeId,
            Description = _updatedDescription,
            Name = _updatedName,
            Price = _updatedPrice
        };

        return new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
    }
}
