using System;

namespace Hydra2.DownLoaders
{
    public class PvlNadrze : BaseDownloader
    {
        public PvlNadrze() : base("dataMereni24hGV")
        {
            DecimalSeparator = ",";
        }

        protected override void ModifyPage()
        {
            int idPosition = Page.IndexOf(Id, StringComparison.Ordinal);
            int start = Page.Substring(0, idPosition).LastIndexOf("<table", StringComparison.Ordinal);
            int stop = Page.IndexOf("/table", start, StringComparison.Ordinal);

            var tableString = Page.Substring(start, stop - start + 8);
            Page = PutTableInHtml(tableString);
        }
    }
}
