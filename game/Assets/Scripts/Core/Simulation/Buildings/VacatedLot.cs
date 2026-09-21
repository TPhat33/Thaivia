// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Buildings;

/// <summary>Records where a relocated building used to stand (plan §8:
/// "การย้าย = destination geometry + project cost + occupants/business
/// transfer + old-location treatment"). This is PlayerDelta-layer
/// bookkeeping only -- it never edits GeographyBase, so a before/after
/// comparison against the original source footprint always stays
/// possible by reading GeographyBase directly plus this record.</summary>
public readonly record struct VacatedLot(long BuildingSourceId, double OldLocalX, double OldLocalZ, string ProjectId);
