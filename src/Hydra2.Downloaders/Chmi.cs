using Microsoft.Extensions.Logging;

namespace Hydra2.Downloaders;

public class Chmi : BaseDownloader
{
    public Chmi(HttpClient httpClient, ILogger<Chmi> logger) : base(httpClient, logger, "")
    {
        DecimalSeparator = ".";
    }

    protected override void SetTable()
    {
        var div = Document.DocumentNode.Descendants("div")
            .First(t => t.Attributes
                .Any(a => a.Name == "class" && a.Value == "tborder center_text"));

        Table = div.Descendants("table").First();
    }
}
