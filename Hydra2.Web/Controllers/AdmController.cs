using System.Linq;
using System.Web.Mvc;
using Hydra2.DownLoaders;
using Hydra2.Web.Models;
using System;
using System.Configuration;
using NLog;
using System.Reflection;
using Hydra2.Service;

namespace Hydra2.Web.Controllers
{
    public class AdmController : Controller
    {
        private readonly AdminService _adminService;
        private readonly DataService _dataService;

        public AdmController()
        {
            var conn = ConfigurationManager.ConnectionStrings["Hydra2Connection"].ConnectionString;
            _adminService = new AdminService(conn);
            _dataService = new DataService(conn);
        }

        public ActionResult Index()
        {
            NLogger.Log(LogLevel.Info, "Adm.Index");

            ViewBag.samplesCount = _adminService.GetSamplesCount();

            var ci = System.Threading.Thread.CurrentThread.CurrentCulture;
            ViewBag.culture = ci.CompareInfo.Name + " - " + ci.DisplayName;
            ViewBag.date = System.IO.File.GetCreationTime(Assembly.GetExecutingAssembly().Location);
                
            return View();
        }

        public ActionResult SpotOverView(bool samples = false)
        {
            var model = samples
                ? _adminService.GetSpotOverviewWitSamples().Select(s =>
                        new SpotOverviewViewModel
                        {
                            Id = s.Id,
                            RiverName = s.Name,
                            SpotName = s.Spot,
                            SpotType = s.Type,
                            Url = s.Link,
                            LastSample = s.LastSample
                        })
                    .OrderBy(s => s.LastSample)
                    .ToArray()
                : _adminService.GetSpotOverview().Select(s =>
                        new SpotOverviewViewModel
                        {
                            Id = s.Id,
                            RiverName = s.Name,
                            SpotName = s.Spot,
                            SpotType = s.Type,
                            Url = s.Link,
                        })
                    .OrderBy(s => s.RiverName)
                    .ThenBy(s => s.SpotName)
                    .ToArray();

            return View(model);
        }

        public DateTime GetLastSample(int id)
        {
            return _adminService.GetLastSample(id);
        }

        public ViewResult GetStationOverView(int id)
        {
            try
            {
                var station = _dataService.GetStation(id);
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
                foreach (var sample in downLoadSamples)
                {
                    sb.AppendLine($"{sample.TimeStamp} - h:{sample.Level}, q:{sample.Flow}, t:{sample.Temperature}");
                    sb.AppendLine("<br />");
                }

                ViewBag.Note = sb.ToString();

                NLogger.Log(LogLevel.Info, "Uloženo");
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
            NLogger.Log(LogLevel.Info, "Ručně aktualizuji záznam s Id:" + stationId);

            Update.UpdateSpots(stationId, stationId);
            return View();
        }
    }
}