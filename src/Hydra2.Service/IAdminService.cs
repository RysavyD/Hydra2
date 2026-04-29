using Hydra2.Service.Data.Admin;

namespace Hydra2.Service;

public interface IAdminService
{
    Task<int> GetSamplesCountAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<SpotOverviewModel>> GetSpotOverviewAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<SpotOverviewModel>> GetSpotOverviewWithSamplesAsync(CancellationToken cancellationToken = default);
    Task<DateTime?> GetLastSampleAsync(int spotId, CancellationToken cancellationToken = default);
}
