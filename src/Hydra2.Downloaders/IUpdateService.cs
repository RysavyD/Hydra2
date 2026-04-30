namespace Hydra2.Downloaders;

public interface IUpdateService
{
    /// <summary>
    /// Updates all stations of the given DownLoadType. Used by Quartz per-source jobs and trigger endpoint.
    /// </summary>
    Task<SourceRunOutcome> UpdateSourceAsync(int downLoadType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a single station by id. Used by /Adm/HandUpdate and granular control.
    /// </summary>
    Task UpdateStationAsync(int stationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an inclusive range of station ids. Used by /Api/HandUpdate for batch retry.
    /// </summary>
    Task UpdateSpotsAsync(int startIndex, int stopIndex, CancellationToken cancellationToken = default);
}
