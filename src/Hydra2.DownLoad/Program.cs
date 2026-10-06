// Diagnostický nástroj pro stahování dat ze stanic.
// Na vstupu ID stanice (načte Link + DownLoadType z DB) nebo URL stránky (typ parseru se zvolí ručně).
// Použití: Hydra2.DownLoad [ID|URL] – bez argumentu se zeptá interaktivně (opakovaně, prázdný vstup = konec).

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Hydra2.DownLoad;
using Hydra2.Downloaders;
using Hydra2.Service;
using Hydra2.Service.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

Console.OutputEncoding = Encoding.UTF8;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddUserSecrets(typeof(Program).Assembly, optional: true)
    .AddEnvironmentVariables()
    .Build();

var services = new ServiceCollection();
services.AddLogging(builder =>
{
    builder.AddConfiguration(configuration.GetSection("Logging"));
    builder.AddProvider(new SyncConsoleLoggerProvider());
});
services.AddHydra2Services(configuration);
services.AddHydra2Downloaders();

await using var provider = services.BuildServiceProvider();
var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Hydra2.DownLoad");

var inputs = new Queue<string>(args);
while (true)
{
    string? input;
    if (args.Length > 0)
    {
        if (inputs.Count == 0) break;
        input = inputs.Dequeue();
    }
    else
    {
        Console.WriteLine();
        Console.Write("Zadej ID stanice nebo URL stránky (prázdné = konec): ");
        input = Console.ReadLine();
    }

    input = input?.Trim();
    if (string.IsNullOrEmpty(input)) break;

    try
    {
        await RunAsync(input);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Neocekavana chyba pri zpracovani vstupu {Input}", input);
    }
}

async Task RunAsync(string input)
{
    string link;
    int downLoadType;

    if (int.TryParse(input, out var stationId))
    {
        using var scope = provider.CreateScope();
        var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();

        logger.LogInformation("Nacitam stanici {StationId} z DB", stationId);
        var station = await dataService.GetStationAsync(stationId);
        if (station is null)
        {
            logger.LogError("Stanice {StationId} v DB neexistuje", stationId);
            return;
        }

        logger.LogInformation("Stanice {StationId}: {Spot}, DownLoadType={DownLoadType} ({Source}), Link={Link}",
            station.Id, station.Spot, station.DownLoadType, SourceCatalog.NameFor(station.DownLoadType), station.Link);

        if (string.IsNullOrWhiteSpace(station.Link))
        {
            logger.LogError("Stanice {StationId} nema vyplneny Link", stationId);
            return;
        }

        link = station.Link.Trim();
        downLoadType = station.DownLoadType;
    }
    else if (Uri.TryCreate(input, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
    {
        link = input;
        var chosen = AskDownLoadType(GuessDownLoadType(uri));
        if (chosen is null) return;
        downLoadType = chosen.Value;
    }
    else
    {
        logger.LogError("Vstup {Input} neni ani ciselne ID stanice, ani http(s) URL", input);
        return;
    }

    var downloader = provider.GetRequiredService<IDownloaderFactory>().GetDownloader(downLoadType);
    if (downloader is null)
    {
        logger.LogError("Pro DownLoadType={DownLoadType} neexistuje downloader", downLoadType);
        return;
    }

    logger.LogInformation("Stahuji {Link} pomoci {Downloader}", link, downloader.GetType().Name);
    var sw = Stopwatch.StartNew();
    IList<SpotRecord> records;
    try
    {
        records = await downloader.GetRecordsAsync(link);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Stazeni/parsovani selhalo po {Elapsed} ms", sw.ElapsedMilliseconds);
        await DumpRawPageAsync(link);
        return;
    }

    PrintRecords(records);
    logger.LogInformation("Hotovo za {Elapsed} ms, zaznamu: {Count}", sw.ElapsedMilliseconds, records.Count);

    if (records.Count == 0)
    {
        logger.LogWarning("Nenacetl se zadny zaznam - ukladam surovou stranku pro kontrolu");
        await DumpRawPageAsync(link);
    }
}

int? AskDownLoadType(int? guess)
{
    Console.WriteLine("Typ zdroje (parser):");
    foreach (var source in SourceCatalog.All)
        Console.WriteLine($"  {source.DownLoadType} = {source.DisplayName} ({source.Name})");

    Console.Write(guess is null ? "Vyber typ: " : $"Vyber typ [{guess}]: ");
    var answer = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(answer)) return guess;

    if (int.TryParse(answer, out var type) && SourceCatalog.FindByDownLoadType(type) is not null)
        return type;
    if (SourceCatalog.FindByName(answer) is { } byName)
        return byName.DownLoadType;

    logger.LogError("Neznamy typ zdroje {Answer}", answer);
    return null;
}

static int? GuessDownLoadType(Uri uri)
{
    var host = uri.Host.ToLowerInvariant();
    var nadrze = uri.AbsoluteUri.Contains("nadr", StringComparison.OrdinalIgnoreCase);

    if (host.Contains("chmi")) return 1;
    if (host.Contains("pvl") || host.Contains("poh")) return nadrze ? 3 : 2;
    if (host.Contains("pla")) return 4;
    if (host.Contains("pmo")) return nadrze ? 5 : 6;
    return null;
}

static void PrintRecords(IList<SpotRecord> records)
{
    Console.WriteLine();
    Console.WriteLine($"{"Čas",-20}{"Hladina",12}{"Průtok",12}{"Teplota",12}");
    Console.WriteLine(new string('-', 56));
    foreach (var r in records)
        Console.WriteLine($"{r.TimeStamp,-20:yyyy-MM-dd HH:mm}{Fmt(r.Level),12}{Fmt(r.Flow),12}{Fmt(r.Temperature),12}");
    Console.WriteLine();

    static string Fmt(float? value) => value?.ToString("0.###") ?? "-";
}

// Stáhne stránku znovu "natvrdo" (bez parsování) a uloží ji na disk, aby šlo zjistit, co server skutečně vrátil.
async Task DumpRawPageAsync(string link)
{
    try
    {
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("raw");
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Hydra2/2.0 (+https://hydra2.dusanrysavy.cz)");

        using var response = await client.GetAsync(link);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var html = Encoding.UTF8.GetString(bytes);
        var title = Regex.Match(html, @"<title[^>]*>\s*(.*?)\s*</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline).Groups[1].Value;

        var dir = Path.Combine(Environment.CurrentDirectory, "dumps");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"page-{DateTime.Now:yyyyMMdd-HHmmss}.html");
        await File.WriteAllBytesAsync(file, bytes);

        logger.LogInformation(
            "Surova odpoved: HTTP {Status} {Reason}, Content-Type={ContentType}, {Length} B, final URL={FinalUrl}, title=\"{Title}\"",
            (int)response.StatusCode, response.ReasonPhrase, response.Content.Headers.ContentType,
            bytes.Length, response.RequestMessage?.RequestUri, title);
        logger.LogInformation("Stranka ulozena do {File}", file);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Nepodarilo se stahnout ani surovou stranku");
    }
}
