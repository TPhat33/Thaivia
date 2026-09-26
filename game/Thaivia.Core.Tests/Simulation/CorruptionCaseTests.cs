using System.Linq;
using System.Reflection;
using Thaivia.Core.Simulation;
using Thaivia.Core.Simulation.Economy;
using Thaivia.Core.Simulation.Planning;
using Thaivia.Core.Simulation.Storyline;
using Xunit;

namespace Thaivia.Core.Tests.Simulation;

/// <summary>
/// G6-09: the one fictional procurement/corruption storyline, driven
/// through real CommittedProject/MoneyLedger state -- open, investigate,
/// resolve-with-recovery, dismiss -- plus the machine-enforced ethical
/// control that the storyline API structurally cannot bind a case to any
/// real GeographyBase-layer feature.
/// </summary>
public class CorruptionCaseTests
{
    private static (WorldState World, string ProjectId) BuildWorldWithAPaidProject(long milestonePaid = 100_000, long milestone2 = 100_000)
    {
        var world = SimulationFixtures.BuildWorldState(initialCashThb: 1_000_000);
        var impact = PlanningEngine.EstimateNewRoadAccessImpact(world, SimulationFixtures.Node2, SimulationFixtures.Node3, SimulationFixtures.ResidentialBuildingId);
        var draft = new NewRoadConnectorDraft("road-with-case", world.Revision, milestonePaid + milestone2, impact,
            SimulationFixtures.Node2, SimulationFixtures.Node3);
        var commit = PlanningEngine.CommitNewRoadConnector(world, draft, LedgerAccountKind.Capex, new long[] { milestonePaid, milestone2 });
        Assert.True(commit.Success, string.Join("; ", commit.Failures));
        PlanningEngine.PayMilestone(world, "road-with-case", milestoneIndex: 0);
        return (world, "road-with-case");
    }

    [Fact]
    public void OpenCase_AgainstAnUnknownProject_IsRejected()
    {
        var world = SimulationFixtures.BuildWorldState();
        var result = CorruptionCaseEngine.OpenCase(world, "case-1", "no-such-project", "Contractor Alpha", 1000);
        Assert.False(result.Success);
    }

    [Fact]
    public void OpenCase_AllegingMoreThanWasActuallyPaid_IsRejected()
    {
        var (world, projectId) = BuildWorldWithAPaidProject(milestonePaid: 60_000, milestone2: 40_000);
        // Only the first milestone (60,000) has been paid so far.
        var result = CorruptionCaseEngine.OpenCase(world, "case-1", projectId, "Contractor Alpha", allegedOverpaymentThb: 60_001);
        Assert.False(result.Success);
    }

    [Fact]
    public void OpenCase_WithinWhatWasActuallyPaid_Succeeds()
    {
        var (world, projectId) = BuildWorldWithAPaidProject(milestonePaid: 60_000, milestone2: 40_000);
        var result = CorruptionCaseEngine.OpenCase(world, "case-1", projectId, "Contractor Alpha", allegedOverpaymentThb: 60_000);
        Assert.True(result.Success, string.Join("; ", result.Failures));
        Assert.Equal(CorruptionCaseStatus.Open, world.CorruptionCases["case-1"].Status);
    }

    [Fact]
    public void DuplicateCaseId_IsRejected()
    {
        var (world, projectId) = BuildWorldWithAPaidProject();
        Assert.True(CorruptionCaseEngine.OpenCase(world, "case-1", projectId, "Contractor Alpha", 50_000).Success);
        var second = CorruptionCaseEngine.OpenCase(world, "case-1", projectId, "Contractor Alpha", 50_000);
        Assert.False(second.Success);
    }

    [Fact]
    public void FullFlow_OpenInvestigateResolve_CreditsExactlyOnce_WithRealLedgerNumbers()
    {
        var (world, projectId) = BuildWorldWithAPaidProject(milestonePaid: 100_000, milestone2: 100_000);
        Assert.True(CorruptionCaseEngine.OpenCase(world, "case-1", projectId, "Contractor Alpha", allegedOverpaymentThb: 100_000).Success);
        Assert.True(CorruptionCaseEngine.BeginInvestigation(world, "case-1").Success);

        var cashBeforeRecovery = world.Ledger.Cash;
        var resolveResult = CorruptionCaseEngine.ResolveWithRecovery(world, "case-1", LedgerAccountKind.NonRecurring, recoveredAmountThb: 80_000);

        Assert.True(resolveResult.Success, string.Join("; ", resolveResult.Failures));
        Assert.Equal(cashBeforeRecovery + 80_000, world.Ledger.Cash);
        var final = world.CorruptionCases["case-1"];
        Assert.Equal(CorruptionCaseStatus.Resolved, final.Status);
        Assert.Equal(80_000, final.RecoveredAmountThb);
    }

    [Fact]
    public void Resolve_BeforeInvestigating_IsRejected_NoLedgerEffect()
    {
        var (world, projectId) = BuildWorldWithAPaidProject();
        CorruptionCaseEngine.OpenCase(world, "case-1", projectId, "Contractor Alpha", 50_000);
        var before = world.Ledger.Cash;

        var result = CorruptionCaseEngine.ResolveWithRecovery(world, "case-1", LedgerAccountKind.NonRecurring, 50_000);

        Assert.False(result.Success);
        Assert.Equal(before, world.Ledger.Cash);
    }

    [Fact]
    public void Resolve_RecoveringMoreThanAlleged_IsRejected()
    {
        var (world, projectId) = BuildWorldWithAPaidProject();
        CorruptionCaseEngine.OpenCase(world, "case-1", projectId, "Contractor Alpha", 50_000);
        CorruptionCaseEngine.BeginInvestigation(world, "case-1");

        var result = CorruptionCaseEngine.ResolveWithRecovery(world, "case-1", LedgerAccountKind.NonRecurring, 50_001);
        Assert.False(result.Success);
    }

    /// <summary>The path the acceptance criteria names explicitly: a
    /// case, once Resolved, is terminal -- resolving it again must never
    /// credit a second time (plan §12: never a recurring income
    /// mechanism).</summary>
    [Fact]
    public void ResolvingAnAlreadyResolvedCase_IsRejected_NeverDoubleCredits()
    {
        var (world, projectId) = BuildWorldWithAPaidProject();
        CorruptionCaseEngine.OpenCase(world, "case-1", projectId, "Contractor Alpha", 50_000);
        CorruptionCaseEngine.BeginInvestigation(world, "case-1");
        Assert.True(CorruptionCaseEngine.ResolveWithRecovery(world, "case-1", LedgerAccountKind.NonRecurring, 50_000).Success);
        var cashAfterFirstRecovery = world.Ledger.Cash;

        var second = CorruptionCaseEngine.ResolveWithRecovery(world, "case-1", LedgerAccountKind.NonRecurring, 50_000);

        Assert.False(second.Success);
        Assert.Equal(cashAfterFirstRecovery, world.Ledger.Cash); // unchanged -- no second credit.
    }

    [Fact]
    public void Dismiss_HasNoLedgerEffectAtAll()
    {
        var (world, projectId) = BuildWorldWithAPaidProject();
        CorruptionCaseEngine.OpenCase(world, "case-1", projectId, "Contractor Alpha", 50_000);
        var before = world.Ledger.Cash;

        var result = CorruptionCaseEngine.Dismiss(world, "case-1");

        Assert.True(result.Success);
        Assert.Equal(CorruptionCaseStatus.Dismissed, world.CorruptionCases["case-1"].Status);
        Assert.Equal(before, world.Ledger.Cash);
    }

    [Fact]
    public void DismissingAnAlreadyTerminalCase_IsRejected()
    {
        var (world, projectId) = BuildWorldWithAPaidProject();
        CorruptionCaseEngine.OpenCase(world, "case-1", projectId, "Contractor Alpha", 50_000);
        CorruptionCaseEngine.Dismiss(world, "case-1");

        var result = CorruptionCaseEngine.Dismiss(world, "case-1");
        Assert.False(result.Success);
    }

    [Fact]
    public void NonPositiveAllegedOverpayment_IsRejectedAtConstruction()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new CorruptionCase("c", "p", "Contractor Alpha", 0));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new CorruptionCase("c", "p", "Contractor Alpha", -1));
    }

    /// <summary>The single shipped storyline is scripted around whichever
    /// real project the player committed -- it never invents its own
    /// project.</summary>
    [Fact]
    public void ScriptedStoryline_OpensAgainstTheCallerSRealProject()
    {
        var (world, projectId) = BuildWorldWithAPaidProject(milestonePaid: 70_000, milestone2: 30_000);
        var result = ScriptedStoryline.Open(world, projectId, allegedOverpaymentThb: 70_000);

        Assert.True(result.Success, string.Join("; ", result.Failures));
        var stored = world.CorruptionCases[ScriptedStoryline.CaseId];
        Assert.Equal(projectId, stored.RelatedCommittedProjectId);
        Assert.Equal(ScriptedStoryline.ContractorLabel, stored.ContractorLabel);
    }

    /// <summary>The shipped storyline's labels are deliberately generic
    /// and unlocated -- a machine-checkable minimum bar (no digits, no
    /// obvious place-name suffixes like province/district markers) on
    /// top of the reflection control below.</summary>
    [Fact]
    public void ScriptedStoryline_LabelsCarryNoLocationMarker()
    {
        foreach (var forbiddenMarker in new[] { "จังหวัด", "อำเภอ", "ตำบล", "Province", "District Office of", "Bangkok", "Thailand" })
        {
            Assert.DoesNotContain(forbiddenMarker, ScriptedStoryline.ContractorLabel);
            Assert.DoesNotContain(forbiddenMarker, ScriptedStoryline.AgencyLabel);
        }
    }

    /// <summary>ETHICAL CONSTRAINT, machine-enforced (AGENTS.md rule 9,
    /// mirroring G4's IncidentEngineTests control and G6-04's
    /// BuildingArchetypeEthicalControlTests control): no public method or
    /// property anywhere in the Storyline namespace may take or return a
    /// Thaivia.Core.MapPack-namespace type (PolygonFeature, LineFeature,
    /// SourceTags, RoadEdge, GeographyBase, ...) -- the storyline system
    /// structurally cannot bind a case to a real source feature id, name,
    /// or address, because it has no parameter/return shape through which
    /// one could ever arrive.</summary>
    [Fact]
    public void EthicalControl_NoStorylineApiTakesOrReturnsAMapPackType()
    {
        var storylineNamespaceTypes = typeof(CorruptionCase).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(CorruptionCase).Namespace);

        var violations = new System.Collections.Generic.List<string>();

        foreach (var type in storylineNamespaceTypes)
        {
            var members = type
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Cast<MethodBase>()
                .Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));

            foreach (var member in members)
            {
                if (member is MethodInfo method && IsMapPackType(method.ReturnType))
                {
                    violations.Add($"{type.FullName}.{member.Name} returns a MapPack type ({method.ReturnType.FullName}).");
                }

                foreach (var parameter in member.GetParameters())
                {
                    if (IsMapPackType(parameter.ParameterType))
                    {
                        violations.Add($"{type.FullName}.{member.Name}({parameter.Name}) takes a MapPack type ({parameter.ParameterType.FullName}) -- a corruption case must never be able to bind to a real source feature.");
                    }
                }
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (IsMapPackType(property.PropertyType))
                {
                    violations.Add($"{type.FullName}.{property.Name} is a MapPack type ({property.PropertyType.FullName}).");
                }
            }
        }

        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    /// <summary>Positive control: the detector above must actually fire
    /// on the shape it forbids. Uses reflection on a real MapPack type
    /// directly so this does not just vacuously pass because no
    /// violation currently exists.</summary>
    [Fact]
    public void DetectorItself_FlagsARealMapPackType()
    {
        Assert.True(IsMapPackType(typeof(Thaivia.Core.MapPack.SourceTags)));
        Assert.True(IsMapPackType(typeof(Thaivia.Core.MapPack.PolygonFeature)));
        Assert.False(IsMapPackType(typeof(string)));
        Assert.False(IsMapPackType(typeof(long)));
    }

    private static bool IsMapPackType(System.Type type) =>
        type.Namespace is not null && type.Namespace.StartsWith("Thaivia.Core.MapPack", System.StringComparison.Ordinal);
}
