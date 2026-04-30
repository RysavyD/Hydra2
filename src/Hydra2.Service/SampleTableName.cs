using System.Text.RegularExpressions;

namespace Hydra2.Service;

public static partial class SampleTableName
{
    [GeneratedRegex(@"^Sample-\d{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    public static string ForStation(int stationId)
    {
        var name = $"Sample-{stationId:000}";
        if (!Pattern().IsMatch(name))
        {
            throw new ArgumentOutOfRangeException(nameof(stationId), stationId, "Station id does not produce a valid Sample-### table name.");
        }
        return name;
    }
}
