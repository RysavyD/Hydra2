using System;
using System.Collections.Generic;

namespace Hydra2.DownLoaders
{
    public interface ISpotInformationDownLoader
    {
        IList<SpotRecord> GetRecords(string link);
    }

    public class SpotRecord
    {
        public DateTime TimeStamp { get; set; }
        public float? Level { get; set; }
        public float? Flow { get; set; }
        public float? Temperature { get; set; }
    }
}
