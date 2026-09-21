// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.MapPack;

public sealed class LocalOrigin
{
    public LocalOrigin(string crsCode, double originEastingM, double originNorthingM)
    {
        CrsCode = crsCode;
        OriginEastingM = originEastingM;
        OriginNorthingM = originNorthingM;
    }

    public string CrsCode { get; }
    public double OriginEastingM { get; }
    public double OriginNorthingM { get; }
}
