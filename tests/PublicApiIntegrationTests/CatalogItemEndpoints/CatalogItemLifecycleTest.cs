using Microsoft.eShopWeb;
using Microsoft.eShopWeb.PublicApi.CatalogItemEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace PublicApiIntegrationTests.CatalogItemEndpoints;

/// <summary>
/// One admin journey through the catalog write API, checking the stored state after every step.
/// </summary>
[TestClass]
public class CatalogItemLifecycleTest
{
    [TestMethod]
    public async Task CreateReadUpdateDeleteRoundTripsThroughTheStore()
    {
        var admin = CatalogItemApi.AdminClient();
        var reader = ProgramTest.NewClient;
        var name = CatalogItemApi.UniqueName("lifecycle");

        // Create
        var created = await CatalogItemApi.CreateAsync(admin, name, 9.99m);
        Assert.IsTrue(created.Id > 0, "Create should return the generated id.");

        var afterCreate = await CatalogItemApi.TryGetAsync(reader, created.Id);
        Assert.IsNotNull(afterCreate, "Created item should be readable by an anonymous client.");
        Assert.AreEqual(name, afterCreate.Name);
        Assert.AreEqual(9.99m, afterCreate.Price);
        StringAssert.Contains(afterCreate.PictureUri, "eCatalog-item-default.png", "Create should set the placeholder picture.");
        Assert.AreEqual(1, (await CatalogItemApi.ListAllAsync(reader)).Count(i => i.Name == name), "List should contain the new item once.");

        // Update
        var renamed = CatalogItemApi.UniqueName("lifecycle renamed");
        var update = new
        {
            created.Id,
            CatalogBrandId = CatalogItemApi.SeedBrandId,
            CatalogTypeId = CatalogItemApi.SeedTypeId,
            Description = "renamed description",
            Name = renamed,
            Price = 12.50m
        };
        var updateResponse = await admin.PutAsync("api/catalog-items", CatalogItemApi.Json(update));
        Assert.AreEqual(HttpStatusCode.OK, updateResponse.StatusCode, await updateResponse.Content.ReadAsStringAsync());

        var afterUpdate = await CatalogItemApi.TryGetAsync(reader, created.Id);
        Assert.IsNotNull(afterUpdate);
        Assert.AreEqual(renamed, afterUpdate.Name);
        Assert.AreEqual("renamed description", afterUpdate.Description);
        Assert.AreEqual(12.50m, afterUpdate.Price);
        var listAfterUpdate = await CatalogItemApi.ListAllAsync(reader);
        Assert.IsFalse(listAfterUpdate.Any(i => i.Name == name), "The old name should be gone after the update.");

        // Delete
        var deleteResponse = await admin.DeleteAsync($"api/catalog-items/{created.Id}");
        Assert.AreEqual(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.AreEqual("Deleted", (await deleteResponse.Content.ReadAsStringAsync()).FromJson<DeleteCatalogItemResponse>()!.Status);

        Assert.IsNull(await CatalogItemApi.TryGetAsync(reader, created.Id), "Deleted item should return 404.");
        Assert.IsFalse((await CatalogItemApi.ListAllAsync(reader)).Any(i => i.Id == created.Id), "Deleted item should not be listed.");

        var secondDelete = await admin.DeleteAsync($"api/catalog-items/{created.Id}");
        Assert.AreEqual(HttpStatusCode.NotFound, secondDelete.StatusCode, "Deleting twice should be a 404, not a silent success.");
    }
}
