using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hydra2.DownLoaders;

namespace Hydra.DownLoad
{
    class Program
    {
        static void Main(string[] args)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            var dict = new Dictionary<string, ISpotInformationDownLoader>();
            var list = new List<int>();
            //dict.Add("http://hydro.chmi.cz/hpps/hpps_prfdata.php?seq=10045030", new Chmi());
            //dict.Add("http://www.pvl.cz/portal/SaP/cz/pc/Mereni.aspx?id=SVCK&oid=1", new Pvl());
            //dict.Add("http://www.pvl.cz/portal/Nadrze/cz/PC/Mereni.aspx?Id=KCKC&oid=3", new PvlNadrze());
            //dict.Add("http://www.pla.cz/portal/nadrze/cz/pc/Mereni.aspx?id=101&oid=1", new PlaNadrze());
            //dict.Add("http://www.pmo.cz/portal/nadrze/cz/mereni_10.htm", new PmoNadrze());
            //dict.Add("http://sap.poh.cz/portal/Nadrze/cz/PC/Mereni.aspx?id=1021&oid=1", new PvlNadrze());
            //dict.Add("http://www.pla.cz/portal/SaP/cz/PC/Mereni.aspx?id=179&oid=1", new Pvl());
            //dict.Add("http://www.pod.cz/portal/nadrze/cz/mereni_06.htm", new PmoNadrze());
            dict.Add("http://sap.poh.cz/portal/Nadrze/cz/PC/Mereni.aspx?id=2091&oid=2", new PvlNadrze());

            foreach (var station in dict)
            {
                var samples = station.Value.GetRecords(station.Key);
                list.Add(samples.Count);

                foreach (var spotInfo in samples)
                {
                    Console.WriteLine("{0} - {1} - {2} - {3}",
                        spotInfo.TimeStamp,
                        spotInfo.Level,
                        spotInfo.Flow,
                        spotInfo.Temperature);
                }

                Console.WriteLine("------------------------------------");
            }

            sw.Stop();
            Console.WriteLine(sw.ElapsedMilliseconds);
            foreach (var count in list)
            {
                Console.WriteLine(count);
            }

            Console.WriteLine();
            Console.WriteLine("Done");
            Console.ReadKey();
        }
    }
}
