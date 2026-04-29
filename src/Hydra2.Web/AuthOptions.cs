namespace Hydra2.Web;

public class AuthOptions
{
    public const string SectionName = "Auth";

    public string SecretToken { get; set; } = string.Empty;
}
