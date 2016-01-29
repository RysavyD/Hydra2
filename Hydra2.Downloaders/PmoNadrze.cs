using System.Linq;

namespace Hydra2.DownLoaders
{
    public class PmoNadrze : BaseDownloader
    {
        public PmoNadrze():base("")
        { }

        protected override void SetTable()
        {
            Table = Document.DocumentNode.Descendants("table")
               .First(t => t.Attributes
                   .Any(a => a.Name == "width" && a.Value == "300"));
        }
    }
}
