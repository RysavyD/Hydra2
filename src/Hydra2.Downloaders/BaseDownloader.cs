using System.Globalization;
using System.Net;
using System.Text;
using HtmlAgilityPack;
using Hydra2.Service.Model;
using Microsoft.Extensions.Logging;

namespace Hydra2.Downloaders;

public abstract class BaseDownloader : ISpotInformationDownloader
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    protected string Page { get; set; } = string.Empty;
    protected string Id { get; }
    protected string Link { get; private set; } = string.Empty;
    protected HtmlDocument Document { get; private set; } = new();
    protected HtmlNode? Table { get; set; }
    protected string DecimalSeparator { get; set; } = ",";
    private NumberFormatInfo _numberFormat = new() { NumberDecimalSeparator = "," };

    protected BaseDownloader(HttpClient httpClient, ILogger logger, string id)
    {
        _httpClient = httpClient;
        _logger = logger;
        Id = id;
    }

    public virtual async Task<IList<SpotRecord>> GetRecordsAsync(string link, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Downloading from {Link}", link);

        Link = link;
        var result = new List<SpotRecord>();

        await DownLoadPageAsync(cancellationToken);
        ModifyPage();
        LoadDocument();
        SetTable();
        SetDecimalSeparator();
        LoadData(result);

        _logger.LogDebug("Parsed {Count} samples from {Link}", result.Count, link);
        return result;
    }

    protected static string PutTableInHtml(string table)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head></head>");
        sb.AppendLine("<body>");
        sb.AppendLine(table);
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    protected virtual async Task DownLoadPageAsync(CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(Link, cancellationToken);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        Page = Encoding.UTF8.GetString(bytes);
    }

    protected virtual void ModifyPage()
    {
    }

    protected virtual void LoadDocument()
    {
        Document = new HtmlDocument();
        Document.LoadHtml(Page);
    }

    protected virtual void SetTable()
    {
        Table = Document.GetElementbyId(Id);
    }

    protected virtual void SetDecimalSeparator()
    {
        _numberFormat = new NumberFormatInfo { NumberDecimalSeparator = DecimalSeparator };
    }

    protected virtual void LoadData(IList<SpotRecord> result)
    {
        if (Table is null) return;

        foreach (var row in Table.Descendants("tr").Skip(1))
        {
            var tds = row.Descendants("td").Select(td => WebUtility.HtmlDecode(td.InnerText).Trim()).ToArray();
            if (string.IsNullOrWhiteSpace(tds[0])) continue;
            if (!DateTime.TryParse(tds[0], out var dt)) continue;
            if (dt.Minute != 0) continue;

            var record = new SpotRecord
            {
                TimeStamp = dt,
                Level = string.IsNullOrWhiteSpace(tds[1]) ? null : Convert.ToSingle(tds[1], _numberFormat),
                Flow = string.IsNullOrWhiteSpace(tds[2]) ? null : Convert.ToSingle(tds[2], _numberFormat),
            };

            if (tds.Length > 3 && !string.IsNullOrWhiteSpace(tds[3]))
            {
                if (float.TryParse(tds[3].Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out var t))
                    record.Temperature = t;
                else if (float.TryParse(tds[3].Replace(".", ","), NumberStyles.Float, new NumberFormatInfo { NumberDecimalSeparator = "," }, out t))
                    record.Temperature = t;
            }

            result.Add(record);
        }
    }
}
