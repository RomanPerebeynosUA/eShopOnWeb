using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web.Controllers;

[Collection("Sequential")]
public class ManageControllerGenerateRecoveryCodes : IClassFixture<TestApplication>
{
    private readonly TestApplication _factory;

    public ManageControllerGenerateRecoveryCodes(TestApplication factory)
    {
        _factory = factory;
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public HttpClient Client { get; }

    [Fact]
    public async Task WarningPageShowsGenerateFormForUserWith2fa()
    {
        var email = await WebTestUsers.CreateAndLoginAsync(_factory, Client);
        await EnableTwoFactorAsync(email);

        var response = await Client.GetAsync("/manage/generate-recovery-codes-warning");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Generate two-factor authentication (2FA) recovery codes", html);
        Assert.Contains("action=\"/manage/generate-recovery-codes\"", html);
    }

    [Fact]
    public async Task WarningPageFailsForUserWithout2fa()
    {
        var email = await WebTestUsers.CreateAndLoginAsync(_factory, Client);

        var response = await Client.GetAsync("/manage/generate-recovery-codes-warning");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("because they do not have 2FA enabled", body);
        Assert.DoesNotContain("action=\"/manage/generate-recovery-codes\"", body);
        Assert.Equal(0, await WebTestUsers.WithUserManagerAsync(_factory, (um, user) => um.CountRecoveryCodesAsync(user), email));
    }

    [Fact]
    public async Task PostReplacesRecoveryCodesAndShowsTheNewOnes()
    {
        var email = await WebTestUsers.CreateAndLoginAsync(_factory, Client);
        await EnableTwoFactorAsync(email);
        var oldCodes = await GetStoredRecoveryCodesAsync(email);

        var response = await WebTestUsers.PostFormAsync(Client, "/manage/generate-recovery-codes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        var newCodes = await GetStoredRecoveryCodesAsync(email);
        Assert.Equal(10, newCodes.Length);
        Assert.Empty(newCodes.Intersect(oldCodes));
        Assert.All(newCodes, code => Assert.Contains($"<code>{code}</code>", html));
    }

    [Fact]
    public async Task PostFailsForUserWithout2faAndGeneratesNoCodes()
    {
        var email = await WebTestUsers.CreateAndLoginAsync(_factory, Client);

        var response = await WebTestUsers.PostFormAsync(Client, "/manage/generate-recovery-codes");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(0, await WebTestUsers.WithUserManagerAsync(_factory, (um, user) => um.CountRecoveryCodesAsync(user), email));
    }

    private Task<string[]> GetStoredRecoveryCodesAsync(string email) =>
        WebTestUsers.WithUserManagerAsync(_factory, async (um, user) =>
            ((await um.GetAuthenticationTokenAsync(user, "[AspNetUserStore]", "RecoveryCodes")) ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries), email);

    private Task EnableTwoFactorAsync(string email) =>
        WebTestUsers.WithUserManagerAsync(_factory, async (um, user) =>
        {
            await um.ResetAuthenticatorKeyAsync(user);
            await um.SetTwoFactorEnabledAsync(user, true);
            await um.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
            return true;
        }, email);
}
