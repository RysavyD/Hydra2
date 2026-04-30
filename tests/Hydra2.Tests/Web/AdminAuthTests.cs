using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Hydra2.Tests.Web;

public class AdminAuthTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AdminAuthTests(TestWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/Adm")]
    [InlineData("/Adm/")]
    [InlineData("/Adm/SpotOverView")]
    [InlineData("/Adm/HandUpdate")]
    public async Task Admin_paths_require_basic_auth(string path)
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.Should().NotBeEmpty();
        response.Headers.WwwAuthenticate.First().Scheme.Should().Be("Basic");
    }

    [Fact]
    public async Task Admin_with_wrong_password_returns_401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = MakeBasic(TestWebApplicationFactory.TestAdminUsername, "wrong");

        var response = await client.GetAsync("/Adm");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admin_with_correct_credentials_passes_auth()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = MakeBasic(
            TestWebApplicationFactory.TestAdminUsername,
            TestWebApplicationFactory.TestAdminPassword);

        var response = await client.GetAsync("/Adm");

        // Auth passes -> reaches controller. Controller may 200 or 500 depending on
        // service stubs; the relevant assertion is that auth did not block us.
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Public_paths_do_not_require_auth()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Heartbeat_does_not_require_auth()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/heartbeat");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    private static AuthenticationHeaderValue MakeBasic(string user, string pass)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}"));
        return new AuthenticationHeaderValue("Basic", credentials);
    }
}
