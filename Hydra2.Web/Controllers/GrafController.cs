using System;
using System.Linq;
using System.Web.Mvc;
using System.Xml;
using Hydra2.Web.Models;

namespace Hydra2.Web.Controllers
{
    public class GrafController : Controller
    {
        public ActionResult Index()
        {
            var now = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Central Europe Standard Time");

            using (var entities = new Model.Hydra2Entities())
            {
                var model = new GrafViewModel()
                {
                    Rivers = entities.River
                        .OrderBy(r => r.Name)
                        .Select(r => new SelectListItem()
                        {
                            Text = r.Name,
                            Value = r.Id.ToString(),
                        })
                        .ToList(),
                    StartDate = now.AddDays(-7).ToString("dd'/'MM'/'yyyy"),
                    StopDate = now.AddDays(1).ToString("dd'/'MM'/'yyyy"),
                };

                return View(model);
            }
        }

        public JsonResult GetSpots(int id)
        {
            using (var entities = new Model.Hydra2Entities())
            {
                var model = entities.Station.Where(s => s.Id_River == id)
                    .OrderBy(s => s.Spot)
                    .Select(s => new { name = s.Spot, id = s.Id })
                    .ToArray();

                return Json(model, JsonRequestBehavior.AllowGet);
            }
        }

        public JsonResult GetData(int spot, string start, string stop, string type)
        {
            DateTime startDate = ParseDateTime(start);
            DateTime stopDate = ParseDateTime(stop);

            using (var entities = new Model.Hydra2Entities())
            {
                var stationEntity = entities.Station.First(r => r.Id == spot);

                Sample[] samples = new Sample[0];
                if (type == "h")
                    samples =
                        entities.Sample.Where(
                            s =>
                                s.Id_Station == spot && s.TimeStamp >= startDate && s.TimeStamp <= stopDate &&
                                s.Level.HasValue)
                            .OrderBy(s => s.TimeStamp)
                            .Select(s => new Sample() {Date = s.TimeStamp, Value = s.Level})
                            .ToArray();

                if (type == "Q")
                    samples =
                        entities.Sample.Where(
                            s =>
                                s.Id_Station == spot && s.TimeStamp >= startDate && s.TimeStamp <= stopDate &&
                                s.Flow.HasValue)
                            .OrderBy(s => s.TimeStamp)
                            .Select(s => new Sample() { Date = s.TimeStamp, Value = s.Flow })
                            .ToArray();

                if (type == "t")
                    samples =
                        entities.Sample.Where(
                            s =>
                                s.Id_Station == spot && s.TimeStamp >= startDate && s.TimeStamp <= stopDate &&
                                s.Temperature.HasValue)
                            .OrderBy(s => s.TimeStamp)
                            .Select(s => new Sample() { Date = s.TimeStamp, Value = s.Temperature })
                            .ToArray();

                var jsonSamples = samples.Select(s => new { date = s.Date.ToString("yyyy-MM-dd HH:mm"), value = s.Value.ToString() });

                var result = new
                {
                    spot = new
                    {
                        spa0 = stationEntity.Spa0,
                        spa1 = stationEntity.Spa1,
                        spa2 = stationEntity.Spa2,
                        spa3 = stationEntity.Spa3,
                        spa3e = stationEntity.Spa3e,
                        link = stationEntity.Link,
                        type = stationEntity.Type,
                        raftLink = string.IsNullOrEmpty(stationEntity.River.RaftLink) ? "" : "http://www.raft.cz/" + stationEntity.River.RaftLink,
                    },
                    samples = jsonSamples,
                };

                return Json(result, JsonRequestBehavior.AllowGet);
            }
        }

        private DateTime ParseDateTime(string text)
        {
            var arr = text.Split('/');
            int day = Convert.ToInt32(arr[0]);
            int month = Convert.ToInt32(arr[1]);
            int year = Convert.ToInt32(arr[2]);

            return new DateTime(year, month, day);
        }

        public ActionResult ExportRaft(int spot, string start, string stop)
        {
            DateTime startDate = ParseDateTime2(start);
            DateTime stopDate = ParseDateTime2(stop);

            XmlDocument doc = new XmlDocument();
            XmlNode docNode = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
            doc.AppendChild(docNode);

            XmlNode stavy = doc.CreateElement("stavy");

            using (var entities = new Model.Hydra2Entities())
            {
                var stationEntity = entities.Station.First(r => r.Id == spot);

                var samples = entities.Sample.Where(
                        s =>
                            s.Id_Station == spot && s.TimeStamp >= startDate && s.TimeStamp <= stopDate &&
                            s.Level.HasValue)
                        .OrderBy(s => s.TimeStamp);

                foreach(var sample in samples)
                {
                    XmlNode stav = doc.CreateElement("s");
                    XmlNode datum = doc.CreateElement("d");
                    datum.InnerText = sample.TimeStamp.ToString("dd.MM.yyyy HH:mm");
                    stav.AppendChild(datum);

                    XmlNode prutok = doc.CreateElement("p");
                    prutok.InnerText = sample.Flow.HasValue ? sample.Flow.ToString() : "";
                    stav.AppendChild(prutok);

                    XmlNode vyska = doc.CreateElement("v");
                    vyska.InnerText = sample.Level.HasValue ? Convert.ToInt32(sample.Level).ToString() : "";
                    stav.AppendChild(vyska);

                    XmlNode teplota = doc.CreateElement("t");
                    teplota.InnerText = sample.Temperature.HasValue ? sample.Temperature.ToString() : "";
                    stav.AppendChild(teplota);

                    stavy.AppendChild(stav);
                }

                var spa1 = doc.CreateAttribute("spa1");
                spa1.Value = stationEntity.Spa1.HasValue ? stationEntity.Spa1.ToString() : "";
                stavy.Attributes.Append(spa1);

                var spa2 = doc.CreateAttribute("spa2");
                spa2.Value = stationEntity.Spa2.HasValue ? stationEntity.Spa2.ToString() : "";
                stavy.Attributes.Append(spa2);

                var spa3 = doc.CreateAttribute("spa3");
                spa3.Value = stationEntity.Spa3.HasValue ? stationEntity.Spa3.ToString() : "";
                stavy.Attributes.Append(spa3);

                doc.AppendChild(stavy);

                return Content(doc.OuterXml, "text/xml");
            }
        }

        private DateTime ParseDateTime2(string text)
        {
            int year = Convert.ToInt32(text.Substring(0, 4));
            int month = Convert.ToInt32(text.Substring(4, 2));
            int day = Convert.ToInt32(text.Substring(6, 2));

            return new DateTime(year, month, day);
        }

    }
}