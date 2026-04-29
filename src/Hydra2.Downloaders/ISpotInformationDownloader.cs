using Hydra2.Service.Model;

namespace Hydra2.Downloaders;

public interface ISpotInformationDownloader
{
    Task<IList<SpotRecord>> GetRecordsAsync(string link, CancellationToken cancellationToken = default);
}
