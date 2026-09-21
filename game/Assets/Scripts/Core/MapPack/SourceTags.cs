// Pure C# -- no UnityEngine reference. See game/README.md.
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Thaivia.Core.MapPack;

/// <summary>
/// Tags exactly as read from the OSM source (mirrors
/// `map_pipeline.pipeline.tags.SourceTags`). This is the GeographyBase
/// layer's leaf type: it is built once, from the MapPack file, and is
/// read-only for the rest of this process's life. There is no setter, no
/// indexer assignment, and no method that merges another dictionary into
/// it -- a simulation or player value literally cannot be written into a
/// source-tag field through this type (AGENTS.md rule 3).
/// </summary>
public sealed class SourceTags
{
    public static readonly SourceTags Empty = new(new Dictionary<string, string>());

    private readonly IReadOnlyDictionary<string, string> _tags;

    public SourceTags(IReadOnlyDictionary<string, string> tags)
    {
        _tags = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(tags));
    }

    public IReadOnlyDictionary<string, string> Tags => _tags;

    public bool Has(string key) => _tags.ContainsKey(key);

    public string? Get(string key) => _tags.TryGetValue(key, out var value) ? value : null;
}
