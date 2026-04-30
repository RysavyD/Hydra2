namespace Hydra2.Web;

public class AdminAuthOptions
{
    public const string SectionName = "AdminAuth";

    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Realm { get; set; } = "Hydra2 Admin";

    /// <summary>
    /// Path prefixes (case-insensitive) protected by Basic Auth in non-Development environments.
    /// </summary>
    public string[] ProtectedPaths { get; set; } = new[] { "/Adm" };
}
