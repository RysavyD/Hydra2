using Microsoft.Extensions.Logging;

namespace Hydra2.Downloaders;

public class Pvl : BaseDownloader
{
    public Pvl(HttpClient httpClient, ILogger<Pvl> logger) : base(httpClient, logger, "ObsahCPH_DataMereniGV")
    {
        DecimalSeparator = ",";
    }
}
