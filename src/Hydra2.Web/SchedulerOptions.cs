namespace Hydra2.Web;

public class SchedulerOptions
{
    public const string SectionName = "Scheduler";

    /// <summary>
    /// Per-source job definitions. Keys are stable names (e.g. "chmi", "pvl") matching SourceCatalog.
    /// </summary>
    public Dictionary<string, SourceJobOptions> Sources { get; set; } = new();
}

public class SourceJobOptions
{
    public bool Enabled { get; set; } = true;
    public int DownLoadType { get; set; }

    /// <summary>
    /// Quartz cron expression. 7 fields: sec min hr day-of-month month day-of-week [year].
    /// Example: "0 0/15 * * * ?" = at second 0, every 15 minutes.
    /// </summary>
    public string Cron { get; set; } = string.Empty;

    /// <summary>
    /// Misfire handling. "FireAndProceed" runs once now after wakeup; "Ignore" skips missed fires.
    /// </summary>
    public string Misfire { get; set; } = "FireAndProceed";
}
