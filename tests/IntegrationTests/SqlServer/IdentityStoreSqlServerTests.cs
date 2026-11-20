using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Microsoft.eShopWeb.IntegrationTests.SqlServer;

/// <summary>
/// ASP.NET Core Identity against the real identity schema: unique indexes, token storage
/// and password hashes persisted across contexts.
/// </summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "SqlServer")]
public class IdentityStoreSqlServerTests
{
    private const string Password = "Pass@word1";
    private readonly SqlServerFixture _fixture;

    public IdentityStoreSqlServerTests(SqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SeededUsersAndAdminRoleArePresent()
    {
        await using var db = _fixture.CreateIdentityContext();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.True(await db.Users.AnyAsync(u => u.NormalizedUserName == "DEMOUSER@MICROSOFT.COM"));
        var adminRoleId = await db.Roles.Where(r => r.Name == BlazorShared.Authorization.Constants.Roles.ADMINISTRATORS).Select(r => r.Id).SingleAsync();
        var admin = await db.Users.SingleAsync(u => u.NormalizedUserName == "ADMIN@MICROSOFT.COM");
        Assert.True(await db.UserRoles.AnyAsync(ur => ur.UserId == admin.Id && ur.RoleId == adminRoleId), "The seeded admin should be in the Administrators role.");
    }

    [Fact]
    public async Task CreatedUserCanSignInWithPasswordReadFromANewScope()
    {
        var email = NewEmail();
        await using var services = _fixture.CreateIdentityServices();
        using (var scope = services.CreateScope())
        {
            var result = await Users(scope).CreateAsync(new ApplicationUser { UserName = email, Email = email }, Password);
            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        using var readScope = services.CreateScope();
        var user = await Users(readScope).FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.True(await Users(readScope).CheckPasswordAsync(user!, Password));
        Assert.False(await Users(readScope).CheckPasswordAsync(user!, "wrong-password"));
    }

    [Fact]
    public async Task DatabaseRejectsDuplicateNormalizedUserName()
    {
        var email = NewEmail();
        await using var db = _fixture.CreateIdentityContext();
        db.Users.Add(new ApplicationUser { UserName = email, NormalizedUserName = email.ToUpperInvariant() });
        await db.SaveChangesAsync();

        await using var second = _fixture.CreateIdentityContext();
        second.Users.Add(new ApplicationUser { UserName = email, NormalizedUserName = email.ToUpperInvariant() });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
        Assert.Contains("UserNameIndex", ex.InnerException?.Message ?? string.Empty);
        await using var verify = _fixture.CreateIdentityContext();
        Assert.Equal(1, await verify.Users.CountAsync(u => u.NormalizedUserName == email.ToUpperInvariant()));
    }

    [Fact]
    public async Task RecoveryCodesArePersistedAndRedeemableOnlyOnce()
    {
        var email = NewEmail();
        await using var services = _fixture.CreateIdentityServices();
        string code;
        using (var scope = services.CreateScope())
        {
            var users = Users(scope);
            var user = new ApplicationUser { UserName = email, Email = email };
            Assert.True((await users.CreateAsync(user, Password)).Succeeded);
            await users.SetTwoFactorEnabledAsync(user, true);
            code = (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))!.First();
        }

        using (var scope = services.CreateScope())
        {
            var users = Users(scope);
            var user = await users.FindByEmailAsync(email);
            Assert.True(await users.GetTwoFactorEnabledAsync(user!));
            Assert.Equal(10, await users.CountRecoveryCodesAsync(user!));
            Assert.True((await users.RedeemTwoFactorRecoveryCodeAsync(user!, code)).Succeeded);
        }

        using var lastScope = services.CreateScope();
        var reloaded = await Users(lastScope).FindByEmailAsync(email);
        Assert.Equal(9, await Users(lastScope).CountRecoveryCodesAsync(reloaded!));
        Assert.False((await Users(lastScope).RedeemTwoFactorRecoveryCodeAsync(reloaded!, code)).Succeeded, "A recovery code must not work twice.");
    }

    private static UserManager<ApplicationUser> Users(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    private static string NewEmail() => $"sql-{Guid.NewGuid():N}@example.com";
}
