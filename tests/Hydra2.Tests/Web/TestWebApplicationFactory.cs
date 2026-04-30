using Hydra2.Downloaders;
using Hydra2.Service;
using Hydra2.Service.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Hydra2.Tests.Web;

/// <summary>
/// Replaces real database access and Quartz with stubs, so the in-memory test
/// app starts without external dependencies. Tests can resolve the substituted
/// services from <see cref="Services"/>.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public IDataService DataService { get; } = Substitute.For<IDataService>();
    public IConfigService ConfigService { get; } = Substitute.For<IConfigService>();
    public IAdminService AdminService { get; } = Substitute.For<IAdminService>();
    public IUpdateService UpdateService { get; } = Substitute.For<IUpdateService>();

    public TestWebApplicationFactory()
    {
        // Sane default: ConfigService returns a stable Config so heartbeat sees DB healthy.
        ConfigService.GetFirstConfigAsync(Arg.Any<CancellationToken>())
            .Returns(new Config { Id = 1, Key = "current", Value = 0 });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Disable scheduled jobs - tests trigger explicitly.
            RemoveAll(services, typeof(IDataService));
            RemoveAll(services, typeof(IConfigService));
            RemoveAll(services, typeof(IAdminService));
            RemoveAll(services, typeof(IUpdateService));

            services.AddSingleton(DataService);
            services.AddSingleton(ConfigService);
            services.AddSingleton(AdminService);
            services.AddSingleton(UpdateService);
        });
    }

    private static void RemoveAll(IServiceCollection services, Type serviceType)
    {
        var descriptors = services.Where(d => d.ServiceType == serviceType).ToList();
        foreach (var d in descriptors) services.Remove(d);
    }
}
