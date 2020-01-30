using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using Hydra2.Service;

namespace Hydra2.Web
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            NLogger.Log(NLog.LogLevel.Info, "MvcApplication Start");

            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            JobsScheduler.Start();
        }
    }
}
