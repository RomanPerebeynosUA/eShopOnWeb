using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web;

/// <summary>
/// Helpers for tests that need their own signed-in user, so that tests which change
/// passwords, 2FA or external logins never mutate the shared seeded demo user.
/// </summary>
public static class WebTestUsers
{
    public const string DefaultPassword = "Pass@word1";

    public static async Task<string> CreateUserAsync(TestApplication factory, string password = DefaultPassword)
    {
        var email = $"test-{Guid.NewGuid():N}@example.com";
        await WithUserManagerAsync(factory, async userManager =>
        {
            var result = await userManager.CreateAsync(new ApplicationUser { UserName = email, Email = email }, password);
            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
        });
        return email;
    }

    public static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password = DefaultPassword)
    {
        var token = await GetAntiForgeryTokenAsync(client, "/identity/account/login");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            [WebPageHelpers.TokenTag] = token
        });

        var response = await client.PostAsync("/identity/account/login", form);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
        return response;
    }

    public static async Task<string> CreateAndLoginAsync(TestApplication factory, HttpClient client)
    {
        var email = await CreateUserAsync(factory);
        await LoginAsync(client, email);
        return email;
    }

    /// <summary>
    /// Anti-forgery tokens are bound to the current identity, so fetch them from a page
    /// rendered for the same (anonymous or signed-in) user that will post the form.
    /// </summary>
    public static async Task<string> GetAntiForgeryTokenAsync(HttpClient client, string pageUrl = "/manage/my-account")
    {
        var response = await client.GetAsync(pageUrl);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var token = WebPageHelpers.GetRequestVerificationToken(html);
        Assert.False(string.IsNullOrEmpty(token), $"No anti-forgery token found on {pageUrl}.");
        return token;
    }

    public static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string url,
        IDictionary<string, string>? fields = null, string tokenPageUrl = "/manage/my-account")
    {
        var values = new Dictionary<string, string>(fields ?? new Dictionary<string, string>())
        {
            [WebPageHelpers.TokenTag] = await GetAntiForgeryTokenAsync(client, tokenPageUrl)
        };
        return await client.PostAsync(url, new FormUrlEncodedContent(values));
    }

    public static async Task WithUserManagerAsync(TestApplication factory, Func<UserManager<ApplicationUser>, Task> action)
    {
        using var scope = factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>());
    }

    public static async Task<T> WithUserManagerAsync<T>(TestApplication factory, Func<UserManager<ApplicationUser>, ApplicationUser, Task<T>> action, string email)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        return await action(userManager, user!);
    }

    public static Task<ApplicationUser> GetUserAsync(TestApplication factory, string email) =>
        WithUserManagerAsync(factory, (_, user) => Task.FromResult(user), email);

    public static async Task<Order> CreateOrderAsync(TestApplication factory, string buyerEmail, string productName, decimal unitPrice, int units)
    {
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<Order>>();
        var item = new OrderItem(new CatalogItemOrdered(1, productName, "http://catalogbaseurltobereplaced/images/products/1.png"), unitPrice, units);
        var address = new Address("123 Test Street", "Testville", "TS", "Testland", "12345");
        return await repository.AddAsync(new Order(buyerEmail, address, new List<OrderItem> { item }));
    }

    /// <summary>
    /// Computes the current RFC 6238 code for an authenticator key, matching ASP.NET Core Identity's
    /// AuthenticatorTokenProvider (HMAC-SHA1, 30 second steps, 6 digits, base32 key).
    /// </summary>
    public static string ComputeTotp(string base32Key)
    {
        var key = FromBase32(base32Key);
        var timestep = (ulong)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);
        var counter = BitConverter.GetBytes(timestep);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counter);
        }

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counter);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
                     | (hash[offset + 1] << 16)
                     | (hash[offset + 2] << 8)
                     | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }

    private static byte[] FromBase32(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var cleaned = input.Replace(" ", string.Empty).TrimEnd('=').ToUpperInvariant();
        var output = new List<byte>();
        int buffer = 0, bitsLeft = 0;
        foreach (var c in cleaned)
        {
            var value = alphabet.IndexOf(c);
            if (value < 0)
            {
                throw new FormatException($"Invalid base32 character '{c}'.");
            }

            buffer = (buffer << 5) | value;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                output.Add((byte)(buffer >> (bitsLeft - 8)));
                bitsLeft -= 8;
            }
        }

        return output.ToArray();
    }
}
