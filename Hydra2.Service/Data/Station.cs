
namespace Hydra2.Service.Data
{
    public class Station
    {
        public int Id { get; set; }
        public string Spot { get; set; }
        public int Spa_val { get; set; }
        public float? Spa0 { get; set; }
        public float? Spa1 { get; set; }
        public float? Spa2 { get; set; }
        public float? Spa3 { get; set; }
        public float? Spa3e { get; set; }
        public int Type { get; set; }
        public string Link { get; set; }
        public int Id_River { get; set; }
        public int DownLoadType { get; set; }
        public string RaftLink { get; set; }
    }
}
