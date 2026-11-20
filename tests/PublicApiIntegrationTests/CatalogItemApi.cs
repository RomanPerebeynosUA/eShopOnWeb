using Microsoft.eShopWeb;
using Microsoft.eShopWeb.PublicApi.CatalogItemEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PublicApiIntegrationTests;

/// <summary>
/// Shared calls against the catalog-items API, so tests create their own data
/// instead of changing seed rows other tests read.
/// </summary>
public static class CatalogItemApi
{
    public const int SeedBrandId = 1;
    public const int SeedTypeId = 2;

    public static HttpClient AdminClient() => ClientWithToken(ApiTokenHelper.GetAdminUserToken());

    public static HttpClient NormalUserClient() => ClientWithToken(ApiTokenHelper.GetNormalUserToken());

    public static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    public static StringContent Json(object request) =>
        new(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

    public static CreateCatalogItemRequest NewItem(string name, decimal price = 1.23m) => new()
    {
        CatalogBrandId = SeedBrandId,
        CatalogTypeId = SeedTypeId,
        Description = "test description",
        Name = name,
        Price = price
    };

    public static async Task<CatalogItemDto> CreateAsync(HttpClient adminClient, string name, decimal price = 1.23m)
    {
        var response = await adminClient.PostAsync("api/catalog-items", Json(NewItem(name, price)));
        var body = await response.Content.ReadAsStringAsync();
        Assert.IsTrue(response.IsSuccessStatusCode, $"Creating '{name}' failed with {(int)response.StatusCode}: {body}");
        return body.FromJson<CreateCatalogItemResponse>()!.CatalogItem;
    }

    public static async Task<CatalogItemDto?> TryGetAsync(HttpClient client, int id)
    {
        var response = await client.GetAsync($"api/catalog-items/{id}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        var body = await response.Content.ReadAsStringAsync();
        Assert.IsTrue(response.IsSuccessStatusCode, $"Reading item {id} failed with {(int)response.StatusCode}: {body}");
        return body.FromJson<GetByIdCatalogItemResponse>()!.CatalogItem;
    }

    public static async Task<CatalogItemDto[]> ListAllAsync(HttpClient client)
    {
        var response = await client.GetAsync("api/catalog-items");
        var body = await response.Content.ReadAsStringAsync();
        Assert.IsTrue(response.IsSuccessStatusCode, $"Listing items failed with {(int)response.StatusCode}: {body}");
        return body.FromJson<ListPagedCatalogItemResponse>()!.CatalogItems.ToArray();
    }

    private static HttpClient ClientWithToken(string token)
    {
        var client = ProgramTest.NewClient;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
