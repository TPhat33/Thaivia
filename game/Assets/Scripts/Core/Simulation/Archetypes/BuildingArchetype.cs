// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Archetypes;

/// <summary>
/// Generic building/activity archetypes (spec IMPLEMENTATION_PLAN
/// §4/§11/§16: "6-8" for G3, expanded toward a "12-16" scope CEILING for
/// G6 -- a ceiling, not a quota: this codebase stops at 12 because the
/// two currently-registered pilot/candidate areas' gameplay problems
/// (office/residential mix, canal/old-town access, arterial/market
/// congestion -- see docs/data/candidate-areas-rationale.md) are covered
/// by these twelve; padding further without a concrete gameplay need
/// would just be unaudited data. These are GENERIC categories -- never a
/// stand-in for a specific named business, temple, or community, and
/// never carrying a fixed "negative" score.
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
///   - Every archetype added in G6 (Hospital, Hotel, Warehouse,
///     ConvenienceStore) got the same shape review as Temple: none of
///     them is wired to a permanently elevated or permanently negative
///     level, each has a real peak window and a genuinely low baseline,
///     and none represents a religious, ethnic, or community land use
///     (the ones that DO -- Temple -- already carry the strictest
///     review). See ActivityClockCatalogTests for the per-archetype
///     shape assertions and
///     BuildingArchetypeEthicalControlTests.
///     EthicalControl_NoArchetypeCarriesAStaticScalarScoreIndependentOfActivityClock
///     for the machine-enforced structural control (not just this
///     comment) that makes a future "hard-coded bad score per archetype"
///     regression fail that test, not just fail human review.
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

    // -- G6 additions (plan §16 ceiling, not quota) --

    /// <summary>24-hour operation with a near-flat, moderate service
    /// load (emergency care does not "peak" the way a market does) --
    /// modelled as a genuine utility-adjacent need generator, not a
    /// disorder source.</summary>
    Hospital,

    /// <summary>Moderate activity through the day with an evening
    /// check-in/dinner peak -- distinct from Residential (guests, not a
    /// household cohort) and from Retail (overnight occupancy).</summary>
    Hotel,

    /// <summary>Early-morning-weighted freight/loading activity,
    /// quiet overnight -- models logistics land use distinctly from
    /// SmallFactory's mid-day production peak.</summary>
    Warehouse,

    /// <summary>Long-hours, low-intensity, near-flat retail activity --
    /// deliberately the *lowest*-amplitude peak of any archetype (small,
    /// frequent, low-noise transactions), so this codebase's data does
    /// not read as "every commercial archetype is loud".</summary>
    ConvenienceStore,
}
