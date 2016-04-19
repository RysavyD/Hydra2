using System;
using System.Linq;
using Hydra2.Model;
using NLog;

namespace Hydra2.DownLoaders
{
    public class Update
    {
        public static void UpdateSpots(int startIndex, int stopIndex)
        {
            NLogger.Log(LogLevel.Info, string.Format("Update Spots {0}-{1}", startIndex, stopIndex));
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.CreateSpecificCulture("cs-cz");

            for (int i = startIndex; i <= stopIndex; i++)
            {
                NLogger.Log(LogLevel.Info, "Index: " + i);

                try
                {
                    using (var entities = new Hydra2Entities())
                    {
                        var station = entities.Station.FirstOrDefault(s => s.Id == i);
                        if (station == null)
                            continue;

                        NLogger.Log(LogLevel.Info, "Stanice: " + station.Spot);

                        var downloader = GetDownloader(station.DownLoadType);

                        NLogger.Log(LogLevel.Debug, "DownloaderType: " + downloader.GetType());
                        var downLoadSamples = downloader.GetRecords(station.Link);

                        var lastSample = station.Sample.OrderByDescending(s => s.TimeStamp)
                            .FirstOrDefault();
                        var lastTime = (lastSample == null) ? new DateTime(2010, 1, 1) : lastSample.TimeStamp;
                        var count = 0;

                        foreach (var sample in downLoadSamples.Where(s => s.TimeStamp > lastTime))
                        {
                            entities.Sample.Add(new Sample()
                            {
                                Flow = sample.Flow,
                                Id_Station = station.Id,
                                Level = sample.Level,
                                Temperature = sample.Temperature,
                                TimeStamp = sample.TimeStamp,
                            });
                            count++;
                        }

                        NLogger.Log(LogLevel.Debug, "Vzorků: " + count + ", ukládám ...");
                        entities.SaveChanges();
                        NLogger.Log(LogLevel.Info, "Uloženo");
                    }
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
                default:
                    return null;                  
            }
        }
    }
}
