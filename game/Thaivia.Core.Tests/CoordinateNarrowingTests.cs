using System.Linq;
using Thaivia.Core.Coordinates;
using Thaivia.Core.MapPack;
using Thaivia.Core.Serialization;
using Xunit;
using Xunit.Abstractions;

namespace Thaivia.Core.Tests;

public class CoordinateNarrowingTests
{
    private readonly ITestOutputHelper _output;

    public CoordinateNarrowingTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Narrowing_AtPilotSizedExtent_MeasuredErrorIsReportedAndBounded()
    {
        // `configs/pilot-area.json`'s context_buffer_m + editable_bbox
        // put every real local_x/local_z coordinate within roughly a few
        // kilometres of the local origin (see docs/data/pilot-audit.md /
        // ADR-0005). We do not have a real pilot pack (blocked -- see
        // ADR-0003), so this measures float32 narrowing error at a
        // synthetic point representative of that extent: 5000m from
        // origin, the rough order of magnitude for a multi-km pilot AOI
        // plus its buffer.
        const double pilotExtentMeters = 5000.123456789;
        var point = new Vec2(pilotExtentMeters, -pilotExtentMeters);

        var ground = CoordinateNarrowing.ToUnityGroundPlane(point, 0f, out var errX, out var errZ);

        _output.WriteLine($"float32 narrowing error at {pilotExtentMeters}m: errX={errX:G17}m errZ={errZ:G17}m");
        _output.WriteLine($"narrowed value: X={ground.X:G9} Z={ground.Z:G9}");

        // float32 has ~7.2 decimal digits of precision; at 5000m the ULP
        // is on the order of 2^-23 * 2^13 ~= 0.00049m. Bound generously at
        // 1mm so the assertion documents "this stays sub-millimetre",
        // without pretending the error is exactly zero.
        Assert.True(System.Math.Abs(errX) < 0.001, $"errX={errX}");
        Assert.True(System.Math.Abs(errZ) < 0.001, $"errZ={errZ}");
        Assert.NotEqual(0.0, errX); // the narrowing genuinely loses precision; a fake "0 error" would be a bug.
    }

    [Fact]
    public void Narrowing_OverEveryCoordinateInARealBakedPack_MeasuredMaxErrorIsReported()
    {
        var doc = MapPackLoader.LoadText(TestFixtures.ReadRoadGraphLayersJson());
        var allPoints = doc.Payload.RoadGraph.Nodes.Select(n => new Vec2(n.LocalX, n.LocalZ)).ToList();
        Assert.NotEmpty(allPoints);

        double maxAbsError = 0;
        foreach (var p in allPoints)
        {
            CoordinateNarrowing.ToUnityGroundPlane(p, 0f, out var errX, out var errZ);
            maxAbsError = System.Math.Max(maxAbsError, System.Math.Max(System.Math.Abs(errX), System.Math.Abs(errZ)));
        }

        _output.WriteLine($"max |float32 narrowing error| over {allPoints.Count} road-graph node coordinates in "
            + $"the synthetic fixture: {maxAbsError:G17}m");

        // This bound (1cm) is generous, not tuned to the observed value:
        // it documents "float32 narrowing stays sub-centimetre at this
        // extent" without hiding the real measured number, which is
        // written to test output above regardless of pass/fail.
        Assert.True(maxAbsError < 0.01, $"maxAbsError={maxAbsError}");
    }

    [Fact]
    public void Rebase_ShiftsCoordinatesByExactlyTheOriginDelta()
    {
        var p = new Vec2(100.0, 200.0);
        var delta = new Vec2(10.0, -5.0);
        var rebased = CoordinateNarrowing.Rebase(p, delta);

        Assert.Equal(90.0, rebased.X, precision: 12);
        Assert.Equal(205.0, rebased.Z, precision: 12);
    }
}
