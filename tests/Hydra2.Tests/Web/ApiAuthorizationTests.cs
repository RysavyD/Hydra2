using System.Net;

namespace Hydra2.Tests.Web;

public class ApiAuthorizationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ApiAuthorizationTests(TestWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/api/admin/logs/level")]
    [InlineData("/api/admin/failing-stations")]
    [InlineData("/api/jobs/status")]
    public async Task Admin_endpoints_require_token(string path)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(path); // no token query

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/api/admin/logs/level?token=wrong")]
    [InlineData("/api/admin/failing-stations?token=wrong")]
    [InlineData("/api/jobs/status?token=wrong")]
    public async Task Admin_endpoints_reject_wrong_token(string path)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Trigger_endpoint_rejects_unknown_source()
    {
        // Auth must pass first - use the configured test token
        var client = _factory.CreateClient();
        var token = GetTokenFromConfig();
        var response = await client.PostAsync($"/api/jobs/trigger/totally-unknown?token={token}", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private string GetTokenFromConfig()
    {
        var configuration = (Microsoft.Extensions.Configuration.IConfiguration)
            _factory.Services.GetService(typeof(Microsoft.Extensions.Configuration.IConfiguration))!;
        return configuration["Auth:SecretToken"] ?? "REPLACE_ME_DO_NOT_USE_IN_PRODUCTION";
    }
}
