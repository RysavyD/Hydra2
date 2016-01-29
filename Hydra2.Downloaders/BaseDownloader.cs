using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using HtmlAgilityPack;
using Hydra2.Model;
using NLog;

namespace Hydra2.DownLoaders
{
    public class BaseDownloader : ISpotInformationDownLoader
    {
        protected string Page;
        protected string Id;
        protected string Link;
        protected HtmlDocument Document;
        protected HtmlNode Table;
        private NumberFormatInfo _nfi;
        protected string DecimalSeparator = ",";

        public BaseDownloader(string id)
        {
            Id = id;
        }

        public virtual IList<SpotRecord> GetRecords(string link)
        {
            NLogger.Log(LogLevel.Info, "Stahuji ze stránky: " + link);
            
            Link = link;
            var result = new List<SpotRecord>();

            DownLoadPage();
            ModifyPage();
            LoadDocument();
            SetTable();
            SetDecimalSeparator();
            LoadData(result);

            NLogger.Log(LogLevel.Info, "Vzorků nalezeno: " + result.Count);
            return result;
        }

        protected string PutTableInHtml(string table)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head></head>");
            sb.AppendLine("<body>");
            sb.AppendLine(table);
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        protected virtual void DownLoadPage()
        {
            using (var client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                Page = client.DownloadString(Link);
            }
        }

        protected virtual void ModifyPage()
        {
            
        }

        protected virtual void LoadDocument()
        {
            Document = new HtmlDocument();
            Document.LoadHtml(Page);
        }

        protected virtual void SetTable()
        {
            Table = Document.GetElementbyId(Id);
        }

        protected virtual void SetDecimalSeparator()
        {
            _nfi = new NumberFormatInfo()
            {
                NumberDecimalSeparator = DecimalSeparator
            };
        }

        protected virtual void LoadData(IList<SpotRecord> result)
        {
            foreach (var row in Table.Descendants("tr").Skip(1))
            {
                var tds = row.Descendants("td").Select(td => WebUtility.HtmlDecode(td.InnerText).Trim()).ToArray();
                if (!string.IsNullOrWhiteSpace(tds[0]))
                {
                    DateTime dt;
                    if (DateTime.TryParse(tds[0], out dt))
                    {
                        if (dt.Minute == 0)
                        {
                            var record = new SpotRecord()
                            {
                                TimeStamp = dt,
                                Level =
                                    (string.IsNullOrWhiteSpace(tds[1])) ? (float?) null : Convert.ToSingle(tds[1], _nfi),
                                Flow =
                                    (string.IsNullOrWhiteSpace(tds[2])) ? (float?) null : Convert.ToSingle(tds[2], _nfi),
                            };

                            if (tds.Length > 3)
                            {
                                record.Temperature = (string.IsNullOrWhiteSpace(tds[3]))
                                    ? (float?) null
                                    : Convert.ToSingle(tds[3], _nfi);
                            }

                            result.Add(record);
                        }
                    }
                }
            }
        }
    }
}
