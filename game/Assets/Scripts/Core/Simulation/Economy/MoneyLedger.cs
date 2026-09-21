// Pure C# -- no UnityEngine reference. See game/README.md.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Thaivia.Core.Simulation.Economy;

/// <summary>
/// Integer-money ledger (AGENTS.md / plan §12: "เงินเป็น integer... ห้าม
/// ใช้ float ที่ ledger เลย"). Every amount here is a whole-baht `long` --
/// there is no `double`/`decimal` anywhere in this type's signatures, so
/// a caller cannot even compile code that passes a fractional amount in.
///
/// Reservation and payment are two separate operations by design
/// (<see cref="Reserve"/> vs <see cref="ChargeFromReservation"/>), each
/// tracked per <see cref="LedgerAccountKind"/>:
///   - Reserve moves budget from "available" into "reserved" WITHOUT
///     touching cash -- accepting a project sets money aside instantly.
///   - ChargeFromReservation removes the SAME amount from cash and from
///     reserved TOGETHER, in one call -- so the total ever deducted from
///     cash for a reservation can never exceed what was reserved for it;
///     there is no code path that deducts cash without also shrinking the
///     matching reservation, which is what makes "reserve, then pay in
///     milestones, then never double-charge" a structural property of
///     this type rather than something a caller has to get right by
///     convention (see Thaivia.Core.Simulation.Planning.PlanningEngine
///     for the higher-level reserve-then-pay-milestones flow this backs).
/// </summary>
public sealed class MoneyLedger
{
    private readonly Dictionary<LedgerAccountKind, long> _cash;
    private readonly Dictionary<LedgerAccountKind, long> _reserved;

    public MoneyLedger(long initialCashThb)
    {
        if (initialCashThb < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialCashThb));
        }

        _cash = AllKinds().ToDictionary(k => k, _ => 0L);
        _reserved = AllKinds().ToDictionary(k => k, _ => 0L);
        _cash[LedgerAccountKind.NonRecurring] = initialCashThb;
    }

    private MoneyLedger(Dictionary<LedgerAccountKind, long> cash, Dictionary<LedgerAccountKind, long> reserved)
    {
        _cash = cash;
        _reserved = reserved;
    }

    private static IEnumerable<LedgerAccountKind> AllKinds() => (LedgerAccountKind[])Enum.GetValues(typeof(LedgerAccountKind));

    public long Cash => _cash.Values.Sum();
    public long Reserved => _reserved.Values.Sum();
    public long Available => Cash - Reserved;

    public long CashOf(LedgerAccountKind kind) => _cash[kind];
    public long ReservedOf(LedgerAccountKind kind) => _reserved[kind];

    public IReadOnlyDictionary<LedgerAccountKind, long> CaptureCashByKind() => new Dictionary<LedgerAccountKind, long>(_cash);
    public IReadOnlyDictionary<LedgerAccountKind, long> CaptureReservedByKind() => new Dictionary<LedgerAccountKind, long>(_reserved);

    public static MoneyLedger Restore(IReadOnlyDictionary<LedgerAccountKind, long> cashByKind, IReadOnlyDictionary<LedgerAccountKind, long> reservedByKind) =>
        new(AllKinds().ToDictionary(k => k, k => cashByKind[k]), AllKinds().ToDictionary(k => k, k => reservedByKind[k]));

    public void Reserve(LedgerAccountKind kind, long amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        if (amount > Available)
        {
            throw new InvalidOperationException($"Cannot reserve {amount}: only {Available} available.");
        }

        _reserved[kind] += amount;
    }

    public void ReleaseReservation(LedgerAccountKind kind, long amount)
    {
        if (amount < 0 || amount > _reserved[kind])
        {
            throw new InvalidOperationException($"Cannot release {amount}: only {_reserved[kind]} reserved for {kind}.");
        }

        _reserved[kind] -= amount;
    }

    /// <summary>Deducts <paramref name="amount"/> from BOTH cash and the
    /// matching reservation for <paramref name="kind"/>, atomically. This
    /// is the only method that ever reduces cash for a reserved project --
    /// see the type doc comment for why that makes double-charging
    /// structurally impossible for reserved spend.</summary>
    public void ChargeFromReservation(LedgerAccountKind kind, long amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        if (amount > _reserved[kind])
        {
            throw new InvalidOperationException($"Cannot charge {amount}: only {_reserved[kind]} reserved for {kind}.");
        }

        _reserved[kind] -= amount;
        _cash[kind] -= amount;
    }

    /// <summary>Deducts <paramref name="amount"/> from cash directly,
    /// without any reservation -- used for costs that are never reserved
    /// in the first place (e.g. a committed-project cancellation fee,
    /// plan §12 "ยกเลิกโครงการใช้กติกาต้นทุนจริงในเกม").</summary>
    public void ChargeDirect(LedgerAccountKind kind, long amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        if (amount > Available)
        {
            throw new InvalidOperationException($"Cannot charge {amount} directly: only {Available} available.");
        }

        _cash[kind] -= amount;
    }

    public void Deposit(LedgerAccountKind kind, long amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        _cash[kind] += amount;
    }
}
