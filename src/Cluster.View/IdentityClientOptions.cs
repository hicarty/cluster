namespace Cluster.View;

public class IdentityClientOptions
{
    public const string SectionName = "IdentityClient";

    /// <summary>Root of the Cluster.Identity app, used for the login and register links.</summary>
    public string BaseUrl { get; set; } = "https://localhost:7276";

    /// <summary>Root of this app, passed to Cluster.Identity as a return URL.</summary>
    public string ViewBaseUrl { get; set; } = "https://localhost:7295";
}