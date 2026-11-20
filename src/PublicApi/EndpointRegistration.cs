using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi;

public static class EndpointRegistration
{
    /// <summary>
    /// Registers this assembly's <see cref="IEndpoint"/> types for <c>app.MapEndpoints()</c>.
    /// MinimalApi.Endpoint's own <c>AddEndpoints()</c> calls GetTypes() on every assembly loaded
    /// in the process, which throws once Microsoft.Data.SqlClient 5.1 is loaded (its SqlGuidCaster
    /// type cannot be loaded by reflection). Scanning only this assembly avoids that.
    /// </summary>
    public static IServiceCollection AddPublicApiEndpoints(this IServiceCollection services)
    {
        var endpointTypes = typeof(EndpointRegistration).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IEndpoint).IsAssignableFrom(t));

        foreach (var endpointType in endpointTypes)
        {
            services.AddScoped(typeof(IEndpoint), endpointType);
        }

        return services;
    }
}
