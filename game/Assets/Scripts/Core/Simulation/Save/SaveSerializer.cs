// Pure C# -- no UnityEngine reference. See game/README.md.
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Thaivia.Core.Serialization;
using Thaivia.Core.Simulation.Economy;
using Thaivia.Core.Simulation.RandomStreams;

namespace Thaivia.Core.Simulation.Save;

/// <summary>
/// Hand-written JSON read/write for <see cref="SaveGame"/>, in the same
/// strict style as <see cref="Thaivia.Core.Serialization.MapPackLoader"/>
/// (every required field goes through <see cref="JsonRequire"/>, so a
/// truncated/malformed save file throws a specific, catchable exception
/// instead of System.Text.Json's default attribute-based leniency masking
/// a corrupt file as a half-populated object). Deliberately not
/// System.Text.Json's `JsonSerializer.Serialize/Deserialize` against
/// SaveGame directly: SaveGame's properties are get-only (no
/// parameterless constructor / settable properties), consistent with
/// every other type in this codebase, and this keeps that consistent
/// rather than adding a second, laxer, attribute-driven code path.
/// </summary>
public static class SaveSerializer
{
    public static string Serialize(SaveGame save)
    {
        using var stream = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("map_id", save.MapId);
            writer.WriteString("map_content_hash", save.MapContentHash);
            writer.WriteString("simulation_version", save.SimulationVersion);
            writer.WriteString("content_version", save.ContentVersion);
            writer.WriteNumber("current_tick", save.CurrentTick);
            writer.WriteNumber("revision", save.Revision);
            writer.WriteNumber("master_seed", save.MasterSeed);

            writer.WriteStartObject("rng_states");
            foreach (var (name, state) in save.RngStates)
            {
                writer.WriteString(name.ToString(), state.ToString());
            }

            writer.WriteEndObject();

            writer.WriteStartObject("rng_draw_counts");
            foreach (var (name, count) in save.RngDrawCounts)
            {
                writer.WriteNumber(name.ToString(), count);
            }

            writer.WriteEndObject();

            writer.WriteStartObject("cash_by_kind");
            foreach (var (kind, amount) in save.CashByKind)
            {
                writer.WriteNumber(kind.ToString(), amount);
            }

            writer.WriteEndObject();

            writer.WriteStartObject("reserved_by_kind");
            foreach (var (kind, amount) in save.ReservedByKind)
            {
                writer.WriteNumber(kind.ToString(), amount);
            }

            writer.WriteEndObject();

            writer.WriteStartArray("cohorts");
            foreach (var c in save.Cohorts)
            {
                writer.WriteStartObject();
                writer.WriteString("id", c.Id);
                writer.WriteNumber("home_building_source_id", c.HomeBuildingSourceId);
                writer.WriteNumber("household_count", c.HouseholdCount);
                writer.WriteNumber("people_per_household", c.PeoplePerHousehold);
                writer.WriteNumber("jobs_held", c.JobsHeld);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteStartArray("buildings");
            foreach (var b in save.Buildings)
            {
                writer.WriteStartObject();
                writer.WriteNumber("source_id", b.SourceId);
                writer.WriteString("archetype", b.Archetype);
                writer.WriteNumber("local_x", b.LocalX);
                writer.WriteNumber("local_z", b.LocalZ);
                writer.WriteNumber("nearest_road_node_id", b.NearestRoadNodeId);
                writer.WriteNumber("jobs_count", b.JobsCount);
                writer.WriteBoolean("relocated", b.Relocated);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteStartArray("projects");
            foreach (var p in save.Projects)
            {
                writer.WriteStartObject();
                writer.WriteString("id", p.Id);
                writer.WriteString("kind", p.Kind);
                writer.WriteString("ledger_kind", p.LedgerKind);
                writer.WriteNumber("fixed_cost_thb", p.FixedCostThb);
                writer.WriteStartArray("milestone_amounts");
                foreach (var m in p.MilestoneAmounts)
                {
                    writer.WriteNumberValue(m);
                }

                writer.WriteEndArray();
                writer.WriteNumber("paid_milestones", p.PaidMilestones);
                writer.WriteNumber("total_paid", p.TotalPaid);
                writer.WriteString("status", p.Status);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteStartArray("planned_road_segments");
            foreach (var s in save.PlannedRoadSegments)
            {
                writer.WriteStartObject();
                writer.WriteNumber("from_node_id", s.FromNodeId);
                writer.WriteNumber("to_node_id", s.ToNodeId);
                writer.WriteNumber("length_meters", s.LengthMeters);
                writer.WriteString("project_id", s.ProjectId);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteStartArray("vacated_lots");
            foreach (var v in save.VacatedLots)
            {
                writer.WriteStartObject();
                writer.WriteNumber("building_source_id", v.BuildingSourceId);
                writer.WriteNumber("old_local_x", v.OldLocalX);
                writer.WriteNumber("old_local_z", v.OldLocalZ);
                writer.WriteString("project_id", v.ProjectId);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteStartArray("link_queues");
            foreach (var q in save.LinkQueues)
            {
                writer.WriteStartObject();
                writer.WriteNumber("way_id", q.WayId);
                writer.WriteBoolean("forward", q.Forward);
                writer.WriteNumber("queue_length", q.QueueLength);
                writer.WriteNumber("total_completed", q.TotalCompleted);
                writer.WriteNumber("total_arrived", q.TotalArrived);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteStartArray("gateway_flows");
            foreach (var g in save.GatewayFlows)
            {
                writer.WriteStartObject();
                writer.WriteNumber("gateway_node_id", g.GatewayNodeId);
                writer.WriteNumber("inbound_capacity_per_tick", g.InboundCapacityPerTick);
                writer.WriteNumber("outbound_capacity_per_tick", g.OutboundCapacityPerTick);
                writer.WriteBoolean("is_open", g.IsOpen);
                writer.WriteNumber("generated_outbound", g.GeneratedOutbound);
                writer.WriteNumber("completed_outbound", g.CompletedOutbound);
                writer.WriteNumber("pending_outbound", g.PendingOutbound);
                writer.WriteNumber("generated_inbound", g.GeneratedInbound);
                writer.WriteNumber("completed_inbound", g.CompletedInbound);
                writer.WriteNumber("pending_inbound", g.PendingInbound);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteStartArray("road_works_zones");
            foreach (var rw in save.RoadWorksZones)
            {
                writer.WriteStartObject();
                writer.WriteString("id", rw.Id);
                writer.WriteNumber("way_id", rw.WayId);
                writer.WriteNumber("start_tick", rw.StartTick);
                writer.WriteNumber("duration_ticks", rw.DurationTicks);
                writer.WriteNumber("capacity_multiplier_during_construction", rw.CapacityMultiplierDuringConstruction);
                writer.WriteString("project_id", rw.ProjectId);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteStartArray("bus_routes");
            foreach (var b in save.BusRoutes)
            {
                writer.WriteStartObject();
                writer.WriteString("id", b.Id);
                writer.WriteStartArray("stop_node_ids");
                foreach (var stop in b.StopNodeIds)
                {
                    writer.WriteNumberValue(stop);
                }

                writer.WriteEndArray();
                writer.WriteNumber("dwell_ticks_per_stop", b.DwellTicksPerStop);
                writer.WriteNumber("vehicle_count", b.VehicleCount);
                writer.WriteNumber("capacity_per_vehicle", b.CapacityPerVehicle);
                writer.WriteNumber("cumulative_ridership", b.CumulativeRidership);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteStartArray("signals");
            foreach (var s in save.Signals)
            {
                writer.WriteStartObject();
                writer.WriteString("id", s.Id);
                writer.WriteNumber("node_id", s.NodeId);
                writer.WriteString("kind", s.Kind);
                writer.WriteNumber("cycle_ticks", s.CycleTicks);
                writer.WriteNumber("config_value", s.ConfigValue);
                writer.WriteNumber("discharge_rate_per_green_tick", s.DischargeRatePerGreenTick);
                writer.WriteNumber("queue_a", s.QueueA);
                writer.WriteNumber("queue_b", s.QueueB);
                writer.WriteNumber("current_green_ticks_a", s.CurrentGreenTicksA);
                writer.WriteNumber("current_green_ticks_b", s.CurrentGreenTicksB);
                writer.WriteNumber("cumulative_queue_ticks_a", s.CumulativeQueueTicksA);
                writer.WriteNumber("cumulative_queue_ticks_b", s.CumulativeQueueTicksB);
                writer.WriteNumber("ticks_simulated", s.TicksSimulated);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteStartArray("incident_sites");
            foreach (var inc in save.IncidentSites)
            {
                writer.WriteStartObject();
                writer.WriteString("site_id", inc.SiteId);
                writer.WriteString("strand", inc.Strand);
                writer.WriteString("phase", inc.Phase);
                writer.WriteNumber("ticks_in_phase", inc.TicksInPhase);
                writer.WriteNumber("warnings_issued", inc.WarningsIssued);
                writer.WriteNumber("incidents_triggered", inc.IncidentsTriggered);
                writer.WriteNumber("last_severity", inc.LastSeverity);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static SaveGame Deserialize(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        const string path = "$";

        var rngStates = new Dictionary<RandomStreamName, ulong>();
        foreach (var prop in JsonRequire.Object(root, "rng_states", path).EnumerateObject())
        {
            rngStates[Enum.Parse<RandomStreamName>(prop.Name)] = ulong.Parse(prop.Value.GetString()!);
        }

        var rngDrawCounts = new Dictionary<RandomStreamName, long>();
        foreach (var prop in JsonRequire.Object(root, "rng_draw_counts", path).EnumerateObject())
        {
            rngDrawCounts[Enum.Parse<RandomStreamName>(prop.Name)] = prop.Value.GetInt64();
        }

        var cashByKind = new Dictionary<LedgerAccountKind, long>();
        foreach (var prop in JsonRequire.Object(root, "cash_by_kind", path).EnumerateObject())
        {
            cashByKind[Enum.Parse<LedgerAccountKind>(prop.Name)] = prop.Value.GetInt64();
        }

        var reservedByKind = new Dictionary<LedgerAccountKind, long>();
        foreach (var prop in JsonRequire.Object(root, "reserved_by_kind", path).EnumerateObject())
        {
            reservedByKind[Enum.Parse<LedgerAccountKind>(prop.Name)] = prop.Value.GetInt64();
        }

        var cohorts = JsonRequire.MapArray(JsonRequire.Array(root, "cohorts", path), "$.cohorts", (item, itemPath, _) =>
            new SavedCohort(
                JsonRequire.String(item, "id", itemPath),
                JsonRequire.Int64(item, "home_building_source_id", itemPath),
                JsonRequire.Int32(item, "household_count", itemPath),
                JsonRequire.Int32(item, "people_per_household", itemPath),
                JsonRequire.Int32(item, "jobs_held", itemPath)));

        var buildings = JsonRequire.MapArray(JsonRequire.Array(root, "buildings", path), "$.buildings", (item, itemPath, _) =>
            new SavedBuilding(
                JsonRequire.Int64(item, "source_id", itemPath),
                JsonRequire.String(item, "archetype", itemPath),
                JsonRequire.Double(item, "local_x", itemPath),
                JsonRequire.Double(item, "local_z", itemPath),
                JsonRequire.Int64(item, "nearest_road_node_id", itemPath),
                JsonRequire.Int32(item, "jobs_count", itemPath),
                JsonRequire.Bool(item, "relocated", itemPath)));

        var projects = JsonRequire.MapArray(JsonRequire.Array(root, "projects", path), "$.projects", (item, itemPath, _) =>
        {
            var milestones = new List<long>();
            foreach (var m in JsonRequire.Array(item, "milestone_amounts", itemPath).EnumerateArray())
            {
                milestones.Add(m.GetInt64());
            }

            return new SavedProject(
                JsonRequire.String(item, "id", itemPath),
                JsonRequire.String(item, "kind", itemPath),
                JsonRequire.String(item, "ledger_kind", itemPath),
                JsonRequire.Int64(item, "fixed_cost_thb", itemPath),
                milestones,
                JsonRequire.Int32(item, "paid_milestones", itemPath),
                JsonRequire.Int64(item, "total_paid", itemPath),
                JsonRequire.String(item, "status", itemPath));
        });

        var roadSegments = JsonRequire.MapArray(JsonRequire.Array(root, "planned_road_segments", path), "$.planned_road_segments", (item, itemPath, _) =>
            new SavedRoadSegment(
                JsonRequire.Int64(item, "from_node_id", itemPath),
                JsonRequire.Int64(item, "to_node_id", itemPath),
                JsonRequire.Double(item, "length_meters", itemPath),
                JsonRequire.String(item, "project_id", itemPath)));

        var vacatedLots = JsonRequire.MapArray(JsonRequire.Array(root, "vacated_lots", path), "$.vacated_lots", (item, itemPath, _) =>
            new SavedVacatedLot(
                JsonRequire.Int64(item, "building_source_id", itemPath),
                JsonRequire.Double(item, "old_local_x", itemPath),
                JsonRequire.Double(item, "old_local_z", itemPath),
                JsonRequire.String(item, "project_id", itemPath)));

        var linkQueues = JsonRequire.MapArray(JsonRequire.Array(root, "link_queues", path), "$.link_queues", (item, itemPath, _) =>
            new SavedLinkQueue(
                JsonRequire.Int64(item, "way_id", itemPath),
                JsonRequire.Bool(item, "forward", itemPath),
                JsonRequire.Int64(item, "queue_length", itemPath),
                JsonRequire.Int64(item, "total_completed", itemPath),
                JsonRequire.Int64(item, "total_arrived", itemPath)));

        var gatewayFlows = JsonRequire.MapArray(JsonRequire.Array(root, "gateway_flows", path), "$.gateway_flows", (item, itemPath, _) =>
            new SavedGatewayFlow(
                JsonRequire.Int64(item, "gateway_node_id", itemPath),
                JsonRequire.Int32(item, "inbound_capacity_per_tick", itemPath),
                JsonRequire.Int32(item, "outbound_capacity_per_tick", itemPath),
                JsonRequire.Bool(item, "is_open", itemPath),
                JsonRequire.Int64(item, "generated_outbound", itemPath),
                JsonRequire.Int64(item, "completed_outbound", itemPath),
                JsonRequire.Int64(item, "pending_outbound", itemPath),
                JsonRequire.Int64(item, "generated_inbound", itemPath),
                JsonRequire.Int64(item, "completed_inbound", itemPath),
                JsonRequire.Int64(item, "pending_inbound", itemPath)));

        var roadWorksZones = JsonRequire.MapArray(JsonRequire.Array(root, "road_works_zones", path), "$.road_works_zones", (item, itemPath, _) =>
            new SavedRoadWorksZone(
                JsonRequire.String(item, "id", itemPath),
                JsonRequire.Int64(item, "way_id", itemPath),
                JsonRequire.Int64(item, "start_tick", itemPath),
                JsonRequire.Int64(item, "duration_ticks", itemPath),
                JsonRequire.Double(item, "capacity_multiplier_during_construction", itemPath),
                JsonRequire.String(item, "project_id", itemPath)));

        var busRoutes = JsonRequire.MapArray(JsonRequire.Array(root, "bus_routes", path), "$.bus_routes", (item, itemPath, _) =>
        {
            var stops = new List<long>();
            foreach (var stop in JsonRequire.Array(item, "stop_node_ids", itemPath).EnumerateArray())
            {
                stops.Add(stop.GetInt64());
            }

            return new SavedBusRoute(
                JsonRequire.String(item, "id", itemPath),
                stops,
                JsonRequire.Int32(item, "dwell_ticks_per_stop", itemPath),
                JsonRequire.Int32(item, "vehicle_count", itemPath),
                JsonRequire.Int32(item, "capacity_per_vehicle", itemPath),
                JsonRequire.Int64(item, "cumulative_ridership", itemPath));
        });

        var signals = JsonRequire.MapArray(JsonRequire.Array(root, "signals", path), "$.signals", (item, itemPath, _) =>
            new SavedSignal(
                JsonRequire.String(item, "id", itemPath),
                JsonRequire.Int64(item, "node_id", itemPath),
                JsonRequire.String(item, "kind", itemPath),
                JsonRequire.Int64(item, "cycle_ticks", itemPath),
                JsonRequire.Int32(item, "config_value", itemPath),
                JsonRequire.Int32(item, "discharge_rate_per_green_tick", itemPath),
                JsonRequire.Int64(item, "queue_a", itemPath),
                JsonRequire.Int64(item, "queue_b", itemPath),
                JsonRequire.Int32(item, "current_green_ticks_a", itemPath),
                JsonRequire.Int32(item, "current_green_ticks_b", itemPath),
                JsonRequire.Int64(item, "cumulative_queue_ticks_a", itemPath),
                JsonRequire.Int64(item, "cumulative_queue_ticks_b", itemPath),
                JsonRequire.Int64(item, "ticks_simulated", itemPath)));

        var incidentSites = JsonRequire.MapArray(JsonRequire.Array(root, "incident_sites", path), "$.incident_sites", (item, itemPath, _) =>
            new SavedIncidentSite(
                JsonRequire.String(item, "site_id", itemPath),
                JsonRequire.String(item, "strand", itemPath),
                JsonRequire.String(item, "phase", itemPath),
                JsonRequire.Int64(item, "ticks_in_phase", itemPath),
                JsonRequire.Int64(item, "warnings_issued", itemPath),
                JsonRequire.Int64(item, "incidents_triggered", itemPath),
                JsonRequire.Int32(item, "last_severity", itemPath)));

        return new SaveGame(
            JsonRequire.String(root, "map_id", path),
            JsonRequire.String(root, "map_content_hash", path),
            JsonRequire.String(root, "simulation_version", path),
            JsonRequire.String(root, "content_version", path),
            JsonRequire.Int64(root, "current_tick", path),
            JsonRequire.Int64(root, "revision", path),
            JsonRequire.Int64(root, "master_seed", path),
            rngStates,
            rngDrawCounts,
            cashByKind,
            reservedByKind,
            cohorts,
            buildings,
            projects,
            roadSegments,
            vacatedLots,
            linkQueues,
            gatewayFlows,
            roadWorksZones,
            busRoutes,
            signals,
            incidentSites);
    }
}
