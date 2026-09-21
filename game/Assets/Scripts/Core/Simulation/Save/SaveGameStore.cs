// Pure C# -- no UnityEngine reference. See game/README.md.
using System;
using System.IO;

namespace Thaivia.Core.Simulation.Save;

/// <summary>
/// Keeps "latest" and "previous" save slots on disk (spec §13: "เก็บ
/// latest/previous และ recover จากความเสียหายอย่างแจ้งผู้เล่น"). Writes
/// are atomic (write to a temp file, then <see cref="File.Move"/> with
/// overwrite, which is atomic on the filesystems this runs on) so a crash
/// mid-write cannot leave "latest" itself half-written -- the corruption
/// this type has to recover from is a save file damaged some other way
/// (disk error, manual edit, truncated sync), not a torn write from this
/// store's own Save() call.
/// </summary>
public sealed class SaveGameStore
{
    private const string LatestFileName = "save-latest.json";
    private const string PreviousFileName = "save-previous.json";

    private readonly string _directory;

    public SaveGameStore(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(_directory);
    }

    private string LatestPath => Path.Combine(_directory, LatestFileName);
    private string PreviousPath => Path.Combine(_directory, PreviousFileName);

    /// <summary>Rotates the existing "latest" (if any) into "previous",
    /// then atomically writes the new save as "latest".</summary>
    public void Save(SaveGame save)
    {
        if (File.Exists(LatestPath))
        {
            File.Copy(LatestPath, PreviousPath, overwrite: true);
        }

        var json = SaveSerializer.Serialize(save);
        var tempPath = LatestPath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, LatestPath, overwrite: true);
    }

    public LoadResult LoadLatest(string expectedMapId, string expectedMapContentHash)
    {
        if (!File.Exists(LatestPath) && !File.Exists(PreviousPath))
        {
            return new LoadResult.NotFound();
        }

        if (TryRead(LatestPath, out var latestSave, out var latestReason))
        {
            return CheckMapVersion(latestSave!, expectedMapId, expectedMapContentHash, fellBackToPrevious: false);
        }

        if (TryRead(PreviousPath, out var previousSave, out var previousReason))
        {
            var result = CheckMapVersion(previousSave!, expectedMapId, expectedMapContentHash, fellBackToPrevious: true);
            if (result is LoadResult.Loaded loaded)
            {
                return new LoadResult.Loaded(loaded.Save, fellBackToPrevious: true);
            }

            // A version mismatch on the fallback is still an explicit,
            // actionable error -- never silently loaded.
            return result;
        }

        return new LoadResult.Corrupt($"latest: {latestReason}; previous: {previousReason}");
    }

    private static LoadResult CheckMapVersion(SaveGame save, string expectedMapId, string expectedMapContentHash, bool fellBackToPrevious)
    {
        if (save.MapId != expectedMapId || save.MapContentHash != expectedMapContentHash)
        {
            return new LoadResult.MapVersionMismatch(save.MapId, save.MapContentHash, expectedMapId, expectedMapContentHash);
        }

        return new LoadResult.Loaded(save, fellBackToPrevious);
    }

    private static bool TryRead(string path, out SaveGame? save, out string reason)
    {
        if (!File.Exists(path))
        {
            save = null;
            reason = "file does not exist.";
            return false;
        }

        try
        {
            var json = File.ReadAllText(path);
            save = SaveSerializer.Deserialize(json);
            reason = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            save = null;
            reason = ex.Message;
            return false;
        }
    }
}
