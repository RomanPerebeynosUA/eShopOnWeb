using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.MsSql;
using Xunit;

namespace Microsoft.eShopWeb.IntegrationTests.SqlServer;

/// <summary>
/// Starts one real SQL Server container for the collection, applies the production
/// EF Core migrations for both contexts and runs the production seeders.
/// Requires Docker; skip with <c>dotnet test --filter Category!=SqlServer</c>.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    public const string Image = "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";

    private readonly MsSqlContainer _container = new MsSqlBuilder(Image).Build();

    public string CatalogConnectionString { get; private set; } = string.Empty;
    public string IdentityConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        CatalogConnectionString = WithDatabase("eShopCatalog");
        IdentityConnectionString = WithDatabase("eShopIdentity");

        await using (var catalog = CreateCatalogContext())
        {
            // The seeder applies migrations itself when the provider is SQL Server.
            await CatalogContextSeed.SeedAsync(catalog, NullLogger.Instance);
        }

        await using var services = CreateIdentityServices();
        using var scope = services.CreateScope();
        await AppIdentityDbContextSeed.SeedAsync(
            scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>(),
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public CatalogContext CreateCatalogContext() =>
        new(new DbContextOptionsBuilder<CatalogContext>().UseSqlServer(CatalogConnectionString).Options);

    public AppIdentityDbContext CreateIdentityContext() =>
        new(new DbContextOptionsBuilder<AppIdentityDbContext>().UseSqlServer(IdentityConnectionString).Options);

    /// <summary>Identity wired the way the apps wire it, but against the container database.</summary>
    public ServiceProvider CreateIdentityServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppIdentityDbContext>(o => o.UseSqlServer(IdentityConnectionString));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppIdentityDbContext>();
        return services.BuildServiceProvider();
    }

    private string WithDatabase(string database) =>
        new SqlConnectionStringBuilder(_container.GetConnectionString()) { InitialCatalog = database }.ConnectionString;
}

[CollectionDefinition(Name)]
public class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}
