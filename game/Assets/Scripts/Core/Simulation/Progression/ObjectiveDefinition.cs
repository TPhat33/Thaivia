// Pure C# -- no UnityEngine reference. See game/README.md.
using System;

namespace Thaivia.Core.Simulation.Progression;

/// <summary>One step of a tutorial/scenario objective sequence: an id
/// (stable identifier a save or a UI could reference), a short English
/// title (data, not UI -- see this namespace's doc comment on
/// <see cref="IObjectiveCheck"/>), and the pure check that decides
/// completion.</summary>
public sealed class ObjectiveDefinition
{
    public ObjectiveDefinition(string id, string title, IObjectiveCheck check)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("ObjectiveDefinition.Id must not be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("ObjectiveDefinition.Title must not be empty.", nameof(title));
        }

        Id = id;
        Title = title;
        Check = check ?? throw new ArgumentNullException(nameof(check));
    }

    public string Id { get; }
    public string Title { get; }
    public IObjectiveCheck Check { get; }
}
