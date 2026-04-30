using System.Globalization;
using Hydra2.Downloaders;
using Hydra2.Service;
using Hydra2.Web;
using Hydra2.Web.Middleware;
using Hydra2.Web.Scheduler;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Quartz;
using Serilog;
using Serilog.Core;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

var configuredLevelText = builder.Configuration["Serilog:MinimumLevel:Default"] ?? "Information";
var initialLevel = Enum.TryParse<LogEventLevel>(configuredLevelText, ignoreCase: true, out var lv)
    ? lv
    : LogEventLevel.Information;
var loggingLevelSwitch = new LoggingLevelSwitch(initialLevel);
builder.Services.AddSingleton(loggingLevelSwitch);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .MinimumLevel.ControlledBy(loggingLevelSwitch)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<AdminAuthOptions>(builder.Configuration.GetSection(AdminAuthOptions.SectionName));
builder.Services.Configure<SchedulerOptions>(builder.Configuration.GetSection(SchedulerOptions.SectionName));

builder.Services.AddHsts(opts =>
{
    opts.Preload = true;
    opts.IncludeSubDomains = true;
    opts.MaxAge = TimeSpan.FromDays(365);
});

builder.Services.AddHydra2Services(builder.Configuration);
builder.Services.AddHydra2Downloaders();

builder.Services.AddSingleton<SchedulerStateTracker>();
builder.Services.AddSingleton<IUpdateProgressListener, TrackerProgressListener>();

builder.Services.AddControllersWithViews();

builder.Services.AddWebOptimizer(pipeline =>
{
    pipeline.AddCssBundle("/css/site.bundle.css",
        "/lib/bootstrap/css/bootstrap.min.css",
        "/css/site.css");

    pipeline.AddJavaScriptBundle("/js/site.bundle.js",
        "/lib/jquery/jquery.min.js",
        "/lib/bootbox/bootbox.min.js",
        "/lib/bootstrap/js/bootstrap.min.js",
        "/js/Hydra2.js");
});

var schedulerOptions = builder.Configuration
    .GetSection(SchedulerOptions.SectionName)
    .Get<SchedulerOptions>() ?? new SchedulerOptions();

builder.Services.AddQuartz(q =>
{
    foreach (var (name, source) in schedulerOptions.Sources)
    {
        if (!source.Enabled || string.IsNullOrWhiteSpace(source.Cron)) continue;

        var jobKey = new JobKey($"source-{name}", "sources");
        q.AddJob<SourceUpdateJob>(opts => opts
            .WithIdentity(jobKey)
            .UsingJobData(SourceUpdateJob.DownLoadTypeKey, source.DownLoadType)
            .StoreDurably());

        q.AddTrigger(opts => opts
            .ForJob(jobKey)
            .WithIdentity($"trigger-{name}", "sources")
            .WithCronSchedule(source.Cron, cron =>
            {
                if (string.Equals(source.Misfire, "Ignore", StringComparison.OrdinalIgnoreCase))
                    cron.WithMisfireHandlingInstructionDoNothing();
                else
                    cron.WithMisfireHandlingInstructionFireAndProceed();
            }));
    }
});

builder.Services.AddQuartzHostedService(opts =>
{
    opts.WaitForJobsToComplete = true;
});

var app = builder.Build();

var supportedCultures = new[] { new CultureInfo("cs-CZ"), new CultureInfo("en-GB") };
app.UseRequestLocalization(new Microsoft.AspNetCore.Builder.RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("cs-CZ"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures,
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseSerilogRequestLogging();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseHttpsRedirection();
app.UseMiddleware<BasicAuthMiddleware>();

// WebOptimizer must run BEFORE UseStaticFiles so /css/site.bundle.css and
// /js/site.bundle.js are served by the optimizer (with content-hash cache key
// in the URL when ITagHelper produces it). Source files at /lib/, /css/site.css
// etc. continue to be served by UseStaticFiles below.
app.UseWebOptimizer();

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.Context.Request.Path.Value ?? "";
        // Third-party libraries are content-addressed by version in the path -> long cache.
        // Owned css/js gets a shorter cache so we can roll fixes without bumping URLs.
        if (path.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/fonts/", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        }
        else
        {
            ctx.Context.Response.Headers.CacheControl = "public,max-age=3600";
        }
    },
});

app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Make the implicit Program class accessible to WebApplicationFactory in tests.
public partial class Program;

