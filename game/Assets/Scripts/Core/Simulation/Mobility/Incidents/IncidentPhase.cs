// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Mobility.Incidents;

/// <summary>
/// One site's state machine phase. Idle -&gt; Warning -&gt; Active -&gt; Cooldown
/// -&gt; Idle is the only path Active can be reached from (spec: "มีสัญญาณ
/// เตือน...ก่อน onset"); Warning can also fall back to Idle directly if the
/// triggering condition subsides or a prevention lever pushes risk back
/// down before the warning lead time elapses -- see
/// <see cref="IncidentLevers"/> and IncidentEngineTests for that "positive"
/// path (CD7: "มีเรื่องบวก").
/// </summary>
public enum IncidentPhase
{
    Idle,
    Warning,
    Active,
    Cooldown,
}
