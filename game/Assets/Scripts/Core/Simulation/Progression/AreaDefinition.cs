// Pure C# -- no UnityEngine reference. See game/README.md.
using System;

namespace Thaivia.Core.Simulation.Progression;

/// <summary>One entry in the country/area-select screen's ordered list
/// of areas (G6-10, plan §16: "หน้าประเทศใช้เลือกฉากและติดตามความ
/// ก้าวหน้า ไม่เปิดทุกพื้นที่พร้อมกัน"). <see cref="AreaId"/> matches a
/// `configs/areas/index.json` entry's id (G6-01) so this model and the
/// Python area registry share the same identifier space, but this type
/// itself has no dependency on that file or on any map data -- it only
/// needs a stable id string.</summary>
public sealed class AreaDefinition
{
    public AreaDefinition(string areaId, string displayTitle)
    {
        if (string.IsNullOrWhiteSpace(areaId))
        {
            throw new ArgumentException("AreaDefinition.AreaId must not be empty.", nameof(areaId));
        }

        if (string.IsNullOrWhiteSpace(displayTitle))
        {
            throw new ArgumentException("AreaDefinition.DisplayTitle must not be empty.", nameof(displayTitle));
        }

        AreaId = areaId;
        DisplayTitle = displayTitle;
    }

    public string AreaId { get; }
    public string DisplayTitle { get; }
}
