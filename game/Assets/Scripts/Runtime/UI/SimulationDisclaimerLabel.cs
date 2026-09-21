// UNCOMPILED: never built by a Unity Editor. See
// game/Assets/Scripts/Runtime/README.md.
using UnityEngine;
using UnityEngine.UI;

namespace Thaivia.Runtime.UI
{
    /// <summary>
    /// A persistent, unmissable label stating that any economic/social
    /// figures shown (population, income, happiness, noise, demand, ...)
    /// are simulation inputs, never a real statistic (AGENTS.md rule 3:
    /// SimulationInitialization must never be presented as source fact).
    /// Bound directly to `SimulationInitialization.Note`, which the
    /// Python pipeline already writes as an honest, non-empty string for
    /// this exact purpose (see `map_pipeline.pipeline.mappack.run_pipeline`).
    /// </summary>
    public sealed class SimulationDisclaimerLabel : MonoBehaviour
    {
        [SerializeField] private Text? label;

        public void SetNote(string simulationInitializationNote)
        {
            if (label == null)
            {
                Debug.LogError("SimulationDisclaimerLabel has no Text target wired up.");
                return;
            }

            label.text = $"SIMULATION, NOT REAL DATA: {simulationInitializationNote}";
            gameObject.SetActive(true);
        }
    }
}
