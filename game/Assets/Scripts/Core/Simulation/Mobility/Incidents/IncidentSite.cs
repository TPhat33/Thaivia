// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Mobility.Incidents;

/// <summary>One location's state-machine instance for one
/// <see cref="IncidentStrand"/>. Mutated only via <see cref="IncidentEngine.Step"/>
/// (mirrors <see cref="Gateways.GatewayFlow"/>'s "one owning engine
/// mutates this in place" style). <see cref="SiteId"/> is caller-defined
/// (e.g. a road node id or building source id) -- this type never stores
/// or reasons about WHAT that location is, only the state machine.</summary>
public sealed class IncidentSite
{
    public IncidentSite(string siteId, IncidentStrand strand)
    {
        SiteId = siteId;
        Strand = strand;
        Phase = IncidentPhase.Idle;
    }

    /// <summary>Restore constructor -- used only by save/load.</summary>
    public IncidentSite(string siteId, IncidentStrand strand, IncidentPhase phase, long ticksInPhase, long warningsIssued, long incidentsTriggered, int lastSeverity)
    {
        SiteId = siteId;
        Strand = strand;
        Phase = phase;
        TicksInPhase = ticksInPhase;
        WarningsIssued = warningsIssued;
        IncidentsTriggered = incidentsTriggered;
        LastSeverity = lastSeverity;
    }

    public string SiteId { get; }
    public IncidentStrand Strand { get; }
    public IncidentPhase Phase { get; private set; }
    public long TicksInPhase { get; private set; }

    /// <summary>How many times this site has ever entered Warning --
    /// includes warnings later cancelled (prevented/subsided), so this
    /// number alone is not the incident rate; see
    /// <see cref="IncidentsTriggered"/> for that.</summary>
    public long WarningsIssued { get; private set; }

    /// <summary>How many times this site has ever entered Active -- the
    /// number the "hard bound on incident rate" tests check.</summary>
    public long IncidentsTriggered { get; private set; }

    /// <summary>Severity of the most recent Active incident (1-100), or 0
    /// if none has occurred yet. Only ever set once, at the instant a
    /// Warning becomes Active -- never re-rolled mid-incident.</summary>
    public int LastSeverity { get; private set; }

    internal void TransitionTo(IncidentPhase phase)
    {
        Phase = phase;
        TicksInPhase = 0;
    }

    internal void AdvancePhaseTick() => TicksInPhase++;

    internal void RecordWarningIssued() => WarningsIssued++;

    internal void RecordIncidentTriggered(int severity)
    {
        IncidentsTriggered++;
        LastSeverity = severity;
    }
}
