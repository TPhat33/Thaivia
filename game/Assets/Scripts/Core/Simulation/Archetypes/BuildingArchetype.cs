// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Archetypes;

/// <summary>
/// Generic building/activity archetypes for G3 (spec IMPLEMENTATION_PLAN
/// §4/§11: "6-8 building/activity archetypes"). These are eight GENERIC
/// categories -- never a stand-in for a specific named business, temple,
/// or community, and never carrying a fixed "negative" score.
///
/// ETHICAL CONSTRAINT (AGENTS.md rule 9; plan §11 "ไม่ตั้งศาสนสถานหรือ
/// ชุมชนใดเป็นปัญหาในตัวเอง"): impact is always activity x time x
/// location (see <see cref="ActivityClockCatalog"/> and
/// Thaivia.Core.Simulation.Noise.NoiseIndex), never a fixed per-archetype
/// score. In particular:
///   - Temple is modelled as calm almost all hours, exactly like any
///     other archetype would be outside its peak window -- it is never
///     wired to a permanently elevated noise/impact number, and no code
///     in this codebase attaches wrongdoing or blame to a temple.
///   - LateNightFoodStreet is loud specifically at night and quiet by
///     morning -- the archetype models a *pattern of activity*, not an
///     inherently undesirable place.
///   - Nothing here names, or is derived from, a real establishment. Any
///     future content author adding flavour text to an archetype must
///     keep it generic (see AGENTS.md rule 9) -- do not wire a specific
///     real business/temple/person into this enum's data.
/// </summary>
public enum BuildingArchetype
{
    Residential,
    LateNightFoodStreet,
    SmallFactory,
    Temple,
    Market,
    Office,
    School,
    Retail,
}
