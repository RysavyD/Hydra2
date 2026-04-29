using Microsoft.Extensions.Logging;

namespace Hydra2.Downloaders;

public class PlaNadrze : BaseDownloader
{
    public PlaNadrze(HttpClient httpClient, ILogger<PlaNadrze> logger) : base(httpClient, logger, "dataMereni24hGV")
    {
    }

    protected override void ModifyPage()
    {
        int idPosition = Page.IndexOf(Id, StringComparison.Ordinal);
        int start = Page[..idPosition].LastIndexOf("<table", StringComparison.Ordinal);
        int stop = Page.IndexOf("/table", start, StringComparison.Ordinal);

        var tableString = Page.Substring(start, stop - start + 8);
        Page = PutTableInHtml(tableString);
    }
}
