using System;
using System.Configuration;
using System.Web.Mvc;
using Hydra2.DownLoaders;
using Hydra2.Service;
using Hydra2.Service.Model;

namespace Hydra2.Web.Controllers
{
    public class ApiController : Controller
    {
        public JsonResult HandUpdate(int id)
        {
            Update.UpdateSpots(id, id);

            return Json($"Ok {id}");
        }

        [HttpPost]
        public JsonResult ManualData(int stationId, string token, SpotRecord[] records)
        {
            string secretToken = ConfigurationManager.AppSettings["secretToken"];
            if (token != secretToken || string.IsNullOrEmpty(secretToken))
                return Json("Bad token");

            try
            {
                var conn = ConfigurationManager.ConnectionStrings["Hydra2Connection"].ConnectionString;
                var dataService = new DataService(conn);
                int count = 0;
                foreach (var sample in records)
                {
                    var rows = dataService.AddSample(stationId, sample.Level, sample.Flow, sample.Temperature,
                        sample.TimeStamp);
                    count += rows;
                }

                return Json($"Ok {stationId}, {count}");
            }
            catch (Exception e)
            {
                return Json($"Error {e.Message}");
            }
        }

        public JsonResult UpdateNext(string token)
        {
            string secretToken = ConfigurationManager.AppSettings["secretToken"];
            if (token != secretToken || string.IsNullOrEmpty(secretToken))
                return Json("Bad token");

            try
            {
                Update.LastSpots();
                return Json("OK");
            }
            catch (Exception e)
            {
                return Json($"Error {e.Message}");
            }
        }

        public JsonResult GetLast(string token)
        {
            string secretToken = ConfigurationManager.AppSettings["secretToken"];
            if (token != secretToken || string.IsNullOrEmpty(secretToken))
                return Json("Bad token");

            try
            {
                var conn = ConfigurationManager.ConnectionStrings["Hydra2Connection"].ConnectionString;
                var configService = new ConfigService(conn);
                var config = configService.GetFirstConfig();

                return Json(config.Value);
            }
            catch (Exception e)
            {
                return Json($"Error {e.Message}");
            }
        }
    }
}