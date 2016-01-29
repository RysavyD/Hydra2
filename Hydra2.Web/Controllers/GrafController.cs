using System;
using System.Linq;
using System.Web.Mvc;
using Hydra2.Web.Models;

namespace Hydra2.Web.Controllers
{
    public class GrafController : Controller
    {
        public ActionResult Index()
        {
            using (var entities = new Model.Hydra2Entities())
            {
                var model = new GrafViewModel()
                {
                    Rivers = entities.River
                    .Select(r => new SelectListItem()
                    {
                        Text = r.Name,
                        Value = r.Id.ToString(),
                    })
                    .ToList(),
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

        public JsonResult GetData(int river, int spot, string start, string stop, string type)
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
    }
}