// Pure C# -- no UnityEngine reference. See game/README.md.
using System;
using System.Collections.Generic;
using Thaivia.Core.Simulation.Economy;

namespace Thaivia.Core.Simulation.Storyline;

public sealed record CorruptionCaseResult(bool Success, IReadOnlyList<string> Failures);

/// <summary>
/// G6-09: open/investigate/resolve-or-dismiss flow for a
/// <see cref="CorruptionCase"/>, using the same transactional discipline
/// <see cref="Planning.PlanningEngine"/>/<see cref="Planning.InvestorProposalEngine"/>
/// already prove: a single atomic ledger credit on resolution, never a
/// double-credit, and no ledger effect at all for dismissal.
///
/// See <see cref="CorruptionCase"/>'s doc comment for the ethical
/// constraints this whole namespace is built to satisfy structurally.
/// </summary>
public static class CorruptionCaseEngine
{
    /// <summary>Opens a case scripted around a REAL committed project:
    /// rejects an unknown project id, and rejects an alleged overpayment
    /// that exceeds what the project has actually had paid on it so far
    /// (<see cref="Planning.CommittedProject.TotalPaid"/>) -- the
    /// storyline's numbers are always bounded by real ledger history,
    /// never invented independently of it.</summary>
    public static CorruptionCaseResult OpenCase(WorldState world, string caseId, string relatedCommittedProjectId, string contractorLabel, long allegedOverpaymentThb)
    {
        if (!world.Projects.TryGetValue(relatedCommittedProjectId, out var project))
        {
            return new CorruptionCaseResult(false, new[] { $"no such committed project: {relatedCommittedProjectId}" });
        }

        var failures = new List<string>();
        if (allegedOverpaymentThb <= 0)
        {
            failures.Add("alleged overpayment must be a positive amount.");
        }
        else if (allegedOverpaymentThb > project.TotalPaid)
        {
            failures.Add($"alleged overpayment ({allegedOverpaymentThb}) cannot exceed what project '{relatedCommittedProjectId}' has actually had paid on it ({project.TotalPaid}).");
        }

        if (world.CorruptionCases.ContainsKey(caseId))
        {
            failures.Add($"a corruption case with id '{caseId}' is already open.");
        }

        if (failures.Count > 0)
        {
            return new CorruptionCaseResult(false, failures);
        }

        world.AddCorruptionCase(new CorruptionCase(caseId, relatedCommittedProjectId, contractorLabel, allegedOverpaymentThb));
        return new CorruptionCaseResult(true, Array.Empty<string>());
    }

    /// <summary>Open -&gt; UnderInvestigation. Pure status change, no
    /// ledger effect.</summary>
    public static CorruptionCaseResult BeginInvestigation(WorldState world, string caseId)
    {
        if (!world.CorruptionCases.TryGetValue(caseId, out var c))
        {
            return new CorruptionCaseResult(false, new[] { $"no such corruption case: {caseId}" });
        }

        if (c.Status != CorruptionCaseStatus.Open)
        {
            return new CorruptionCaseResult(false, new[] { $"case {caseId} is not Open (currently {c.Status})." });
        }

        world.ReplaceCorruptionCase(c.WithStatus(CorruptionCaseStatus.UnderInvestigation));
        return new CorruptionCaseResult(true, Array.Empty<string>());
    }

    /// <summary>Resolves an under-investigation case with a ONE-OFF
    /// recovery credit (plan §12: never a recurring income mechanism --
    /// enforced here structurally: a case can only ever be UnderInvestigation
    /// -&gt; Resolved ONCE, since Resolved is terminal and this method
    /// rejects a case that is not currently UnderInvestigation, so calling
    /// it again on an already-Resolved case is a no-op failure, never a
    /// second credit). <paramref name="recoveredAmountThb"/> is capped at
    /// the case's own <see cref="CorruptionCase.AllegedOverpaymentThb"/> --
    /// recovery can never exceed what was alleged, which itself was
    /// already capped at real money that really moved
    /// (<see cref="OpenCase"/>).</summary>
    public static CorruptionCaseResult ResolveWithRecovery(WorldState world, string caseId, LedgerAccountKind creditKind, long recoveredAmountThb)
    {
        if (!world.CorruptionCases.TryGetValue(caseId, out var c))
        {
            return new CorruptionCaseResult(false, new[] { $"no such corruption case: {caseId}" });
        }

        var failures = new List<string>();
        if (c.Status != CorruptionCaseStatus.UnderInvestigation)
        {
            failures.Add($"case {caseId} is not UnderInvestigation (currently {c.Status}) -- cannot resolve.");
        }

        if (recoveredAmountThb <= 0)
        {
            failures.Add("recovered amount must be positive.");
        }
        else if (recoveredAmountThb > c.AllegedOverpaymentThb)
        {
            failures.Add($"recovered amount ({recoveredAmountThb}) cannot exceed the amount alleged ({c.AllegedOverpaymentThb}).");
        }

        if (failures.Count > 0)
        {
            return new CorruptionCaseResult(false, failures);
        }

        // The single, one-off ledger mutation this whole subsystem ever
        // performs -- exactly one Deposit call, guarded above by the
        // UnderInvestigation-only check, so it can never fire twice for
        // the same case.
        world.Ledger.Deposit(creditKind, recoveredAmountThb);
        world.ReplaceCorruptionCase(c.WithResolved(recoveredAmountThb));
        return new CorruptionCaseResult(true, Array.Empty<string>());
    }

    /// <summary>Closes a case with NO recovery -- no ledger effect at
    /// all, exactly like <see cref="Planning.InvestorProposalEngine.Decline"/>.</summary>
    public static CorruptionCaseResult Dismiss(WorldState world, string caseId)
    {
        if (!world.CorruptionCases.TryGetValue(caseId, out var c))
        {
            return new CorruptionCaseResult(false, new[] { $"no such corruption case: {caseId}" });
        }

        if (c.Status is CorruptionCaseStatus.Resolved or CorruptionCaseStatus.Dismissed)
        {
            return new CorruptionCaseResult(false, new[] { $"case {caseId} is already terminal ({c.Status})." });
        }

        world.ReplaceCorruptionCase(c.WithStatus(CorruptionCaseStatus.Dismissed));
        return new CorruptionCaseResult(true, Array.Empty<string>());
    }
}
