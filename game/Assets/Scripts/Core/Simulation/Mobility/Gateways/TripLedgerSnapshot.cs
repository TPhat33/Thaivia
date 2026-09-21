// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Mobility.Gateways;

/// <summary>
/// An integer-exact trip ledger for one direction (inbound or outbound) at
/// one gateway, or summed across every gateway. The invariant this whole
/// namespace exists to guarantee (spec §10: "การปิดประตูเข้าออกต้องกระทบ
/// การเดินทางและบริการ ไม่ทำให้รถหายจาก simulation") is:
///
///     Generated == Completed + Queued   (always, exactly, no tolerance)
///
/// i.e. every trip that was ever generated is EITHER completed OR still
/// queued -- never neither. See GatewayFlowTests for the many-tick proof
/// and its deliberately leaky control.
/// </summary>
public readonly record struct TripLedgerSnapshot(long Generated, long Completed, long Queued)
{
    public bool IsConserved => Generated == Completed + Queued;
}
