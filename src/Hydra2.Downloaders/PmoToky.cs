using Microsoft.Extensions.Logging;

namespace Hydra2.Downloaders;

public class PmoToky : BaseDownloader
{
    public PmoToky(HttpClient httpClient, ILogger<PmoToky> logger) : base(httpClient, logger, "")
    {
    }

    protected override void SetTable()
    {
        var nodes = Document.DocumentNode.SelectNodes("//*/table");
        if (nodes is null) return;

        var table = nodes.Where(x => x.Attributes["bordercolor"]?.Value == "gray");
        Table = table.Skip(1).FirstOrDefault();
    }
}
