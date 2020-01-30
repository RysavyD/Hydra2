using System;
using System.Configuration;
using Hydra2.Service;
using NLog;

namespace Hydra2.DownLoaders
{
    public class Update
    {
        public static void UpdateSpots(int startIndex, int stopIndex)
        {
            NLogger.Log(LogLevel.Info, string.Format("Update Spots {0}-{1}", startIndex, stopIndex));
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.CreateSpecificCulture("cs-cz");

            var conn = ConfigurationManager.ConnectionStrings["Hydra2Connection"].ConnectionString;
            var dataService = new DataService(conn);

            for (int i = startIndex; i <= stopIndex; i++)
            {
                NLogger.Log(LogLevel.Info, "Index: " + i);

                try
                {
                    var station = dataService.GetStation(i);
                    if (station == null)
                        continue;

                    NLogger.Log(LogLevel.Info, "Stanice: " + station.Spot);

                    var downloader = GetDownloader(station.DownLoadType);

                    NLogger.Log(LogLevel.Debug, "DownloaderType: " + downloader.GetType());
                    var downLoadSamples = downloader.GetRecords(station.Link);

                    var count = 0;

                    foreach (var sample in downLoadSamples)
                    {
                        var rows = dataService.AddSample(station.Id, sample.Level, sample.Flow, sample.Temperature,
                            sample.TimeStamp);
                        count += rows;
                    }

                    NLogger.Log(LogLevel.Debug, "Vzorků: " + count + ", ukládám ...");
                    NLogger.Log(LogLevel.Info, "Uloženo");
                }
                catch (Exception ex)
                {
                    NLogger.Log(LogLevel.Error, ex.ToString());
                }
            }
        }

        public static void LastSpots()
        {
            NLogger.Log(LogLevel.Info, "Start update.");
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.CreateSpecificCulture("cs-cz");

            var conn = ConfigurationManager.ConnectionStrings["Hydra2Connection"].ConnectionString;
            var configService = new ConfigService(conn);
            var dataService = new DataService(conn);

            while (true)
            {
                try
                {
                    var config = configService.GetFirstConfig();
                    NLogger.Log(LogLevel.Info, "Načten config: " + config.Value);

                    config.Value = config.Value + 1;

                    if (config.Value > 650)
                        config.Value = 0;

                    configService.UpdateConfig(config.Id, config.Value);

                    var station = dataService.GetStation(config.Value);
                    if (station == null)
                        continue;

                    NLogger.Log(LogLevel.Info, "Stanice: " + station.Spot);

                    var downloader = GetDownloader(station.DownLoadType);

                    NLogger.Log(LogLevel.Debug, "DownloaderType: " + downloader.GetType());
                    var downLoadSamples = downloader.GetRecords(station.Link);

                    var count = 0;
                    foreach (var sample in downLoadSamples)
                    {
                        var rows = dataService.AddSample(station.Id, sample.Level, sample.Flow, sample.Temperature,
                            sample.TimeStamp);
                        count += rows;
                    }

                    NLogger.Log(LogLevel.Debug, "Vzorků: " + count + ", ukládám ...");
                    NLogger.Log(LogLevel.Info, "Uloženo");
                }
                catch (Exception ex)
                {
                    NLogger.Log(LogLevel.Error, ex.ToString());
                }
            }
        }

        public static ISpotInformationDownLoader GetDownloader(int dowloadType)
        {
            switch(dowloadType)
            {
                case 1:
                    return new Chmi();
                case 2:
                    return new Pvl();
                case 3:
                    return new PvlNadrze();
                case 4:
                    return new PlaNadrze();
                case 5:
                    return new PmoNadrze();
                case 6:
                    return new PmoToky();
                default:
                    return null;                  
            }
        }
    }
}
