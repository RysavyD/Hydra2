using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
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
                    Rivers = GetRiversFromDb(),

                    StartDate = now.AddDays(-7).ToString("dd'/'MM'/'yyyy"),
                    StopDate = now.AddDays(1).ToString("dd'/'MM'/'yyyy"),
                };

                return View(model);
            }
        }

        public List<SelectListItem> GetRiversFromDb()
        {
            using (var entities = new Model.Hydra2Entities())
            {
                return entities.River
                        .OrderBy(r => r.Name)
                        .Select(r => new SelectListItem()
                        {
                            Text = r.Name,
                            Value = r.Id.ToString(),
                        })
                        .ToList();
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
            System.Threading.Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en-GB");
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-GB");

            var stationEntity = GetStationEntity(spot);
            var samples = GetSamples(type, spot, startDate, stopDate);

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
                    raftLink = string.IsNullOrEmpty(stationEntity.River.RaftLink) 
                        ? "" 
                        : "http://www.raft.cz/" + stationEntity.River.RaftLink,
                },
                samples
            };

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        private Hydra2.Model.Station GetStationEntity(int spot)
        {
            using (var entities = new Model.Hydra2Entities())
            {
                var stationEntity = entities.Station.First(r => r.Id == spot);
                return new Hydra2.Model.Station
                {
                    Spa0 = stationEntity.Spa0,
                    Spa1 = stationEntity.Spa1,
                    Spa2 = stationEntity.Spa2,
                    Spa3 = stationEntity.Spa3,
                    Spa3e = stationEntity.Spa3e,
                    Link = stationEntity.Link,
                    Type = stationEntity.Type,
                    River = new Hydra2.Model.River()
                    {
                        RaftLink = stationEntity.River.RaftLink
                    }
                };
            }
        }

        private Sample[] GetSamples(string type, int spot, DateTime startDate, DateTime stopDate)
        {
            var useLevel = type.Contains("h");
            var useFlow = type.Contains("Q");
            var useTemperature = type.Contains("t");

            var hours = (int)(stopDate - startDate).TotalHours;
            var result = new Sample[hours];
            var random1 = new Random();
            var random2 = new Random(random1.Next(0, 25000));
            var random3 = new Random(random1.Next(0, 55000));

            var lastLevel = 100f;
            var lastFlow = 8.5f;
            var lastTemperature = 3.5f;

            for (int i = 0; i < hours; i++)
            {
                var rand1 = random1.NextDouble();
                var rand2 = random2.NextDouble();
                var rand3 = random3.NextDouble();
                var nextLevel = Convert.ToSingle(lastLevel + lastLevel * ((rand1 - 0.5) / 5));
                var nextFlow = Convert.ToSingle(lastFlow + lastFlow * ((rand2 - 0.5) / 5));
                var nextTemperature = Convert.ToSingle(lastTemperature + lastTemperature * ((rand3 - 0.5) / 5));
                result[i] = new Sample
                {
                    Date = startDate.AddHours(i).ToString("yyyy-MM-dd HH:mm"),
                    h = useLevel ? nextLevel : (float?)null,
                    Q = useFlow ? nextFlow : (float?)null,
                    t = useTemperature ? nextTemperature : (float?)null
                };

                lastLevel = nextLevel;
                lastFlow = nextFlow;
                lastTemperature = nextTemperature;
            }


            return result;
        }

        private Sample[] GetSamples1(string type, int spot, DateTime startDate, DateTime stopDate)
        {
            var useLevel = type.Contains("h");
            var useFlow = type.Contains("Q");
            var useTemperature = type.Contains("t");
            using (var entities = new Model.Hydra2Entities())
            {
                Sample[] samples = null;
                samples = entities.Sample
                        .Where(s => s.Id_Station == spot && s.TimeStamp >= startDate && s.TimeStamp <= stopDate && s.Temperature.HasValue)
                        .OrderBy(s => s.TimeStamp)
                        .Select(s => new Sample()
                        {
                            Date = s.TimeStamp.ToString("yyyy-MM-dd HH:mm"),
                            t = useTemperature ? s.Temperature : null,
                            Q = useFlow ? s.Flow : null,
                            h = useLevel ? s.Level : null
                        })
                        .ToArray();

                return samples ?? new Sample[0];
            }
        }


        public JsonResult GetRivers()
        {
            using (var entities = new Model.Hydra2Entities())
            {
                var model = entities.River.OrderBy(r => r.Name)
                        .Select(r => new
                        {
                            Name = r.Name,
                            Id = r.Id,
                        })
                        .ToArray();

                return Json(model, JsonRequestBehavior.AllowGet);
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

            System.Threading.Thread.CurrentThread.CurrentCulture =
                System.Threading.Thread.CurrentThread.CurrentUICulture =
                new System.Globalization.CultureInfo("en-US");

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