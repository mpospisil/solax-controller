namespace Solax.Infrastructure.Solcast;

/// <summary>
/// Configuration for the Solcast forecast integration. Bound from the <c>"Solcast"</c>
/// configuration section. The <see cref="ApiKey"/> is a secret and must come from outside the
/// repository (user-secrets in development, environment variables in deployment) -- never
/// committed to <c>appsettings.json</c>.
/// </summary>
public sealed class SolcastOptions
{
    public const string SectionName = "Solcast";

    /// <summary>Base address of the Solcast API.</summary>
    public string BaseUrl { get; init; } = "https://api.solcast.com.au/";

    /// <summary>Solcast API key. Secret -- supply via user-secrets or an environment variable.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>The Solcast rooftop-site (resource) id to fetch the forecast for.</summary>
    public string ResourceId { get; init; } = string.Empty;

    /// <summary>
    /// How often the cached forecast is refreshed from Solcast. Defaults to 12 hours to stay well
    /// within the free hobbyist tier's daily request quota.
    /// </summary>
    public TimeSpan RefreshInterval { get; init; } = TimeSpan.FromHours(12);
}
