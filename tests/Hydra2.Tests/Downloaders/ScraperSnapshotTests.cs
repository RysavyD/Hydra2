using System.Net;
using System.Text;
using Hydra2.Downloaders;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hydra2.Tests.Downloaders;

/// <summary>
/// Snapshot/characterization tests for individual scrapers using fixture HTML files.
/// If a source website changes layout, the corresponding fixture should be re-saved
/// from a real response and these tests updated to match.
/// </summary>
public class ScraperSnapshotTests
{
    [Fact]
    public async Task Chmi_parses_fixture_correctly()
    {
        var html = await ReadFixture("chmi-station.html");
        var scraper = new Chmi(MakeClient(html), NullLogger<Chmi>.Instance);

        var records = await scraper.GetRecordsAsync("http://test/");

        // Fixture has 22:00, 22:15, 22:30, 22:45, 23:00, 23:15, 23:30, 23:45, 00:00.
        // Only minute=0 entries are kept: 22:00, 23:00, 00:00 = 3 records.
        records.Should().HaveCount(3);
        records.Should().Contain(r => r.TimeStamp == new DateTime(2026, 4, 29, 22, 0, 0)
                                      && r.Level == 118f
                                      && r.Flow!.Value == 5.42f);
        records.Should().Contain(r => r.TimeStamp == new DateTime(2026, 4, 29, 23, 0, 0));
        records.Should().Contain(r => r.TimeStamp == new DateTime(2026, 4, 30, 0, 0, 0));
    }

    [Fact]
    public async Task Chmi_uses_dot_decimal_separator()
    {
        var html = await ReadFixture("chmi-station.html");
        var scraper = new Chmi(MakeClient(html), NullLogger<Chmi>.Instance);

        var records = await scraper.GetRecordsAsync("http://test/");
        var first = records.First();

        // CHMI dataset uses "." as decimal separator (e.g. "5.42")
        first.Flow.Should().BeApproximately(5.42f, 0.001f);
        first.Temperature.Should().BeApproximately(11.2f, 0.001f);
    }

    [Fact]
    public async Task Pvl_parses_fixture_correctly()
    {
        var html = await ReadFixture("pvl-mereni.html");
        var scraper = new Pvl(MakeClient(html), NullLogger<Pvl>.Instance);

        var records = await scraper.GetRecordsAsync("http://test/");

        records.Should().HaveCount(3);
        // PVL uses "," as decimal separator
        records.First().Flow.Should().BeApproximately(3.21f, 0.001f);
        records.First().Temperature.Should().BeApproximately(10.5f, 0.001f);
    }

    [Fact]
    public async Task PmoNadrze_parses_table_with_width_300()
    {
        var html = await ReadFixture("pmoNadrze-mereni.html");
        var scraper = new PmoNadrze(MakeClient(html), NullLogger<PmoNadrze>.Instance);

        var records = await scraper.GetRecordsAsync("http://test/");

        records.Should().HaveCount(3);
        // First non-filler table (width=300), comma decimal sep
        records.First().Level.Should().BeApproximately(312.5f, 0.001f);
    }

    private static async Task<string> ReadFixture(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        return await File.ReadAllTextAsync(path);
    }

    private static HttpClient MakeClient(string html) => new(new StaticHandler(html));

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
