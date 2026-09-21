// Pure C# -- no UnityEngine reference. See game/README.md.
namespace Thaivia.Core.Simulation.Economy;

/// <summary>The four ledgers spec §12 requires kept separate.</summary>
public enum LedgerAccountKind
{
    Recurring,
    Opex,
    Capex,
    NonRecurring,
}
