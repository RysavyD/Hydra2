using Microsoft.Extensions.Logging;

namespace Hydra2.Downloaders;

public class PmoNadrze : BaseDownloader
{
    public PmoNadrze(HttpClient httpClient, ILogger<PmoNadrze> logger) : base(httpClient, logger, "")
    {
    }

    protected override void SetTable()
    {
        Table = Document.DocumentNode.Descendants("table")
            .First(t => t.Attributes
                .Any(a => a.Name == "width" && a.Value == "300"));
    }
}
