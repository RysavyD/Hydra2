using System.Net;
using System.Text;
using Hydra2.Downloaders;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hydra2.Tests.Downloaders;

public class BaseDownloaderTests
{
    [Fact]
    public async Task Parses_well_formed_table_with_comma_decimal()
    {
        const string html = """
            <html><body>
            <table id="testTable">
              <tr><th>Time</th><th>Level</th><th>Flow</th><th>Temp</th></tr>
              <tr><td>2026-04-30 10:00</td><td>120</td><td>5,5</td><td>12,3</td></tr>
              <tr><td>2026-04-30 11:00</td><td>122</td><td>5,7</td><td>12,5</td></tr>
            </table>
            </body></html>
            """;

        var scraper = MakeScraper(html, decimalSeparator: ",");

        var records = await scraper.GetRecordsAsync("http://test/");

        records.Should().HaveCount(2);
        records[0].TimeStamp.Should().Be(new DateTime(2026, 4, 30, 10, 0, 0));
        records[0].Level.Should().Be(120f);
        records[0].Flow.Should().BeApproximately(5.5f, 0.001f);
        records[0].Temperature.Should().BeApproximately(12.3f, 0.001f);
    }

    [Fact]
    public async Task Skips_rows_with_non_zero_minute()
    {
        const string html = """
            <html><body>
            <table id="testTable">
              <tr><th>h</th><th>l</th><th>f</th></tr>
              <tr><td>2026-04-30 10:00</td><td>10</td><td>1</td></tr>
              <tr><td>2026-04-30 10:15</td><td>11</td><td>1.1</td></tr>
              <tr><td>2026-04-30 10:30</td><td>12</td><td>1.2</td></tr>
              <tr><td>2026-04-30 11:00</td><td>13</td><td>1.3</td></tr>
            </table>
            </body></html>
            """;

        var scraper = MakeScraper(html, decimalSeparator: ".");
        var records = await scraper.GetRecordsAsync("http://test/");

        records.Should().HaveCount(2);
        records.Select(r => r.TimeStamp.Hour).Should().Equal(10, 11);
    }

    [Fact]
    public async Task Empty_value_cells_become_null()
    {
        const string html = """
            <html><body>
            <table id="testTable">
              <tr><th>h</th><th>l</th><th>f</th></tr>
              <tr><td>2026-04-30 10:00</td><td></td><td>5,5</td></tr>
            </table>
            </body></html>
            """;

        var scraper = MakeScraper(html, decimalSeparator: ",");
        var records = await scraper.GetRecordsAsync("http://test/");

        records.Should().HaveCount(1);
        records[0].Level.Should().BeNull();
        records[0].Flow.Should().BeApproximately(5.5f, 0.001f);
    }

    [Fact]
    public async Task Missing_temperature_column_is_handled()
    {
        const string html = """
            <html><body>
            <table id="testTable">
              <tr><th>h</th><th>l</th><th>f</th></tr>
              <tr><td>2026-04-30 10:00</td><td>10</td><td>1.5</td></tr>
            </table>
            </body></html>
            """;

        var scraper = MakeScraper(html, decimalSeparator: ".");
        var records = await scraper.GetRecordsAsync("http://test/");

        records.Should().HaveCount(1);
        records[0].Temperature.Should().BeNull();
    }

    [Fact]
    public async Task Unparseable_datetime_skips_row_without_throwing()
    {
        const string html = """
            <html><body>
            <table id="testTable">
              <tr><th>h</th><th>l</th><th>f</th></tr>
              <tr><td>not-a-date</td><td>10</td><td>1</td></tr>
              <tr><td>2026-04-30 10:00</td><td>20</td><td>2</td></tr>
            </table>
            </body></html>
            """;

        var scraper = MakeScraper(html, decimalSeparator: ".");
        var records = await scraper.GetRecordsAsync("http://test/");

        records.Should().HaveCount(1);
        records[0].Level.Should().Be(20f);
    }

    private static TestScraper MakeScraper(string html, string decimalSeparator)
    {
        var client = new HttpClient(new StaticHandler(html));
        return new TestScraper(client, decimalSeparator);
    }

    private sealed class TestScraper : BaseDownloader
    {
        public TestScraper(HttpClient client, string decimalSeparator)
            : base(client, NullLogger<TestScraper>.Instance, "testTable")
        {
            DecimalSeparator = decimalSeparator;
        }
    }

    private sealed class StaticHandler : HttpMessageHandler
    {
        private readonly string _html;
        public StaticHandler(string html) => _html = html;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_html, Encoding.UTF8, "text/html"),
            });
    }
}
