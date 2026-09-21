// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Time;

/// <summary>
/// The logical simulation tick rate (spec IMPLEMENTATION_PLAN.th.md §11:
/// "logical tick ที่ไม่ขึ้นกับ FPS"; §15 proposes 5Hz as an experimental
/// starting target for the pilot AOI, not a measured/validated result).
/// This is the ONE place that number is written down -- every other type
/// in Thaivia.Core that needs a tick rate reads this constant instead of
/// repeating "5.0" or "5" as a magic number (see docs/decisions for the
/// ADR backing this choice and how it would be revisited after a real
/// soak measurement).
/// </summary>
public static class TickConfig
{
    public const double TicksPerSecond = 5.0;

    public static double SecondsPerTick => 1.0 / TicksPerSecond;
}
