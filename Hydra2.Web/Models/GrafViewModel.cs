using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Hydra2.Web.Models
{
    public class GrafViewModel
    {
        public List<SelectListItem> Rivers { get; set; }

        public string StartDate { get; set; }
        public string StopDate { get; set; }
    }
}