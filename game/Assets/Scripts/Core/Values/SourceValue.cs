// Pure C# -- no UnityEngine reference. See game/README.md.
using System;

namespace Thaivia.Core.Values;

/// <summary>
/// Tri-state value: a field the MapPack either (a) got directly from the
/// OSM source (<see cref="Known"/>), (b) has no source value for and no
/// derived value for either (<see cref="Unknown"/>), or (c) has no source
/// value for but a visual/simulation assumption filled in, carrying the
/// rule that produced it (<see cref="Assumed"/>).
///
/// This is the single load-bearing type behind AGENTS.md rule 4 ("no data
/// = unknown, not empty/zero") and rule 3 (never let a simulation/visual
/// guess read back as a source fact). The three cases are three sealed
/// subclasses, not a flag on one mutable object, so a caller cannot
/// construct a value that is simultaneously "known" and "assumed", and
/// <see cref="AsSourceFact"/> physically cannot return an assumed value --
/// it throws instead of guessing. There is deliberately no
/// "GetValueOrDefault"-style member: an <see cref="Unknown"/> can only be
/// consumed through <see cref="Match{TResult}"/> or
/// <see cref="TryGetDisplayValue"/>, which force the caller to decide what
/// "no data" should look like at that call site, rather than silently
/// becoming 0/""/false.
/// </summary>
public abstract class SourceValue<T>
{
    private SourceValue()
    {
    }

    public abstract bool IsKnown { get; }
    public abstract bool IsUnknown { get; }
    public abstract bool IsAssumed { get; }

    /// <summary>
    /// The rule string that produced an assumed value, or null for
    /// Known/Unknown. Exposed so an inspector UI can show provenance
    /// without needing to pattern-match on the concrete subclass.
    /// </summary>
    public abstract string? AssumptionRule { get; }

    /// <summary>
    /// The assumption namespace, or null for Known/Unknown.
    /// </summary>
    public abstract AssumptionKind? AssumptionKindValue { get; }

    /// <summary>
    /// The source-fact accessor. Returns the value ONLY when this is
    /// <see cref="Known"/>. Throws <see cref="InvalidOperationException"/>
    /// for both <see cref="Unknown"/> and <see cref="Assumed"/> -- an
    /// assumed value can never be read through this accessor and made to
    /// look like it came from the source (AGENTS.md rule 4).
    /// </summary>
    public abstract T AsSourceFact();

    /// <summary>
    /// The best value to show/use for display or simulation purposes:
    /// true+value for Known or Assumed, false+default(T) for Unknown. The
    /// caller MUST check the returned bool; the out parameter is never a
    /// silent stand-in for "no data" on its own (an Unknown always yields
    /// false here, never a value that could be mistaken for 0/"").
    /// </summary>
    public abstract bool TryGetDisplayValue(out T value);

    public abstract TResult Match<TResult>(
        Func<T, TResult> onKnown,
        Func<TResult> onUnknown,
        Func<T, string, AssumptionKind, TResult> onAssumed);

    public static SourceValue<T> Known(T value) => new KnownValue(value);

    public static SourceValue<T> Unknown() => UnknownValue.Instance;

    public static SourceValue<T> Assumed(T value, string rule, AssumptionKind kind)
    {
        if (string.IsNullOrWhiteSpace(rule))
        {
            throw new ArgumentException("An assumed value must carry a non-empty rule string.", nameof(rule));
        }

        return new AssumedValue(value, rule, kind);
    }

    private sealed class KnownValue : SourceValue<T>
    {
        private readonly T _value;

        public KnownValue(T value) => _value = value;

        public override bool IsKnown => true;
        public override bool IsUnknown => false;
        public override bool IsAssumed => false;
        public override string? AssumptionRule => null;
        public override AssumptionKind? AssumptionKindValue => null;

        public override T AsSourceFact() => _value;

        public override bool TryGetDisplayValue(out T value)
        {
            value = _value;
            return true;
        }

        public override TResult Match<TResult>(
            Func<T, TResult> onKnown,
            Func<TResult> onUnknown,
            Func<T, string, AssumptionKind, TResult> onAssumed) => onKnown(_value);

        public override string ToString() => $"Known({_value})";
    }

    private sealed class UnknownValue : SourceValue<T>
    {
        public static readonly UnknownValue Instance = new();

        private UnknownValue()
        {
        }

        public override bool IsKnown => false;
        public override bool IsUnknown => true;
        public override bool IsAssumed => false;
        public override string? AssumptionRule => null;
        public override AssumptionKind? AssumptionKindValue => null;

        public override T AsSourceFact() =>
            throw new InvalidOperationException(
                $"SourceValue<{typeof(T).Name}> is Unknown; there is no source fact to read. "
                + "Use Match(...) or TryGetDisplayValue(...) instead.");

        public override bool TryGetDisplayValue(out T value)
        {
            value = default!;
            return false;
        }

        public override TResult Match<TResult>(
            Func<T, TResult> onKnown,
            Func<TResult> onUnknown,
            Func<T, string, AssumptionKind, TResult> onAssumed) => onUnknown();

        public override string ToString() => "Unknown";
    }

    private sealed class AssumedValue : SourceValue<T>
    {
        private readonly T _value;
        private readonly string _rule;
        private readonly AssumptionKind _kind;

        public AssumedValue(T value, string rule, AssumptionKind kind)
        {
            _value = value;
            _rule = rule;
            _kind = kind;
        }

        public override bool IsKnown => false;
        public override bool IsUnknown => false;
        public override bool IsAssumed => true;
        public override string? AssumptionRule => _rule;
        public override AssumptionKind? AssumptionKindValue => _kind;

        public override T AsSourceFact() =>
            throw new InvalidOperationException(
                $"SourceValue<{typeof(T).Name}> is Assumed (rule='{_rule}', kind={_kind}), not a source "
                + "fact. Reading an assumption through the source-fact accessor would misrepresent it "
                + "as coming from OSM (AGENTS.md rule 4). Use Match(...) or TryGetDisplayValue(...) instead.");

        public override bool TryGetDisplayValue(out T value)
        {
            value = _value;
            return true;
        }

        public override TResult Match<TResult>(
            Func<T, TResult> onKnown,
            Func<TResult> onUnknown,
            Func<T, string, AssumptionKind, TResult> onAssumed) => onAssumed(_value, _rule, _kind);

        public override string ToString() => $"Assumed({_value}, rule={_rule}, kind={_kind})";
    }
}
