// Pure C# -- no UnityEngine reference. See game/README.md.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Thaivia.Core.MapPack;
using Thaivia.Core.Simulation.Accessibility;
using Thaivia.Core.Simulation.Archetypes;
using Thaivia.Core.Simulation.Buildings;
using Thaivia.Core.Simulation.Cohorts;
using Thaivia.Core.Simulation.Economy;
using Thaivia.Core.Simulation.Mobility.Gateways;
using Thaivia.Core.Simulation.Mobility.Incidents;
using Thaivia.Core.Simulation.Mobility.Queues;
using Thaivia.Core.Simulation.Mobility.RoadWorks;
using Thaivia.Core.Simulation.Mobility.Signals;
using Thaivia.Core.Simulation.Mobility.Transit;
using Thaivia.Core.Simulation.Noise;
using Thaivia.Core.Simulation.Planning;
using Thaivia.Core.Simulation.RandomStreams;
using Thaivia.Core.Simulation.Save;
using Thaivia.Core.Simulation.Scenario;
using SimTime = Thaivia.Core.Simulation.Time;

namespace Thaivia.Core.Simulation;

/// <summary>
/// The in-session combination of an immutable GeographyBase + RoadGraph
/// (from a loaded MapPack), its SimulationInitialization, and everything
/// the G3 simulation core grows on top: a logical tick clock, named RNG
/// streams, an integer money ledger, cohort/building simulation state,
/// committed projects, and player deltas. WorldState *references*
/// GeographyBase/RoadGraph/SimulationInitialization read-only -- it has
/// no method anywhere that mutates any of them, and no method whose name
/// suggests folding a delta back into them (AGENTS.md rule 3: "three
/// layers never mix in one structure, never send PlayerDelta back to
/// OSM"). Everything WorldState itself owns (buildings, cohorts, ledger,
/// projects, clock, RNG) is simulation/PlayerDelta-layer state that is
/// free to change over the session.
/// </summary>
public sealed class WorldState
{
    private readonly List<PlayerDelta> _deltas = new();
    private readonly Dictionary<long, BuildingSimState> _buildingStates;
    private readonly Dictionary<string, HouseholdCohort> _cohorts;
    private readonly Dictionary<string, CommittedProject> _projects = new();
    private readonly List<PlannedRoadSegment> _plannedRoadSegments = new();
    private readonly List<VacatedLot> _vacatedLots = new();
    private readonly HashSet<long> _roadNodeIds;

    // --- G4 mobility state (network queues, gateways, road works, buses,
    // signals, incidents) -- see docs/decisions/0017..0020. Everything
    // here is simulation/PlayerDelta-layer state (never GeographyBase),
    // exposed for direct mutation the same way Clock/RandomStreams/Ledger
    // already are on this type (see class doc comment).
    private readonly Dictionary<long, GatewayFlow> _gatewayFlows;
    private readonly List<RoadWorksZone> _roadWorksZones = new();
    private readonly List<BusRoute> _busRoutes = new();
    private readonly Dictionary<string, long> _busRouteCumulativeRidership = new();
    private readonly List<SignalInstance> _signals = new();
    private readonly List<IncidentSite> _incidentSites = new();

    /// <summary>Normal construction: seeds cohorts/buildings fresh from
    /// GeographyBase + ScenarioConfig (SimulationInitialization-layer
    /// data; see ScenarioConfig's doc comment for why the config, not
    /// GeographyBase, drives this).</summary>
    public WorldState(GeographyBase geographyBase, SimulationInitialization simulationInitialization, RoadGraph roadGraph, ScenarioConfig scenario)
    {
        GeographyBase = geographyBase;
        SimulationInitialization = simulationInitialization;
        RoadGraph = roadGraph;
        Scenario = scenario;
        Deltas = new ReadOnlyCollection<PlayerDelta>(_deltas);

        Clock = new SimTime.SimClock();
        RandomStreams = new NamedRandomStreams(scenario.MasterSeed);
        Ledger = new MoneyLedger(scenario.InitialCashThb);
        Revision = 0;

        _roadNodeIds = roadGraph.Nodes.Select(n => n.NodeId).ToHashSet();
        (_buildingStates, _cohorts) = SeedFromGeography(geographyBase, roadGraph, scenario, RandomStreams);

        LinkQueues = new LinkQueueSimulator();
        _gatewayFlows = BuildGatewayFlowsFromRoadGraph(roadGraph);
    }

    /// <summary>Restore construction: used only by
    /// <see cref="Restore"/> to rebuild a WorldState from a
    /// <see cref="SaveGame"/> instead of seeding fresh state.</summary>
    private WorldState(
        GeographyBase geographyBase,
        SimulationInitialization simulationInitialization,
        RoadGraph roadGraph,
        ScenarioConfig scenario,
        SaveGame save)
    {
        GeographyBase = geographyBase;
        SimulationInitialization = simulationInitialization;
        RoadGraph = roadGraph;
        Scenario = scenario;
        Deltas = new ReadOnlyCollection<PlayerDelta>(_deltas);

        Clock = new SimTime.SimClock();
        Clock.AdvanceTicks(save.CurrentTick);
        RandomStreams = NamedRandomStreams.Restore(save.MasterSeed, save.RngStates, save.RngDrawCounts);
        Ledger = MoneyLedger.Restore(save.CashByKind, save.ReservedByKind);
        Revision = save.Revision;

        _roadNodeIds = roadGraph.Nodes.Select(n => n.NodeId).ToHashSet();

        _buildingStates = save.Buildings.ToDictionary(
            b => b.SourceId,
            b => new BuildingSimState(b.SourceId, Enum.Parse<BuildingArchetype>(b.Archetype), b.LocalX, b.LocalZ, b.NearestRoadNodeId, b.JobsCount, b.Relocated));

        _cohorts = save.Cohorts.ToDictionary(
            c => c.Id,
            c => new HouseholdCohort(c.Id, c.HomeBuildingSourceId, c.HouseholdCount, c.PeoplePerHousehold, c.JobsHeld));

        foreach (var p in save.Projects)
        {
            _projects[p.Id] = new CommittedProject(
                p.Id,
                Enum.Parse<ProjectKind>(p.Kind),
                Enum.Parse<LedgerAccountKind>(p.LedgerKind),
                p.FixedCostThb,
                p.MilestoneAmounts,
                p.PaidMilestones,
                p.TotalPaid,
                Enum.Parse<ProjectStatus>(p.Status));
        }

        foreach (var s in save.PlannedRoadSegments)
        {
            _plannedRoadSegments.Add(new PlannedRoadSegment(s.FromNodeId, s.ToNodeId, s.LengthMeters, s.ProjectId));
        }

        foreach (var v in save.VacatedLots)
        {
            _vacatedLots.Add(new VacatedLot(v.BuildingSourceId, v.OldLocalX, v.OldLocalZ, v.ProjectId));
        }

        var (linkQueueLengths, linkQueueCompleted, linkQueueArrived) = SplitLinkQueues(save.LinkQueues);
        LinkQueues = LinkQueueSimulator.Restore(linkQueueLengths, linkQueueCompleted, linkQueueArrived);

        _gatewayFlows = new Dictionary<long, GatewayFlow>();
        foreach (var g in save.GatewayFlows)
        {
            _gatewayFlows[g.GatewayNodeId] = new GatewayFlow(
                g.GatewayNodeId, g.InboundCapacityPerTick, g.OutboundCapacityPerTick, g.IsOpen,
                g.GeneratedOutbound, g.CompletedOutbound, g.PendingOutbound,
                g.GeneratedInbound, g.CompletedInbound, g.PendingInbound);
        }

        foreach (var rw in save.RoadWorksZones)
        {
            _roadWorksZones.Add(new RoadWorksZone(rw.Id, rw.WayId, rw.StartTick, rw.DurationTicks, rw.CapacityMultiplierDuringConstruction, rw.ProjectId));
        }

        foreach (var b in save.BusRoutes)
        {
            _busRoutes.Add(new BusRoute(b.Id, b.StopNodeIds, b.DwellTicksPerStop, b.VehicleCount, b.CapacityPerVehicle));
            _busRouteCumulativeRidership[b.Id] = b.CumulativeRidership;
        }

        foreach (var s in save.Signals)
        {
            _signals.Add(new SignalInstance(
                s.Id, s.NodeId, Enum.Parse<SignalPlanKind>(s.Kind), s.CycleTicks, s.ConfigValue, s.DischargeRatePerGreenTick,
                s.QueueA, s.QueueB, s.CurrentGreenTicksA, s.CurrentGreenTicksB, s.CumulativeQueueTicksA, s.CumulativeQueueTicksB, s.TicksSimulated));
        }

        foreach (var inc in save.IncidentSites)
        {
            _incidentSites.Add(new IncidentSite(
                inc.SiteId, Enum.Parse<IncidentStrand>(inc.Strand), Enum.Parse<IncidentPhase>(inc.Phase),
                inc.TicksInPhase, inc.WarningsIssued, inc.IncidentsTriggered, inc.LastSeverity));
        }
    }

    public GeographyBase GeographyBase { get; }
    public SimulationInitialization SimulationInitialization { get; }

    /// <summary>The loaded MapPack's road_graph -- same immutability
    /// guarantee as GeographyBase: WorldState never mutates it, only
    /// reads node/edge geometry from it (see
    /// <see cref="BuildAccessibilityGraph"/>).</summary>
    public RoadGraph RoadGraph { get; }

    public ScenarioConfig Scenario { get; }
    public IReadOnlyList<PlayerDelta> Deltas { get; }
    public SimTime.SimClock Clock { get; }
    public NamedRandomStreams RandomStreams { get; }
    public MoneyLedger Ledger { get; }
    public long Revision { get; private set; }

    public IReadOnlyDictionary<long, BuildingSimState> BuildingStates => _buildingStates;
    public IReadOnlyDictionary<string, HouseholdCohort> Cohorts => _cohorts;
    public IReadOnlyDictionary<string, CommittedProject> Projects => _projects;
    public IReadOnlyList<PlannedRoadSegment> PlannedRoadSegments => _plannedRoadSegments;
    public IReadOnlyList<VacatedLot> VacatedLots => _vacatedLots;
    public IReadOnlySet<long> RoadNodeIds => _roadNodeIds;

    // --- G4 mobility state: exposed for direct mutation, same as
    // Clock/RandomStreams (none of these carry a budget/transactional
    // concern the way MoneyLedger's Reserve/Charge does, so there is no
    // need to funnel them through PlanningEngine).
    public LinkQueueSimulator LinkQueues { get; }
    public IReadOnlyDictionary<long, GatewayFlow> GatewayFlows => _gatewayFlows;
    public IReadOnlyList<RoadWorksZone> RoadWorksZones => _roadWorksZones;
    public IReadOnlyList<BusRoute> BusRoutes => _busRoutes;
    public IReadOnlyList<SignalInstance> Signals => _signals;
    public IReadOnlyList<IncidentSite> IncidentSites => _incidentSites;

    public void AddRoadWorksZone(RoadWorksZone zone) => _roadWorksZones.Add(zone);
    public void AddBusRoute(BusRoute route) => _busRoutes.Add(route);
    public void AddSignal(SignalInstance signal) => _signals.Add(signal);
    public void AddIncidentSite(IncidentSite site) => _incidentSites.Add(site);

    public long BusRidershipOf(string routeId) => _busRouteCumulativeRidership.TryGetValue(routeId, out var v) ? v : 0;

    /// <summary>Records ridership boarded this tick for a route (see
    /// <see cref="Transit.BusRidership.AssignRidership"/>) -- accumulates,
    /// never replaces, so this is safe to call once per tick per
    /// route.</summary>
    public void RecordBusRidership(string routeId, long boarded)
    {
        if (boarded < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(boarded));
        }

        _busRouteCumulativeRidership[routeId] = BusRidershipOf(routeId) + boarded;
    }

    public long TotalPopulation => _cohorts.Values.Sum(c => c.PopulationCount);
    public long TotalJobs => _buildingStates.Values.Sum(b => (long)b.JobsCount);

    /// <summary>Appends a player delta. There is deliberately no method on
    /// this class, or on GeographyBase, that lets a delta be folded back
    /// into GeographyBase's source-tag data -- a delta stays a delta.</summary>
    public void AddDelta(PlayerDelta delta) => _deltas.Add(delta);

    // --- Internal mutation surface for Thaivia.Core.Simulation.Planning.PlanningEngine ---
    // Kept internal (not public) so the only code path that can reserve
    // budget / apply geometry effects / bump the revision counter is the
    // reviewed transactional logic in PlanningEngine, not an ad hoc caller.

    internal void IncrementRevision() => Revision++;

    internal void RegisterProject(CommittedProject project) => _projects[project.Id] = project;

    internal void RegisterPlannedRoadSegment(PlannedRoadSegment segment) => _plannedRoadSegments.Add(segment);

    internal void ReplaceBuildingState(BuildingSimState updated) => _buildingStates[updated.SourceId] = updated;

    internal void RegisterVacatedLot(VacatedLot lot) => _vacatedLots.Add(lot);

    /// <summary>Advances the logical clock by exactly
    /// <paramref name="tickCount"/> ticks. No wall-clock parameter exists
    /// anywhere in this call chain (see SimClock's doc comment).</summary>
    public void Tick(long tickCount) => Clock.AdvanceTicks(tickCount);

    /// <summary>One fixed-step simulation tick: advances the clock by one
    /// tick and draws one value from the Traffic stream. G3 does not yet
    /// drive real per-tick traffic simulation from this draw (full
    /// mobility/queues is G4 scope, plan §16) -- it exists here purely so
    /// tick advancement is entangled with RNG stream position, which is
    /// what the save/load round-trip test exercises.</summary>
    public void SimulateTick()
    {
        Clock.AdvanceTicks(1);
        RandomStreams.Stream(RandomStreamName.Traffic).NextUInt64();
    }

    /// <summary>Builds a fresh accessibility graph combining the loaded
    /// MapPack's road_graph with every player-committed
    /// <see cref="PlannedRoadSegment"/> so far. Pure/read-only: never
    /// mutates RoadGraph or WorldState, safe to call from Estimate-style
    /// code paths that must not touch the world.</summary>
    public AccessibilityGraph BuildAccessibilityGraph() => new(RoadGraph, _plannedRoadSegments);

    /// <summary>Network-graph accessibility score (0-100, see
    /// AccessibilityNeed) from a building to the nearest job-bearing
    /// building, or 0 if no job-bearing building exists or none is
    /// reachable at all.</summary>
    public int ComputeAccessibilityScore(long buildingSourceId)
    {
        if (!_buildingStates.TryGetValue(buildingSourceId, out var building))
        {
            throw new ArgumentException($"Unknown building source id {buildingSourceId}.", nameof(buildingSourceId));
        }

        var graph = BuildAccessibilityGraph();
        var nearestJobNode = FindNearestJobNode(graph, building.NearestRoadNodeId, excludeBuildingSourceId: buildingSourceId);
        var distance = nearestJobNode is { } nodeId ? graph.ShortestDistanceMeters(building.NearestRoadNodeId, nodeId) : null;
        return AccessibilityNeed.ComputeScore(distance);
    }

    /// <summary>Finds the job-bearing building's nearest road node closest
    /// (by network distance) to <paramref name="fromNodeId"/>. Pure/read-only.</summary>
    public long? FindNearestJobNode(AccessibilityGraph graph, long fromNodeId, long? excludeBuildingSourceId = null)
    {
        long? best = null;
        double bestDistance = double.PositiveInfinity;
        foreach (var b in _buildingStates.Values)
        {
            if (b.JobsCount <= 0 || b.SourceId == excludeBuildingSourceId)
            {
                continue;
            }

            var d = graph.ShortestDistanceMeters(fromNodeId, b.NearestRoadNodeId);
            if (d is { } value && value < bestDistance)
            {
                bestDistance = value;
                best = b.NearestRoadNodeId;
            }
        }

        return best;
    }

    /// <summary>Noise index (0-100, of the game -- see NoiseIndex) at a
    /// building's current location, for the given hour, from every other
    /// building's activity clock reading.</summary>
    public int ComputeNoiseIndexAt(long buildingSourceId, int hourOfDay)
    {
        if (!_buildingStates.TryGetValue(buildingSourceId, out var target))
        {
            throw new ArgumentException($"Unknown building source id {buildingSourceId}.", nameof(buildingSourceId));
        }

        var sources = _buildingStates.Values
            .Where(b => b.SourceId != buildingSourceId)
            .Select(b => new NoiseIndex.NoiseSource(b.Archetype, b.LocalX, b.LocalZ));

        return NoiseIndex.ComputeAt(target.LocalX, target.LocalZ, hourOfDay, sources);
    }

    public CohortNeeds ComputeCohortNeeds(string cohortId, int hourOfDay)
    {
        if (!_cohorts.TryGetValue(cohortId, out var cohort))
        {
            throw new ArgumentException($"Unknown cohort id {cohortId}.", nameof(cohortId));
        }

        var noise = ComputeNoiseIndexAt(cohort.HomeBuildingSourceId, hourOfDay);
        var access = ComputeAccessibilityScore(cohort.HomeBuildingSourceId);
        var graph = BuildAccessibilityGraph();
        var home = _buildingStates[cohort.HomeBuildingSourceId];
        var reachableJobs = _buildingStates.Values.Count(b => b.JobsCount > 0
            && graph.ShortestDistanceMeters(home.NearestRoadNodeId, b.NearestRoadNodeId) is { } d
            && d <= AccessibilityNeed.ReferenceDistanceMeters * 2);

        return CohortNeedsCalculator.Compute(noise, access, reachableJobs);
    }

    /// <summary>A deterministic structural fingerprint of everything that
    /// makes two WorldStates "the same simulation state": clock, revision,
    /// every named RNG stream's exact state+draw count, the ledger,
    /// every building/cohort/project/planned segment/vacated lot sorted by
    /// a stable key. Same seed + same committed command sequence must
    /// produce the same hash (see WorldStateDeterminismTests) -- and the
    /// same holds across a save/restore round trip (see
    /// SaveLoadRoundTripTests), because everything this hash reads is
    /// exactly what CaptureSave/Restore carry over.</summary>
    public string ComputeStructuralHash()
    {
        var sb = new StringBuilder();
        sb.Append("tick=").Append(Clock.CurrentTick).Append(';');
        sb.Append("rev=").Append(Revision).Append(';');

        foreach (var name in ((RandomStreamName[])Enum.GetValues(typeof(RandomStreamName))).OrderBy(n => n.ToString()))
        {
            var stream = RandomStreams.Stream(name);
            sb.Append("rng.").Append(name).Append('=').Append(stream.State).Append(':').Append(stream.DrawCount).Append(';');
        }

        foreach (var kind in ((LedgerAccountKind[])Enum.GetValues(typeof(LedgerAccountKind))).OrderBy(k => k.ToString()))
        {
            sb.Append("cash.").Append(kind).Append('=').Append(Ledger.CashOf(kind)).Append(';');
            sb.Append("reserved.").Append(kind).Append('=').Append(Ledger.ReservedOf(kind)).Append(';');
        }

        foreach (var b in _buildingStates.Values.OrderBy(b => b.SourceId))
        {
            sb.Append("bld.").Append(b.SourceId).Append('=')
                .Append(b.Archetype).Append(',').Append(b.LocalX).Append(',').Append(b.LocalZ).Append(',')
                .Append(b.NearestRoadNodeId).Append(',').Append(b.JobsCount).Append(',').Append(b.Relocated).Append(';');
        }

        foreach (var c in _cohorts.Values.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            sb.Append("coh.").Append(c.Id).Append('=')
                .Append(c.HomeBuildingSourceId).Append(',').Append(c.HouseholdCount).Append(',')
                .Append(c.PeoplePerHousehold).Append(',').Append(c.JobsHeld).Append(';');
        }

        foreach (var p in _projects.Values.OrderBy(p => p.Id, StringComparer.Ordinal))
        {
            sb.Append("prj.").Append(p.Id).Append('=')
                .Append(p.Status).Append(',').Append(p.PaidMilestones).Append(',').Append(p.TotalPaid).Append(';');
        }

        foreach (var s in _plannedRoadSegments.OrderBy(s => s.ProjectId, StringComparer.Ordinal))
        {
            sb.Append("road.").Append(s.ProjectId).Append('=')
                .Append(s.FromNodeId).Append(',').Append(s.ToNodeId).Append(',').Append(s.LengthMeters).Append(';');
        }

        foreach (var v in _vacatedLots.OrderBy(v => v.ProjectId, StringComparer.Ordinal))
        {
            sb.Append("vac.").Append(v.ProjectId).Append('=')
                .Append(v.BuildingSourceId).Append(',').Append(v.OldLocalX).Append(',').Append(v.OldLocalZ).Append(';');
        }

        // --- G4 mobility state: every field the same commands could ever
        // change must be represented here, or two worlds built from the
        // same seed + same commands could hash identically while actually
        // differing in mobility state (see determinism tests).
        foreach (var g in _gatewayFlows.Values.OrderBy(g => g.GatewayNodeId))
        {
            sb.Append("gw.").Append(g.GatewayNodeId).Append('=')
                .Append(g.IsOpen).Append(',').Append(g.GeneratedOutbound).Append(',').Append(g.CompletedOutbound).Append(',').Append(g.PendingOutbound).Append(',')
                .Append(g.GeneratedInbound).Append(',').Append(g.CompletedInbound).Append(',').Append(g.PendingInbound).Append(';');
        }

        var linkQueueLengths = LinkQueues.QueueLengths;
        var linkQueueCompleted = LinkQueues.TotalCompleted;
        var linkQueueArrived = LinkQueues.TotalArrived;
        foreach (var key in linkQueueLengths.Keys.OrderBy(k => k.WayId).ThenBy(k => k.Forward))
        {
            sb.Append("lq.").Append(key.WayId).Append(key.Forward ? 'F' : 'R').Append('=')
                .Append(linkQueueLengths[key]).Append(',')
                .Append(linkQueueCompleted.TryGetValue(key, out var completed) ? completed : 0).Append(',')
                .Append(linkQueueArrived.TryGetValue(key, out var arrived) ? arrived : 0).Append(';');
        }

        foreach (var z in _roadWorksZones.OrderBy(z => z.Id, StringComparer.Ordinal))
        {
            sb.Append("rw.").Append(z.Id).Append('=')
                .Append(z.WayId).Append(',').Append(z.StartTick).Append(',').Append(z.DurationTicks).Append(',').Append(z.CapacityMultiplierDuringConstruction).Append(';');
        }

        foreach (var route in _busRoutes.OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            sb.Append("bus.").Append(route.Id).Append('=').Append(BusRidershipOf(route.Id)).Append(';');
        }

        foreach (var sig in _signals.OrderBy(s => s.Id, StringComparer.Ordinal))
        {
            sb.Append("sig.").Append(sig.Id).Append('=')
                .Append(sig.Simulator.QueueA).Append(',').Append(sig.Simulator.QueueB).Append(',')
                .Append(sig.Simulator.CurrentGreenTicksA).Append(',').Append(sig.Simulator.CurrentGreenTicksB).Append(',')
                .Append(sig.Simulator.CumulativeQueueTicksA).Append(',').Append(sig.Simulator.CumulativeQueueTicksB).Append(',')
                .Append(sig.Simulator.TicksSimulated).Append(';');
        }

        foreach (var site in _incidentSites.OrderBy(i => i.SiteId, StringComparer.Ordinal).ThenBy(i => i.Strand.ToString(), StringComparer.Ordinal))
        {
            sb.Append("inc.").Append(site.SiteId).Append('.').Append(site.Strand).Append('=')
                .Append(site.Phase).Append(',').Append(site.TicksInPhase).Append(',').Append(site.WarningsIssued).Append(',')
                .Append(site.IncidentsTriggered).Append(',').Append(site.LastSeverity).Append(';');
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    /// <summary>Rebuilds a WorldState from a save file plus the (freshly
    /// loaded, byte-identical) MapPack the save says it belongs to.
    /// Callers are expected to have already checked mapId/mapContentHash
    /// (see Thaivia.Core.Simulation.Save.SaveGameStore.LoadLatest, which
    /// returns an explicit MapVersionMismatch result rather than letting
    /// this method silently load against the wrong map).</summary>
    public static WorldState Restore(SaveGame save, GeographyBase geographyBase, SimulationInitialization simulationInitialization, RoadGraph roadGraph, ScenarioConfig scenario) =>
        new(geographyBase, simulationInitialization, roadGraph, scenario, save);

    public SaveGame CaptureSave(string mapId, string mapContentHash, string simulationVersion, string contentVersion) =>
        new(
            mapId,
            mapContentHash,
            simulationVersion,
            contentVersion,
            Clock.CurrentTick,
            Revision,
            RandomStreams.MasterSeed,
            RandomStreams.CaptureStates(),
            RandomStreams.CaptureDrawCounts(),
            Ledger.CaptureCashByKind(),
            Ledger.CaptureReservedByKind(),
            _cohorts.Values.Select(c => new SavedCohort(c.Id, c.HomeBuildingSourceId, c.HouseholdCount, c.PeoplePerHousehold, c.JobsHeld)).ToList(),
            _buildingStates.Values.Select(b => new SavedBuilding(b.SourceId, b.Archetype.ToString(), b.LocalX, b.LocalZ, b.NearestRoadNodeId, b.JobsCount, b.Relocated)).ToList(),
            _projects.Values.Select(p => new SavedProject(p.Id, p.Kind.ToString(), p.LedgerKind.ToString(), p.FixedCostThb, p.MilestoneAmounts.ToList(), p.PaidMilestones, p.TotalPaid, p.Status.ToString())).ToList(),
            _plannedRoadSegments.Select(s => new SavedRoadSegment(s.FromNodeId, s.ToNodeId, s.LengthMeters, s.ProjectId)).ToList(),
            _vacatedLots.Select(v => new SavedVacatedLot(v.BuildingSourceId, v.OldLocalX, v.OldLocalZ, v.ProjectId)).ToList(),
            CaptureLinkQueues(),
            _gatewayFlows.Values.Select(g => new SavedGatewayFlow(
                g.GatewayNodeId, g.InboundCapacityPerTick, g.OutboundCapacityPerTick, g.IsOpen,
                g.GeneratedOutbound, g.CompletedOutbound, g.PendingOutbound,
                g.GeneratedInbound, g.CompletedInbound, g.PendingInbound)).ToList(),
            _roadWorksZones.Select(z => new SavedRoadWorksZone(z.Id, z.WayId, z.StartTick, z.DurationTicks, z.CapacityMultiplierDuringConstruction, z.ProjectId)).ToList(),
            _busRoutes.Select(b => new SavedBusRoute(b.Id, b.StopNodeIds, b.DwellTicksPerStop, b.VehicleCount, b.CapacityPerVehicle, BusRidershipOf(b.Id))).ToList(),
            _signals.Select(s => new SavedSignal(
                s.Id, s.NodeId, s.Kind.ToString(), s.CycleTicks, s.ConfigValue, s.DischargeRatePerGreenTick,
                s.Simulator.QueueA, s.Simulator.QueueB, s.Simulator.CurrentGreenTicksA, s.Simulator.CurrentGreenTicksB,
                s.Simulator.CumulativeQueueTicksA, s.Simulator.CumulativeQueueTicksB, s.Simulator.TicksSimulated)).ToList(),
            _incidentSites.Select(i => new SavedIncidentSite(i.SiteId, i.Strand.ToString(), i.Phase.ToString(), i.TicksInPhase, i.WarningsIssued, i.IncidentsTriggered, i.LastSeverity)).ToList());

    private List<SavedLinkQueue> CaptureLinkQueues()
    {
        var lengths = LinkQueues.QueueLengths;
        var completed = LinkQueues.TotalCompleted;
        var arrived = LinkQueues.TotalArrived;
        return lengths.Keys.Select(k => new SavedLinkQueue(
            k.WayId, k.Forward,
            lengths[k],
            completed.TryGetValue(k, out var c) ? c : 0,
            arrived.TryGetValue(k, out var a) ? a : 0)).ToList();
    }

    private static (Dictionary<LinkKey, long> Lengths, Dictionary<LinkKey, long> Completed, Dictionary<LinkKey, long> Arrived) SplitLinkQueues(IReadOnlyList<SavedLinkQueue> saved)
    {
        var lengths = new Dictionary<LinkKey, long>();
        var completed = new Dictionary<LinkKey, long>();
        var arrived = new Dictionary<LinkKey, long>();
        foreach (var q in saved)
        {
            var key = new LinkKey(q.WayId, q.Forward);
            lengths[key] = q.QueueLength;
            completed[key] = q.TotalCompleted;
            arrived[key] = q.TotalArrived;
        }

        return (lengths, completed, arrived);
    }

    /// <summary>Builds one <see cref="GatewayFlow"/> per
    /// <see cref="RoadGraph.Gateways"/> entry, deriving per-tick capacity
    /// from the MapPack's `external_capacity_veh_per_hour`
    /// simulation_assumption field (see <see cref="Gateway"/>'s doc
    /// comment -- it is already documented as never a source fact). Every
    /// gateway starts open.</summary>
    private static Dictionary<long, GatewayFlow> BuildGatewayFlowsFromRoadGraph(RoadGraph roadGraph)
    {
        var flows = new Dictionary<long, GatewayFlow>();
        foreach (var gateway in roadGraph.Gateways)
        {
            var capacityPerTick = VehPerHourToPerTick(gateway.ExternalCapacityVehPerHour);
            flows[gateway.NodeId] = new GatewayFlow(gateway.NodeId, capacityPerTick, capacityPerTick);
        }

        return flows;
    }

    private static int VehPerHourToPerTick(int vehPerHour)
    {
        var ticksPerHour = SimTime.TickConfig.TicksPerSecond * 3600.0;
        return Math.Max(0, (int)Math.Round(vehPerHour / ticksPerHour));
    }

    private static (Dictionary<long, BuildingSimState>, Dictionary<string, HouseholdCohort>) SeedFromGeography(
        GeographyBase geographyBase, RoadGraph roadGraph, ScenarioConfig scenario, NamedRandomStreams randomStreams)
    {
        var buildingStates = new Dictionary<long, BuildingSimState>();
        var cohorts = new Dictionary<string, HouseholdCohort>();
        var cohortStream = randomStreams.Stream(RandomStreamName.CohortVariation);

        foreach (var building in geographyBase.Buildings)
        {
            if (building.RingGroupsCanonical.Count == 0 || building.RingGroupsCanonical[0].Outer.Count == 0)
            {
                // No geometry to place this building at -- skip rather
                // than guess a location (mirrors "no data = unknown, not
                // an assumed location" for the parts of a feature this
                // simulation layer needs a coordinate for).
                continue;
            }

            var (cx, cz) = Centroid(building.RingGroupsCanonical[0].Outer);
            var nearestNode = NearestRoadNode(roadGraph, cx, cz);
            if (nearestNode is null)
            {
                continue;
            }

            var archetype = AssignArchetype(building.SourceId);
            var jobs = archetype == BuildingArchetype.Residential ? 0 : scenario.JobsPerNonResidentialBuilding;
            buildingStates[building.SourceId] = new BuildingSimState(building.SourceId, archetype, cx, cz, nearestNode.Value, jobs, relocated: false);

            if (archetype == BuildingArchetype.Residential)
            {
                var householdCount = cohortStream.NextInt(scenario.MinHouseholdsPerResidentialBuilding, scenario.MaxHouseholdsPerResidentialBuilding + 1);
                var cohortId = $"cohort-{building.SourceId}";
                cohorts[cohortId] = new HouseholdCohort(cohortId, building.SourceId, householdCount, scenario.PeoplePerHousehold, jobsHeld: 0);
            }
        }

        return (buildingStates, cohorts);
    }

    /// <summary>Deterministic archetype assignment from a building's
    /// stable source id -- a pure hash, NOT an RNG draw, so it never
    /// depends on call order relative to other RNG consumption (see
    /// DeterministicRandom.HashStep's doc comment).</summary>
    public static BuildingArchetype AssignArchetype(long sourceId)
    {
        var values = (BuildingArchetype[])Enum.GetValues(typeof(BuildingArchetype));
        var mixed = Thaivia.Core.Simulation.RandomStreams.DeterministicRandom.HashStep(unchecked((ulong)sourceId));
        return values[(int)(mixed % (ulong)values.Length)];
    }

    private static (double X, double Z) Centroid(IReadOnlyList<Vec2> outer)
    {
        double sx = 0, sz = 0;
        foreach (var p in outer)
        {
            sx += p.X;
            sz += p.Z;
        }

        return (sx / outer.Count, sz / outer.Count);
    }

    private static long? NearestRoadNode(RoadGraph roadGraph, double x, double z)
    {
        long? best = null;
        var bestDistanceSq = double.PositiveInfinity;
        foreach (var node in roadGraph.Nodes)
        {
            var dx = node.LocalX - x;
            var dz = node.LocalZ - z;
            var distSq = dx * dx + dz * dz;
            if (distSq < bestDistanceSq)
            {
                bestDistanceSq = distSq;
                best = node.NodeId;
            }
        }

        return best;
    }
}
