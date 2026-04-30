using System.Net;
using System.Text.Json;
using Hydra2.Service.Data;
using NSubstitute;

namespace Hydra2.Tests.Web;

public class HeartbeatTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public HeartbeatTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Heartbeat_returns_200_when_db_healthy()
    {
        _factory.ConfigService.GetFirstConfigAsync(Arg.Any<CancellationToken>())
            .Returns(new Config { Id = 1, Key = "current", Value = 5 });

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/heartbeat");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        // Status is either OK or STARTING (no source has run yet in a fresh test app).
        var status = doc.RootElement.GetProperty("status").GetString();
        status.Should().BeOneOf("OK", "STARTING");
        doc.RootElement.GetProperty("db").GetString().Should().Be("OK");
    }

    [Fact]
    public async Task Heartbeat_returns_503_when_db_unavailable()
    {
        _factory.ConfigService.GetFirstConfigAsync(Arg.Any<CancellationToken>())
            .Returns<Task<Config>>(_ => throw new InvalidOperationException("DB down"));

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/heartbeat");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("status").GetString().Should().Be("DEGRADED");
        doc.RootElement.GetProperty("db").GetString().Should().Be("ERROR");
    }

    [Fact]
    public async Task Heartbeat_includes_all_known_sources()
    {
        _factory.ConfigService.GetFirstConfigAsync(Arg.Any<CancellationToken>())
            .Returns(new Config { Id = 1, Key = "current", Value = 0 });

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/heartbeat");
        var body = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(body);
        var sources = doc.RootElement.GetProperty("sources");
        sources.GetArrayLength().Should().Be(6);
    }
}
