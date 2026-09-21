// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Mobility.Incidents;

/// <summary>
/// The two fictional incident strands (spec: CD7 "เหตุการณ์มีสัญญาณเตือนและ
/// มีเรื่องบวก", "เหตุการณ์สมมติ... ไม่ผูกกับตัวจริง").
///
/// ETHICAL CONSTRAINT (AGENTS.md rule 9 -- non-negotiable):
///   - No real person or real business is EVER the wrongdoer in either
///     strand. Nothing in this namespace stores a name, and nothing in the
///     rest of this codebase feeds a real-world identifier into an
///     incident.
///   - No place CATEGORY (temple, community venue, market, residential
///     block, ...) is inherently a problem. Notice that
///     <see cref="IncidentConditions"/>'s risk functions take only
///     (hour, noiseIndex, congestionRatio) -- generic, already-anonymised
///     environmental signals -- and structurally CANNOT see a
///     <see cref="Archetypes.BuildingArchetype"/>, a source id, or any
///     other "what/who is this place" identifier. A future contributor
///     tempted to add "if archetype == Temple, risk += X" style special-
///     casing must add a NEW parameter to do so, which is a deliberate
///     friction point -- do not add it. Impact is activity x time x
///     location, never identity (see IncidentConditionsTests' structural
///     control test, which asserts by reflection that no public method in
///     this namespace takes a BuildingArchetype parameter).
/// </summary>
public enum IncidentStrand
{
    NightDisorder,
    StreetRacing,
}
