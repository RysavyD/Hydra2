using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hydra2.Service;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHydra2Services(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<Hydra2Options>(configuration.GetSection(Hydra2Options.SectionName));

        services.AddScoped<IDataService, DataService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IConfigService, ConfigService>();

        return services;
    }
}
