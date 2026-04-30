namespace Hydra2.Downloaders;

/// <summary>
/// Maps the numeric Station.DownLoadType column to a stable, human-friendly name.
/// </summary>
public static class SourceCatalog
{
    public static readonly IReadOnlyList<SourceInfo> All = new[]
    {
        new SourceInfo("chmi",       1, "ČHMÚ"),
        new SourceInfo("pvl",        2, "PVL"),
        new SourceInfo("pvlNadrze",  3, "PVL nádrže"),
        new SourceInfo("plaNadrze",  4, "PLA nádrže"),
        new SourceInfo("pmoNadrze",  5, "PMO nádrže"),
        new SourceInfo("pmoToky",    6, "PMO toky"),
    };

    public static SourceInfo? FindByName(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

    public static SourceInfo? FindByDownLoadType(int downLoadType) =>
        All.FirstOrDefault(s => s.DownLoadType == downLoadType);

    public static string NameFor(int downLoadType) =>
        FindByDownLoadType(downLoadType)?.Name ?? $"unknown-{downLoadType}";
}

public sealed record SourceInfo(string Name, int DownLoadType, string DisplayName);
