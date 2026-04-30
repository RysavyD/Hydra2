namespace Hydra2.Tests.Web;

public class HomeControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public HomeControllerTests(TestWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/")]
    [InlineData("/Home/About")]
    [InlineData("/Home/Contact")]
    public async Task Public_pages_return_200(string path)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(path);

        response.IsSuccessStatusCode.Should().BeTrue($"GET {path} should succeed but got {(int)response.StatusCode}");
    }
}
