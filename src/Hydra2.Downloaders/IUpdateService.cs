namespace Hydra2.Downloaders;

public interface IUpdateService
{
    Task UpdateSpotsAsync(int startIndex, int stopIndex, CancellationToken cancellationToken = default);
    Task LastSpotsLoopAsync(CancellationToken cancellationToken);
    Task UpdateNextSpotAsync(CancellationToken cancellationToken = default);
}
