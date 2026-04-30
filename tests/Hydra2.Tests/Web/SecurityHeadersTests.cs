namespace Hydra2.Tests.Web;

public class SecurityHeadersTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SecurityHeadersTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Public_response_has_security_headers()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/");

        response.Headers.Should().ContainKey("X-Content-Type-Options")
            .WhoseValue.Should().ContainSingle("nosniff");
        response.Headers.Should().ContainKey("X-Frame-Options")
            .WhoseValue.Should().ContainSingle("SAMEORIGIN");
        response.Headers.Should().ContainKey("Referrer-Policy");
        response.Headers.Should().ContainKey("Content-Security-Policy");
    }

    [Fact]
    public async Task Csp_allows_amcharts_cdn_and_self()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/");

        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        csp.Should().Contain("default-src 'self'");
        csp.Should().Contain("https://cdn.amcharts.com");
        csp.Should().Contain("frame-ancestors 'none'");
    }

    [Fact]
    public async Task Static_files_under_lib_have_immutable_cache()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/lib/jquery/jquery.min.js");

        response.IsSuccessStatusCode.Should().BeTrue();
        response.Headers.CacheControl!.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge!.Value.Should().Be(TimeSpan.FromDays(365));
    }

    [Fact]
    public async Task Static_files_under_css_have_shorter_cache()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/css/site.css");

        response.IsSuccessStatusCode.Should().BeTrue();
        response.Headers.CacheControl!.MaxAge!.Value.Should().Be(TimeSpan.FromHours(1));
    }
}
