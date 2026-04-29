using Hydra2.Service.Data;

namespace Hydra2.Service;

public interface IConfigService
{
    Task<Config> GetFirstConfigAsync(CancellationToken cancellationToken = default);
    Task UpdateConfigAsync(int id, int value, CancellationToken cancellationToken = default);
}
