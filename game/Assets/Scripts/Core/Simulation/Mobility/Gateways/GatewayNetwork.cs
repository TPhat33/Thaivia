// Pure C# -- no UnityEngine reference. See game/README.md.
using System.Collections.Generic;
using System.Linq;

namespace Thaivia.Core.Simulation.Mobility.Gateways;

/// <summary>Owns every <see cref="GatewayFlow"/> in the loaded MapPack's
/// road_graph and advances them together each tick. Also exposes the
/// SUMMED conservation ledger across every gateway, which is what a
/// whole-world "trips never vanish" check reads (see
/// GatewayConservationTests).</summary>
public sealed class GatewayNetwork
{
    private readonly Dictionary<long, GatewayFlow> _flows;

    public GatewayNetwork(IEnumerable<GatewayFlow> flows)
    {
        _flows = flows.ToDictionary(f => f.GatewayNodeId);
    }

    public IReadOnlyDictionary<long, GatewayFlow> Flows => _flows;

    public GatewayFlow this[long gatewayNodeId] => _flows[gatewayNodeId];

    public void StepAll()
    {
        foreach (var flow in _flows.Values)
        {
            flow.Step();
        }
    }

    public TripLedgerSnapshot TotalOutboundLedger() => Sum(_flows.Values.Select(f => f.OutboundLedger));
    public TripLedgerSnapshot TotalInboundLedger() => Sum(_flows.Values.Select(f => f.InboundLedger));

    private static TripLedgerSnapshot Sum(IEnumerable<TripLedgerSnapshot> snapshots)
    {
        long generated = 0, completed = 0, queued = 0;
        foreach (var s in snapshots)
        {
            generated += s.Generated;
            completed += s.Completed;
            queued += s.Queued;
        }

        return new TripLedgerSnapshot(generated, completed, queued);
    }
}
