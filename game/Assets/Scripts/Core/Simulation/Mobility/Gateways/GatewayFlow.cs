// Pure C# -- no UnityEngine reference. See game/README.md.
using System;

namespace Thaivia.Core.Simulation.Mobility.Gateways;

/// <summary>
/// Bounded inbound/outbound trip flow through one boundary
/// <see cref="MapPack.Gateway"/> (spec §10: gateways carry bounded inbound/
/// outbound demand and finite external capacity; a closed gateway must
/// push cost/queueing back into the network, never delete demand).
///
/// Every trip this type is told about via <see cref="GenerateOutboundDemand"/>/
/// <see cref="GenerateInboundDemand"/> is tracked until it either departs
/// (<see cref="Step"/> discharges up to capacity) or stays queued
/// (<see cref="PendingOutbound"/>/<see cref="PendingInbound"/>) -- there is
/// no code path in this type that reduces a pending count without moving
/// the same amount into a completed count. <see cref="Close"/> does NOT
/// touch <see cref="PendingOutbound"/>/<see cref="PendingInbound"/> at all;
/// it only makes <see cref="Step"/> treat this tick's capacity as zero, so
/// closing an open gateway with trips already queued makes them queue
/// LONGER, never disappear.
/// </summary>
public sealed class GatewayFlow
{
    public GatewayFlow(long gatewayNodeId, int inboundCapacityPerTick, int outboundCapacityPerTick, bool isOpen = true)
    {
        if (inboundCapacityPerTick < 0 || outboundCapacityPerTick < 0)
        {
            throw new ArgumentOutOfRangeException(inboundCapacityPerTick < 0 ? nameof(inboundCapacityPerTick) : nameof(outboundCapacityPerTick));
        }

        GatewayNodeId = gatewayNodeId;
        InboundCapacityPerTick = inboundCapacityPerTick;
        OutboundCapacityPerTick = outboundCapacityPerTick;
        IsOpen = isOpen;
    }

    public long GatewayNodeId { get; }
    public int InboundCapacityPerTick { get; }
    public int OutboundCapacityPerTick { get; }
    public bool IsOpen { get; private set; }

    public long GeneratedOutbound { get; private set; }
    public long CompletedOutbound { get; private set; }
    public long PendingOutbound { get; private set; }

    public long GeneratedInbound { get; private set; }
    public long CompletedInbound { get; private set; }
    public long PendingInbound { get; private set; }

    public TripLedgerSnapshot OutboundLedger => new(GeneratedOutbound, CompletedOutbound, PendingOutbound);
    public TripLedgerSnapshot InboundLedger => new(GeneratedInbound, CompletedInbound, PendingInbound);

    /// <summary>Opening/closing NEVER touches Pending*/Completed*/Generated*
    /// -- only <see cref="Step"/>'s capacity this tick.</summary>
    public void Open() => IsOpen = true;

    public void Close() => IsOpen = false;

    public void GenerateOutboundDemand(long amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        GeneratedOutbound += amount;
        PendingOutbound += amount;
    }

    public void GenerateInboundDemand(long amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        GeneratedInbound += amount;
        PendingInbound += amount;
    }

    /// <summary>Advances one tick: discharges up to this tick's effective
    /// capacity (0 while <see cref="IsOpen"/> is false) from each pending
    /// count into its completed count. Returns how many actually departed/
    /// arrived this tick.</summary>
    public (long DepartedOutbound, long ArrivedInbound) Step()
    {
        var outboundCapacity = IsOpen ? OutboundCapacityPerTick : 0;
        var departed = Math.Min(PendingOutbound, outboundCapacity);
        PendingOutbound -= departed;
        CompletedOutbound += departed;

        var inboundCapacity = IsOpen ? InboundCapacityPerTick : 0;
        var arrived = Math.Min(PendingInbound, inboundCapacity);
        PendingInbound -= arrived;
        CompletedInbound += arrived;

        return (departed, arrived);
    }
}
