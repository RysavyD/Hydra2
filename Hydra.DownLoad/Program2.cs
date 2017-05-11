using System;
using System.Diagnostics;
using Hydra2.DownLoaders;

namespace Hydra.DownLoad
{
    class Program2
    {
        static void Main()
        {
            string url = "http://app.pod.cz/portal/SaP/cz/PC/Mereni.aspx?id=300021282&oid=2";
            Stopwatch sw = new Stopwatch();
            sw.Start();

            for (int i = 1; i <= 5; i++)
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
                catch
                {
                    Console.WriteLine("{0} - {1} - error", i, downloader.GetType());
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
