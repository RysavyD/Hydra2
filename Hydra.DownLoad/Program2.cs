using System;
using System.Diagnostics;
using Hydra2.DownLoaders;

namespace Hydra.DownLoad
{
    class Program2
    {
        static void Main()
        {
            string url = "http://www.pmo.cz/portal/sap/cz/mereni_135.htm";
            Stopwatch sw = new Stopwatch();
            sw.Start();

            for (int i = 1; i <= 6; i++)
            {
                var downloader = Update.GetDownloader(i);
                if (downloader == null)
                {
                    Console.WriteLine("{0} is null", i);
                    continue;
                }
                try
                {
                    var samples = downloader.GetRecords(url);
                    Console.WriteLine("{0} - {1} - {2}", i, downloader.GetType(), samples.Count);
                }
                catch(Exception ex)
                {
                    Console.WriteLine("{0} - {1} - error: {2}", i, downloader.GetType(), ex.Message);
                }
            }

            sw.Stop();
            Console.WriteLine();
            Console.WriteLine(sw.ElapsedMilliseconds);

            Console.WriteLine();
            Console.WriteLine("Done");
            Console.ReadKey();
        }
    }
}
