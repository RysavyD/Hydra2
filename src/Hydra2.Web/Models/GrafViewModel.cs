using Microsoft.AspNetCore.Mvc.Rendering;

namespace Hydra2.Web.Models;

public class GrafViewModel
{
    public List<SelectListItem> Rivers { get; set; } = new();
    public string StartDate { get; set; } = string.Empty;
    public string StopDate { get; set; } = string.Empty;
}
