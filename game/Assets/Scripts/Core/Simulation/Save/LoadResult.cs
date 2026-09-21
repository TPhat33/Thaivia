// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Save;

/// <summary>
/// The outcome of <see cref="SaveGameStore.LoadLatest"/> as an explicit
/// result type (no UI needed this wave -- spec's instruction to "surface
/// it as a result type" taken literally): a caller is FORCED to pattern-
/// match one of these four cases rather than being handed a nullable
/// SaveGame that could be silently used without checking why it might be
/// null.
/// </summary>
public abstract class LoadResult
{
    private LoadResult()
    {
    }

    public sealed class Loaded : LoadResult
    {
        public Loaded(SaveGame save, bool fellBackToPrevious)
        {
            Save = save;
            FellBackToPrevious = fellBackToPrevious;
        }

        public SaveGame Save { get; }

        /// <summary>True when the "latest" slot was missing/corrupt and
        /// this save actually came from the "previous" slot (spec §13:
        /// "เก็บ latest/previous และ recover จากความเสียหายอย่างแจ้ง
        /// ผู้เล่น") -- the player-facing layer should tell the player
        /// this happened, not load silently.</summary>
        public bool FellBackToPrevious { get; }
    }

    /// <summary>The save's mapId/mapContentHash does not match the
    /// installed MapPack -- spec §13: "OSM snapshot ใหม่ห้ามทับ save ที่
    /// กำลังเล่น" / "loading a save whose mapVersion/hash doesn't match
    /// the installed pack must produce an explicit, actionable error, not
    /// a partial load". Nothing is loaded in this case.</summary>
    public sealed class MapVersionMismatch : LoadResult
    {
        public MapVersionMismatch(string savedMapId, string savedMapContentHash, string installedMapId, string installedMapContentHash)
        {
            SavedMapId = savedMapId;
            SavedMapContentHash = savedMapContentHash;
            InstalledMapId = installedMapId;
            InstalledMapContentHash = installedMapContentHash;
        }

        public string SavedMapId { get; }
        public string SavedMapContentHash { get; }
        public string InstalledMapId { get; }
        public string InstalledMapContentHash { get; }
    }

    /// <summary>Both the latest and previous slots were missing, unparsable,
    /// or otherwise unusable -- nothing could be recovered.</summary>
    public sealed class Corrupt : LoadResult
    {
        public Corrupt(string reason)
        {
            Reason = reason;
        }

        public string Reason { get; }
    }

    public sealed class NotFound : LoadResult
    {
    }
}
