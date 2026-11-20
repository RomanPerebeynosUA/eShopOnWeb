using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.eShopWeb.Web.Interfaces;
using Microsoft.Extensions.Hosting;
using Testcontainers.MsSql;
using Xunit;

namespace Microsoft.eShopWeb.FunctionalTests.Web;

/// <summary>
/// The Web app with its production SQL Server wiring, pointed at a throwaway SQL Server container.
/// Unlike <see cref="TestApplication"/>, nothing is replaced: the app's own startup applies the
/// migrations and seeds both databases. Requires Docker; skip with
/// <c>dotnet test --filter Category!=SqlServer</c>.
/// </summary>
public class SqlServerWebApplication : WebApplicationFactory<IBasketViewModelService>, IAsyncLifetime
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04").Build();

    public Task InitializeAsync() => _container.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _container.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("UseOnlyInMemoryDatabase", "false");
        builder.UseSetting("ConnectionStrings:CatalogConnection", WithDatabase("eShopCatalog"));
        builder.UseSetting("ConnectionStrings:IdentityConnection", WithDatabase("eShopIdentity"));
    }

    private string WithDatabase(string database) =>
        new SqlConnectionStringBuilder(_container.GetConnectionString()) { InitialCatalog = database }.ConnectionString;
}

/// <summary>Shares one SQL Server container across all SQL-backed Web test classes.</summary>
[CollectionDefinition(Name)]
public class SqlServerWebCollection : ICollectionFixture<SqlServerWebApplication>
{
    public const string Name = "SqlServerWeb";
}
