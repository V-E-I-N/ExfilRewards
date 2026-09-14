using SPTarkov.Server.Core.Models.Spt.Mod;

namespace ExfilRewards.Server;

/// <summary>
///     Describes this mod to the SPT server. Every standalone C# mod must contain
///     exactly one IModMetadata implementation.
///
///     Note: SemanticVersioning.Version/Range are referenced with their full
///     namespace below (not via a `using SemanticVersioning;`) because both names
///     collide with System.Version and System.Range - the interface requires the
///     SemanticVersioning ones specifically.
/// </summary>
public class ExfilRewardsMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.froze.exfilrewards";
    public string Name { get; init; } = "Exfil Rewards";
    public string Author { get; init; } = "froze";
    public List<string>? Contributors { get; init; } = null;
    public SemanticVersioning.Version Version { get; init; } = new SemanticVersioning.Version("1.0.0");
    public SemanticVersioning.Range SptVersion { get; init; } = new SemanticVersioning.Range("~4.1.4");
    public bool HasPrepatcher { get; init; } = false;
    public List<string>? Incompatibilities { get; init; } = null;
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; } = null;
    public string? Url { get; init; } = null;
    public string License { get; init; } = "MIT";
}
