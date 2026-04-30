namespace Hydra2.Tests.Web;

public class WebOptimizerBundleTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public WebOptimizerBundleTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Css_bundle_is_served_with_correct_content_type()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/css/site.bundle.css");

        response.IsSuccessStatusCode.Should().BeTrue($"GET /css/site.bundle.css returned {(int)response.StatusCode}");
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/css");
    }

    [Fact]
    public async Task Css_bundle_contains_bootstrap_and_site_css()
    {
        var client = _factory.CreateClient();
        var body = await client.GetStringAsync("/css/site.bundle.css");

        // bootstrap signature
        body.Should().Contain("Bootstrap", "the bundle should include bootstrap.min.css");
        // own site.css signature
        body.Should().Contain(".chart-container", "the bundle should include site.css with chart-container class");
    }

    [Fact]
    public async Task Js_bundle_is_served_with_correct_content_type()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/js/site.bundle.js");

        response.IsSuccessStatusCode.Should().BeTrue($"GET /js/site.bundle.js returned {(int)response.StatusCode}");
        response.Content.Headers.ContentType!.MediaType.Should().BeOneOf("application/javascript", "text/javascript");
    }

    [Fact]
    public async Task Js_bundle_contains_jquery_and_bootbox_and_hydra2()
    {
        var client = _factory.CreateClient();
        var body = await client.GetStringAsync("/js/site.bundle.js");

        body.Should().Contain("jQuery", "the bundle should include jQuery");
        body.Should().Contain("v3.7", "the bundle should include jQuery 3.7+");
        body.Should().Contain("bootbox", "the bundle should include bootbox");
        body.Should().Contain("ShowWaitDialog", "the bundle should include Hydra2.js (ShowWaitDialog function)");
    }
}
