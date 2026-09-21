// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Planning;

/// <summary>
/// A predicted impact, which spec §12 requires to always carry
/// uncertainty ("cost ที่กำหนดแน่นอนแยกจาก predicted impacts ที่มี
/// uncertainty") -- this type has NO single-number constructor: every
/// <see cref="ImpactRange"/> is an expected value plus an explicit
/// min/max band, so a UI reading this type is forced to decide how to
/// display a range rather than being handed a bare number it could show
/// as if it were definite. Contrast with
/// <see cref="ProjectDraft.FixedCostThb"/>, which IS a definite integer
/// -- cost and predicted impact are deliberately different types/shapes,
/// not the same field reused for both.
/// </summary>
public readonly record struct ImpactRange(double ExpectedValue, double MinValue, double MaxValue, string MetricName);
