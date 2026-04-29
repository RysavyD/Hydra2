namespace Hydra2.Web.Models;

public class SpotOverviewViewModel
{
    public int Id { get; set; }
    public string RiverName { get; set; } = string.Empty;
    public string SpotName { get; set; } = string.Empty;
    public int SpotType { get; set; }
    public string? Url { get; set; }
    public DateTime? LastSample { get; set; }
}
