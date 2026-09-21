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
            vacatedLots);
    }
}
