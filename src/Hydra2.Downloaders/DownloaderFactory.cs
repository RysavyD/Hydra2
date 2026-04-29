using Microsoft.Extensions.DependencyInjection;

namespace Hydra2.Downloaders;

public class DownloaderFactory : IDownloaderFactory
{
    private readonly IServiceProvider _serviceProvider;

    public DownloaderFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ISpotInformationDownloader? GetDownloader(int downloadType) => downloadType switch
    {
        1 => _serviceProvider.GetRequiredService<Chmi>(),
        2 => _serviceProvider.GetRequiredService<Pvl>(),
        3 => _serviceProvider.GetRequiredService<PvlNadrze>(),
        4 => _serviceProvider.GetRequiredService<PlaNadrze>(),
        5 => _serviceProvider.GetRequiredService<PmoNadrze>(),
        6 => _serviceProvider.GetRequiredService<PmoToky>(),
        _ => null,
    };
}
