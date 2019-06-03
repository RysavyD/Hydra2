using System;

namespace Hydra2.Web.Models
{
    public class SpotOverviewViewModel
    {
        public string RiverName { get; set; }
        public string SpotName { get; set; }
        public int SpotType { get; set; }
        public string Url { get; set; }
        public DateTime? LastSample { get; set; }
    }
}