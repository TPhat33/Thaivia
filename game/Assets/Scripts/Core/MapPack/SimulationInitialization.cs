// Pure C# -- no UnityEngine reference. See game/README.md.
using System.Text.Json;

namespace Thaivia.Core.MapPack;

/// <summary>
/// The SimulationInitialization layer (AGENTS.md rule 3, layer 2):
/// population/jobs/income/happiness/noise/demand *scenario* seeds. This
/// is a separate top-level type from <see cref="GeographyBase"/> and from
/// <see cref="Thaivia.Core.Simulation.PlayerDelta"/>/
/// <see cref="Thaivia.Core.Simulation.WorldState"/> -- there is no shared
/// base type or interface that would let a caller pass one where another
/// is expected. `Seed` is deliberately untyped JSON for now: G1 leaves it
/// `{}` (see `map_pipeline.pipeline.mappack.run_pipeline`), and real
/// scenario-seed fields land here in G3, not as fields bolted onto
/// GeographyBase.
/// </summary>
public sealed class SimulationInitialization
{
    public SimulationInitialization(string schemaVersion, string note, JsonElement seed)
    {
        SchemaVersion = schemaVersion;
        Note = note;
        Seed = seed.Clone();
    }

    public string SchemaVersion { get; }
    public string Note { get; }
    public JsonElement Seed { get; }
}
