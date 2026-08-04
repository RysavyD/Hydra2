namespace Hydra2.Tests.Web;

public class PwaAndThemeTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public PwaAndThemeTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Manifest_endpoint_serves_valid_json()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/manifest.webmanifest");

        response.IsSuccessStatusCode.Should().BeTrue();
        response.Content.Headers.ContentType!.MediaType.Should().BeOneOf(
            "application/manifest+json", "application/json", "text/plain");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"name\"");
        body.Should().Contain("Hydra");
        body.Should().Contain("\"start_url\"");
        body.Should().Contain("\"icons\"");
    }

    [Fact]
    public async Task Svg_icon_is_served()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/icons/icon.svg");

        response.IsSuccessStatusCode.Should().BeTrue();
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/svg+xml");
    }

    [Fact]
    public async Task Theme_init_script_is_served()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/js/theme-init.js");

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("hydra2-theme", "theme-init must read the same localStorage key");
        // Phase 11: Bootstrap 5 native dark mode via data-bs-theme (replaced custom data-theme).
        body.Should().Contain("data-bs-theme", "theme-init must set Bootstrap 5 data-bs-theme attribute");
    }

    [Fact]
    public async Task Css_bundle_contains_dark_mode_tokens()
    {
        var client = _factory.CreateClient();
        var body = await client.GetStringAsync("/css/site.bundle.css");

        // Phase 11: Bootstrap 5 native dark mode — no custom --bg vars, uses --bs-* tokens.
        // Auto-mode (prefers-color-scheme) is now handled in theme-init.js, not in CSS.
        body.Should().Contain("--bs-body-bg", "Bootstrap 5 CSS must define its body background token");
        body.Should().Contain("data-bs-theme", "Bootstrap 5 native dark mode selector must be present");
    }

    [Fact]
    public async Task Layout_includes_manifest_link()
    {
        var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/");

        html.Should().Contain("rel=\"manifest\"");
        html.Should().Contain("manifest.webmanifest");
        html.Should().Contain("theme-toggle", "navbar must include the theme toggle button");
    }
}
