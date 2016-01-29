using System.Linq;

namespace Hydra2.DownLoaders
{
    public class Chmi : BaseDownloader
    {
        public Chmi() : base("")
        {
            DecimalSeparator = ".";
        }

        protected override void SetTable()
        {
            var div = Document.DocumentNode.Descendants("div")
                           .First(t => t.Attributes
                               .Any(a => a.Name == "class" && a.Value == "tborder center_text"));

            Table = div.Descendants("table").First();
        }
    }
}