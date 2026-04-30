using System.Globalization;
using Hydra2.Downloaders;
using Hydra2.Service;
using Hydra2.Web;
using Hydra2.Web.Scheduler;
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
builder.Services.Configure<SchedulerOptions>(builder.Configuration.GetSection(SchedulerOptions.SectionName));

builder.Services.AddHydra2Services(builder.Configuration);
builder.Services.AddHydra2Downloaders();

builder.Services.AddSingleton<SchedulerStateTracker>();
builder.Services.AddSingleton<IUpdateProgressListener, TrackerProgressListener>();

builder.Services.AddControllersWithViews();

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
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
