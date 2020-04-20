using System;

namespace Hydra2.Service.Data.Admin
{
    public class SpotOverviewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Spot { get; set; }
        public int Type { get; set; }
        public string Link { get; set; }
        public DateTime? LastSample { get; set; }
    }
}
