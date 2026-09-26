// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Mobility.Incidents;

/// <summary>
/// The bounded, fictional incident strands (spec: CD7 "เหตุการณ์มีสัญญาณ
/// เตือนและมีเรื่องบวก", "เหตุการณ์สมมติ... ไม่ผูกกับตัวจริง"). G4 shipped
/// the first two (<see cref="NightDisorder"/>/<see cref="StreetRacing"/>);
/// G6-07 added <see cref="RoadworksGridlock"/>/<see cref="IllegalWasteDumping"/>,
/// reusing the exact same rate-bounding discipline
/// <see cref="IncidentThresholds.MinimumFullCycleTicks"/> already
/// enforces. This is a SMALL, NAMED, FINITE list -- G6-07's acceptance
/// criterion is explicit that this must never become an open-ended "add
/// more later without a cap" set; a future strand is a deliberate,
/// reviewed addition to this exact enum, not a data-driven/config-driven
/// unbounded catalog.
///
/// ETHICAL CONSTRAINT (AGENTS.md rule 9 -- non-negotiable), for EVERY
/// strand including future ones:
///   - No real person or real business is EVER the wrongdoer in any
///     strand. Nothing in this namespace stores a name, and nothing in the
///     rest of this codebase feeds a real-world identifier into an
///     incident.
///   - No place CATEGORY (temple, community venue, market, residential
///     block, ...) is inherently a problem. Notice that
///     <see cref="IncidentConditions"/>'s risk functions take only
///     generic, already-anonymised environmental signals (hour, noise
///     index, congestion ratio) -- and structurally CANNOT see a
///     <see cref="Archetypes.BuildingArchetype"/>, a source id, or any
///     other "what/who is this place" identifier. A future contributor
///     tempted to add "if archetype == Temple, risk += X" style special-
///     casing must add a NEW parameter to do so, which is a deliberate
///     friction point -- do not add it. Impact is activity x time x
///     location, never identity (see IncidentConditionsTests' structural
///     control test, which asserts by reflection that no public method in
///     this namespace takes a BuildingArchetype parameter -- it scans the
///     WHOLE namespace, so it automatically covers every strand added
///     here, past or future).
///   - <see cref="RoadworksGridlock"/> is a traffic-backup event tied to
///     construction-zone congestion, never framed as a contractor's or
///     area's fault -- it is a generic infrastructure condition.
///     <see cref="IllegalWasteDumping"/> is a generic, unattributed
///     infrastructure/environmental-crime pattern (quiet, low-traffic,
///     late-night conditions), never tied to any real place, business, or
///     community identity, exactly like <see cref="NightDisorder"/>.
/// </summary>
public enum IncidentStrand
{
    NightDisorder,
    StreetRacing,

    /// <summary>G6-07: a traffic backup at a congested point in the
    /// network during busy daytime hours -- the OPPOSITE congestion
    /// polarity from <see cref="StreetRacing"/> (which wants an OPEN
    /// road), so this is a genuinely distinct condition, not a
    /// relabeled duplicate.</summary>
    RoadworksGridlock,

    /// <summary>G6-07: illegal dumping under cover of a quiet, low-
    /// traffic, late-night window -- peaks at a different hour from
    /// <see cref="NightDisorder"/> (see <see cref="IncidentConditions"/>'s
    /// doc comment) so the two late-night strands are not the same risk
    /// curve twice.</summary>
    IllegalWasteDumping,
}
