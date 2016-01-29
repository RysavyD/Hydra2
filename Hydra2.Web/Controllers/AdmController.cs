using System.Linq;
using System.Web.Mvc;
using Hydra2.Model;

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

            return View();
        }
    }
}