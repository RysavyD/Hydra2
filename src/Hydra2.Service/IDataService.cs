using Hydra2.Service.Data;

namespace Hydra2.Service;

public interface IDataService
{
    Task<IEnumerable<River>> GetRiversAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Station>> GetStationsAsync(int riverId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Station>> GetStationsByDownLoadTypeAsync(int downLoadType, CancellationToken cancellationToken = default);
    Task<Station?> GetStationAsync(int stationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Sample>> GetSamplesAsync(int spot, DateTime startDate, DateTime stopDate, CancellationToken cancellationToken = default);
    Task<int> AddSampleAsync(int stationId, float? sampleLevel, float? sampleFlow, float? sampleTemperature, DateTime sampleTimeStamp, CancellationToken cancellationToken = default);
}
