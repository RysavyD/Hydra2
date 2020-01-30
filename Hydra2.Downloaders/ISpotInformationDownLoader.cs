using System.Collections.Generic;
using Hydra2.Service.Model;

namespace Hydra2.DownLoaders
{
    public interface ISpotInformationDownLoader
    {
        IList<SpotRecord> GetRecords(string link);
    }
}
