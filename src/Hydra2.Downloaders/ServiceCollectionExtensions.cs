using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Hydra2.Downloaders;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHydra2Downloaders(this IServiceCollection services)
    {
        services.AddHttpClient<Chmi>(ConfigureClient).AddResilience();
        services.AddHttpClient<Pvl>(ConfigureClient).AddResilience();
        services.AddHttpClient<PvlNadrze>(ConfigureClient).AddResilience();
        services.AddHttpClient<PlaNadrze>(ConfigureClient).AddResilience();
        services.AddHttpClient<PmoNadrze>(ConfigureClient).AddResilience();
        services.AddHttpClient<PmoToky>(ConfigureClient).AddResilience();

        services.AddSingleton<IDownloaderFactory, DownloaderFactory>();
        services.AddSingleton<IStationErrorTracker, StationErrorTracker>();
        services.AddSingleton<ISourceStateTracker, SourceStateTracker>();
        services.TryAddSingleton<IUpdateProgressListener, NullUpdateProgressListener>();
        services.AddScoped<IUpdateService, UpdateService>();

        return services;

        static void ConfigureClient(HttpClient client)
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Hydra2/2.0 (+https://hydra2.dusanrysavy.cz)");
        }
    }

    private static IHttpClientBuilder AddResilience(this IHttpClientBuilder builder)
    {
        builder.AddResilienceHandler("scraper", pipeline =>
        {
            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
            });
        });
        return builder;
    }
}
