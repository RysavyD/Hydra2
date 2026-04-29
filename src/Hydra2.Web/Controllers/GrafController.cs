using System.Globalization;
using System.Xml;
using Hydra2.Service;
using Hydra2.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Hydra2.Web.Controllers;

public class GrafController : Controller
{
    private static readonly TimeZoneInfo CzechTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central Europe Standard Time");

    private readonly IDataService _dataService;

    public GrafController(IDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, CzechTimeZone);

        var rivers = (await _dataService.GetRiversAsync(cancellationToken))
            .OrderBy(r => r.Name)
            .Select(r => new SelectListItem { Text = r.Name, Value = r.Id.ToString() })
            .ToList();

        var model = new GrafViewModel
        {
            Rivers = rivers,
            StartDate = now.AddDays(-7).ToString("dd'/'MM'/'yyyy", CultureInfo.InvariantCulture),
            StopDate = now.AddDays(1).ToString("dd'/'MM'/'yyyy", CultureInfo.InvariantCulture),
        };

        return View(model);
    }

    public async Task<IActionResult> GetSpots(int id, CancellationToken cancellationToken)
    {
        var stations = (await _dataService.GetStationsAsync(id, cancellationToken))
            .OrderBy(s => s.Spot)
            .Select(s => new { name = s.Spot, id = s.Id });

        return Json(stations);
    }

    public async Task<IActionResult> GetData(int spot, string start, string stop, string type, CancellationToken cancellationToken)
    {
        var startDate = ParseDateTime(start);
        var stopDate = ParseDateTime(stop);

        var station = await _dataService.GetStationAsync(spot, cancellationToken);
        if (station is null) return NotFound();

        var samples = await GetSamplesAsync(type, spot, startDate, stopDate, cancellationToken);

        var result = new
        {
            spot = new
            {
                spa0 = station.Spa0,
                spa1 = station.Spa1,
                spa2 = station.Spa2,
                spa3 = station.Spa3,
                spa3e = station.Spa3e,
                link = station.Link,
                type = station.Type,
                raftLink = string.IsNullOrEmpty(station.RaftLink) ? "" : "http://www.raft.cz/" + station.RaftLink,
            },
            samples,
        };

        return Json(result);
    }

    private async Task<Sample[]> GetSamplesAsync(string type, int spot, DateTime startDate, DateTime stopDate, CancellationToken cancellationToken)
    {
        var useLevel = type.Contains('h');
        var useFlow = type.Contains('Q');
        var useTemperature = type.Contains('t');

        var samples = await _dataService.GetSamplesAsync(spot, startDate, stopDate, cancellationToken);

        return samples.Select(s => new Sample
        {
            Date = s.TimeStamp.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            t = useTemperature ? s.Temperature : null,
            Q = useFlow ? s.Flow : null,
            h = useLevel ? s.Level : null,
        }).ToArray();
    }

    private static DateTime ParseDateTime(string text)
    {
        var parts = text.Split('/');
        var day = Convert.ToInt32(parts[0], CultureInfo.InvariantCulture);
        var month = Convert.ToInt32(parts[1], CultureInfo.InvariantCulture);
        var year = Convert.ToInt32(parts[2], CultureInfo.InvariantCulture);
        return new DateTime(year, month, day);
    }

    public async Task<IActionResult> ExportRaft(int spot, string start, string stop, CancellationToken cancellationToken)
    {
        var startDate = ParseDateTime2(start);
        var stopDate = ParseDateTime2(stop);

        var station = await _dataService.GetStationAsync(spot, cancellationToken);
        if (station is null) return NotFound();

        var samples = (await _dataService.GetSamplesAsync(spot, startDate, stopDate, cancellationToken))
            .Where(s => s.Level.HasValue);

        var doc = new XmlDocument();
        doc.AppendChild(doc.CreateXmlDeclaration("1.0", "UTF-8", null));

        var stavy = doc.CreateElement("stavy");

        foreach (var sample in samples)
        {
            var stav = doc.CreateElement("s");

            var datum = doc.CreateElement("d");
            datum.InnerText = sample.TimeStamp.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
            stav.AppendChild(datum);

            var prutok = doc.CreateElement("p");
            prutok.InnerText = sample.Flow?.ToString(CultureInfo.InvariantCulture) ?? "";
            stav.AppendChild(prutok);

            var vyska = doc.CreateElement("v");
            vyska.InnerText = sample.Level.HasValue ? Convert.ToInt32(sample.Level.Value).ToString(CultureInfo.InvariantCulture) : "";
            stav.AppendChild(vyska);

            var teplota = doc.CreateElement("t");
            teplota.InnerText = sample.Temperature?.ToString(CultureInfo.InvariantCulture) ?? "";
            stav.AppendChild(teplota);

            stavy.AppendChild(stav);
        }

        AppendAttribute(doc, stavy, "spa1", station.Spa1);
        AppendAttribute(doc, stavy, "spa2", station.Spa2);
        AppendAttribute(doc, stavy, "spa3", station.Spa3);

        doc.AppendChild(stavy);

        return Content(doc.OuterXml, "text/xml");
    }

    private static void AppendAttribute(XmlDocument doc, XmlElement parent, string name, float? value)
    {
        var attr = doc.CreateAttribute(name);
        attr.Value = value?.ToString(CultureInfo.InvariantCulture) ?? "";
        parent.Attributes.Append(attr);
    }

    private static DateTime ParseDateTime2(string text)
    {
        var year = Convert.ToInt32(text.Substring(0, 4), CultureInfo.InvariantCulture);
        var month = Convert.ToInt32(text.Substring(4, 2), CultureInfo.InvariantCulture);
        var day = Convert.ToInt32(text.Substring(6, 2), CultureInfo.InvariantCulture);
        return new DateTime(year, month, day);
    }
}
