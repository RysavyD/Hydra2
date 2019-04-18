using System.Linq;
using System.Web.Mvc;
using Hydra2.DownLoaders;
using Hydra2.Model;
using System;
using NLog;
using System.Reflection;

namespace Hydra2.Web.Controllers
{
    public class AdmController : Controller
    {
        public ActionResult Index()
        {
            NLogger.Log(NLog.LogLevel.Info, "Adm.Index");

            using (var entities = new Hydra2Entities())
            {
                ViewBag.samplesCount = entities.Sample.Count();
            }

            var ci = System.Threading.Thread.CurrentThread.CurrentCulture;
            ViewBag.culture = ci.CompareInfo.Name + " - " + ci.DisplayName;
            ViewBag.date = System.IO.File.GetCreationTime(Assembly.GetExecutingAssembly().Location);
                
            return View();
        }

        public ActionResult SpotOverView()
        {
            using (var entities = new Hydra2Entities())
            {
                var model = entities.Station.Select(s =>
                new
                {
                    riverName = s.River.Name,
                    spotName = s.Spot,
                    url = s.Link,
                    lastSample = s.Sample.OrderByDescending(sm => sm.TimeStamp).FirstOrDefault().TimeStamp
                })
                .OrderBy(s => s.lastSample)
                .ToArray();

                ViewBag.Model = model;
            }
            return View();
        }

        public DateTime GetLastSample(int id)
        {
            using (var entities = new Hydra2Entities())
            {
                var station = entities.Station.FirstOrDefault(s => s.Id == id);
                var lastSample = station.Sample.OrderByDescending(s => s.TimeStamp).FirstOrDefault();
                return lastSample.TimeStamp;
            }
        }

        public ViewResult GetStationOverView(int id)
        {
            try
            {
                using (var entities = new Hydra2Entities())
                {
                    var station = entities.Station.FirstOrDefault(s => s.Id == id);
                    if (station == null)
                    {
                        ViewBag.Note = $"Stanice s id={id} nenalezena";
                        return View();
                    }

                    NLogger.Log(LogLevel.Info, "Stanice: " + station.Spot);

                    var downloader = Update.GetDownloader(station.DownLoadType);

                    NLogger.Log(LogLevel.Debug, "DownloaderType: " + downloader.GetType());
                    var downLoadSamples = downloader.GetRecords(station.Link);

                    var sb = new System.Text.StringBuilder();
                    foreach(var sample in downLoadSamples)
                    {
                        sb.AppendLine($"{sample.TimeStamp} - h:{sample.Level}, q:{sample.Flow}, t:{sample.Temperature}");
                        sb.AppendLine("<br />");
                    }

                    ViewBag.Note = sb.ToString();

                    NLogger.Log(LogLevel.Info, "Uloženo");
                }
            }
            catch (Exception ex)
            {
                ViewBag.Note = $"Výjimka: {ex.Message}";

                NLogger.Log(LogLevel.Error, ex.ToString());
            }

            return View();
        }

        public ActionResult HandUpdate()
        {
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken, ActionName("HandUpdate")]
        public ActionResult HandUpdatePost(int stationId)
        {
            NLogger.Log(NLog.LogLevel.Info, "Ručně aktualizuji záznam s Id:" + stationId);

            Update.UpdateSpots(stationId, stationId);
            return View();
        }
    }
}