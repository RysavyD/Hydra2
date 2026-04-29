using System.Diagnostics;
using Hydra2.Downloaders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddConsole());
services.AddHydra2Downloaders();

await using var provider = services.BuildServiceProvider();

var factory = provider.GetRequiredService<IDownloaderFactory>();
var sw = Stopwatch.StartNew();

var url = "http://sap.poh.cz/portal/Nadrze/cz/PC/Mereni.aspx?id=2091&oid=2";
var downloader = factory.GetDownloader(3);
if (downloader is null)
{
    Console.WriteLine("Downloader for type 3 not registered.");
    return;
}

var samples = await downloader.GetRecordsAsync(url);
foreach (var sample in samples)
{
    Console.WriteLine("{0} - {1} - {2} - {3}",
        sample.TimeStamp, sample.Level, sample.Flow, sample.Temperature);
}

Console.WriteLine("------------------------------------");
Console.WriteLine($"Elapsed: {sw.ElapsedMilliseconds} ms, samples: {samples.Count}");
Console.WriteLine("Done");
