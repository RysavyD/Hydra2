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
    public async Task Css_bundle_contains_bootstrap5_bootstrap_icons_and_site_css()
    {
        var client = _factory.CreateClient();
        var body = await client.GetStringAsync("/css/site.bundle.css");

        // Bootstrap 5 signature
        body.Should().Contain("Bootstrap", "the bundle should include Bootstrap 5 CSS");
        // Bootstrap Icons signature
        body.Should().Contain("bootstrap-icons", "the bundle should include bootstrap-icons CSS");
        // site.css signature
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
    public async Task Js_bundle_contains_bootstrap5_and_hydra2_but_not_jquery()
    {
        var client = _factory.CreateClient();
        var body = await client.GetStringAsync("/js/site.bundle.js");

        // Bootstrap 5 bundle includes Popper
        body.Should().Contain("Popper", "the bundle should include Bootstrap 5 bundle (with Popper)");
        // Hydra2.js is included
        body.Should().Contain("hydra2-theme", "the bundle should include Hydra2.js (theme toggle)");
        // jQuery library must NOT be present (Bootstrap 5 has jQueryInterface for back-compat,
        // but the actual jQuery library identifies itself with "jQuery.fn.jquery").
        body.Should().NotContain("jQuery.fn.jquery", "jQuery library was dropped in Phase 11");
        body.Should().NotContain("bootbox", "bootbox was dropped in Phase 11");
    }
}
