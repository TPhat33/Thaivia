// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Mobility.Routing;

/// <summary>Modes the G4 multimodal graph routes over (spec §9/§11:
/// "directed multimodal graph"). Freight is kept distinct from Vehicle even
/// though they share the same physical network, because access tags
/// (`motor_vehicle` vs `hgv`) and future capacity/lane rules can legitimately
/// differ per mode -- collapsing them into one "Vehicle" mode would silently
/// lose that distinction the moment a real HGV-only or HGV-excluded road
/// shows up in imported data.</summary>
public enum TravelMode
{
    Walk,
    Vehicle,
    Freight,
}
