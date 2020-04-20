using System;

namespace Hydra2.Service.Model
{
    public class SpotRecord
    {
        public DateTime TimeStamp { get; set; }
        public float? Level { get; set; }
        public float? Flow { get; set; }
        public float? Temperature { get; set; }
    }
}
