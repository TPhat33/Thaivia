// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.MapPack;

/// <summary>OSM/ODbL attribution the viewer must render legibly (AGENTS.md
/// / ADR-0004) -- never optional, never buried.</summary>
public sealed class Attribution
{
    public Attribution(string notice, string odblLicenseUrl, string copyrightUrl)
    {
        Notice = notice;
        OdblLicenseUrl = odblLicenseUrl;
        CopyrightUrl = copyrightUrl;
    }

    public string Notice { get; }
    public string OdblLicenseUrl { get; }
    public string CopyrightUrl { get; }
}
