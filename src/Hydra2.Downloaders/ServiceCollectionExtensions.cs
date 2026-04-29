using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hydra2.Downloaders;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHydra2Downloaders(this IServiceCollection services)
    {
        services.AddHttpClient<Chmi>(ConfigureClient);
        services.AddHttpClient<Pvl>(ConfigureClient);
        services.AddHttpClient<PvlNadrze>(ConfigureClient);
        services.AddHttpClient<PlaNadrze>(ConfigureClient);
        services.AddHttpClient<PmoNadrze>(ConfigureClient);
        services.AddHttpClient<PmoToky>(ConfigureClient);

        services.AddSingleton<IDownloaderFactory, DownloaderFactory>();
        services.TryAddSingleton<IUpdateProgressListener, NullUpdateProgressListener>();
        services.AddScoped<IUpdateService, UpdateService>();

        return services;

        static void ConfigureClient(HttpClient client)
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Hydra2/2.0 (+https://hydra2.dusanrysavy.cz)");
        }
    }
}
