using Microsoft.Extensions.Logging;

namespace Hydra2.Downloaders;

public class PvlNadrze : BaseDownloader
{
    public PvlNadrze(HttpClient httpClient, ILogger<PvlNadrze> logger) : base(httpClient, logger, "dataMereni24hGV")
    {
        DecimalSeparator = ",";
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
