using System.Linq;

namespace Hydra2.DownLoaders
{
    public class PmoToky : BaseDownloader
    {
        public PmoToky():base("")
        { }

        protected override void SetTable()
        {
            var nodes = Document.DocumentNode.SelectNodes(@"//*/table");

            var table = nodes.Where(x => x.Attributes["bordercolor"]?.Value == "gray");
            Table = table.Skip(1).FirstOrDefault();
        }
    }
}
