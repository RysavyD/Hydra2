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
        body.Should().Contain("data-theme", "theme-init must set data-theme attribute");
    }

    [Fact]
    public async Task Css_bundle_contains_dark_mode_tokens()
    {
        var client = _factory.CreateClient();
        var body = await client.GetStringAsync("/css/site.bundle.css");

        body.Should().Contain("--bg", "site.css must define CSS custom properties for theming");
        body.Should().Contain("data-theme=\"dark\"", "dark theme override must be present");
        // CSS minifier strips the space after the colon ("prefers-color-scheme:dark").
        body.Should().Contain("prefers-color-scheme:", "system preference media query must be present");
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
