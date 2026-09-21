// Pure C# -- no UnityEngine reference. See game/README.md.
using System.Collections.Generic;
using System.Text.Json;

namespace Thaivia.Core.MapPack;

public sealed class Manifest
{
    public Manifest(
        string mapId,
        string schemaVersion,
        string importerVersion,
        string settingsHash,
        JsonElement settings,
        DrivingSide drivingSide,
        string crsCode,
        IReadOnlyList<string> crsValidationWarnings,
        LocalOrigin localOrigin,
        string unityAxisConvention)
    {
        MapId = mapId;
        SchemaVersion = schemaVersion;
        ImporterVersion = importerVersion;
        SettingsHash = settingsHash;
        Settings = settings.Clone();
        DrivingSide = drivingSide;
        CrsCode = crsCode;
        CrsValidationWarnings = crsValidationWarnings;
        LocalOrigin = localOrigin;
        UnityAxisConvention = unityAxisConvention;
    }

    public string MapId { get; }
    public string SchemaVersion { get; }
    public string ImporterVersion { get; }
    public string SettingsHash { get; }
    public JsonElement Settings { get; }
    public DrivingSide DrivingSide { get; }
    public string CrsCode { get; }
    public IReadOnlyList<string> CrsValidationWarnings { get; }
    public LocalOrigin LocalOrigin { get; }
    public string UnityAxisConvention { get; }
}
