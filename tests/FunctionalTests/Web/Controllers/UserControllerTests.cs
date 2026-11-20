using System.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using BlazorShared.Authorization;
using Microsoft.eShopWeb.Web.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

[Collection("Sequential")]
public class UserControllerTests : IClassFixture<TestApplication>
{
    private readonly TestApplication _factory;

    public UserControllerTests(TestApplication factory)
    {
        _factory = factory;
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public HttpClient Client { get; }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task GetReturnsAnonymousUserInfoWhenNotSignedIn()
    {
        var response = await Client.GetAsync("/user");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var info = JsonSerializer.Deserialize<UserInfo>(await response.Content.ReadAsStringAsync(), JsonOptions);
        Assert.NotNull(info);
        Assert.False(info!.IsAuthenticated);
        Assert.Null(info.Token);
        Assert.Null(info.Claims);
    }

    [Fact]
    public async Task GetReturnsClaimsAndApiTokenForSignedInUser()
    {
        var email = await WebTestUsers.CreateAndLoginAsync(_factory, Client);

        var response = await Client.GetAsync("/user");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var info = JsonSerializer.Deserialize<UserInfo>(await response.Content.ReadAsStringAsync(), JsonOptions);
        Assert.NotNull(info);
        Assert.True(info!.IsAuthenticated);
        Assert.Contains(info.Claims, c => c.Type == info.NameClaimType && c.Value == email);
        Assert.False(string.IsNullOrEmpty(info.Token));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(info.Token);
        Assert.Contains(jwt.Claims, c => c.Value == email);
    }

    [Fact]
    public async Task LogoutRevokesTheAuthenticationCookie()
    {
        var email = await WebTestUsers.CreateUserAsync(_factory);
        var loginResponse = await WebTestUsers.LoginAsync(Client, email);
        var authCookie = loginResponse.Headers.GetValues("Set-Cookie")
            .Select(c => c.Split(';')[0])
            .Single(c => c.StartsWith($"{ConfigureCookieSettings.IdentifierCookieName}="));

        // A second client that replays the captured cookie, as a stolen or cached cookie would be.
        var replayClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });
        Assert.True(await IsAuthenticatedWithCookieAsync(replayClient, authCookie));

        var logoutResponse = await Client.PostAsync("/user/logout", null);

        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);
        Assert.False(await IsAuthenticatedWithCookieAsync(replayClient, authCookie));
        var current = JsonSerializer.Deserialize<UserInfo>(await (await Client.GetAsync("/user")).Content.ReadAsStringAsync(), JsonOptions);
        Assert.False(current!.IsAuthenticated);
    }

    private static async Task<bool> IsAuthenticatedWithCookieAsync(HttpClient client, string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/user");
        request.Headers.Add("Cookie", cookie);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var info = JsonSerializer.Deserialize<UserInfo>(await response.Content.ReadAsStringAsync(), JsonOptions);
        return info!.IsAuthenticated;
    }
}
