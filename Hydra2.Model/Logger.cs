using System;
using NLog;

namespace Hydra2.Model
{
    public class NLogger
    {
        public static void Log(LogLevel level, string message)
        {
            Logger logger = LogManager.GetCurrentClassLogger();
            LogEventInfo ev = new LogEventInfo(level, logger.Name, message)
            {
                TimeStamp = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Central Europe Standard Time")
            };
            logger.Log(ev);
        }
    }
}
