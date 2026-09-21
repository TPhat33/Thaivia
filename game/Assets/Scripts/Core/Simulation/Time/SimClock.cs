// Pure C# -- no UnityEngine reference. See game/README.md.
using System;

namespace Thaivia.Core.Simulation.Time;

/// <summary>
/// A purely logical tick counter. This type has NO method, property, or
/// field that reads <see cref="DateTime.Now"/>, <see cref="DateTime.UtcNow"/>,
/// <see cref="Environment.TickCount"/>, or any other wall-clock source --
/// ticks only ever move because <see cref="AdvanceTicks"/> was called with
/// an explicit count, driven by the game's own fixed-step loop. That
/// absence is what makes "no wall-clock catch-up" true: resuming a
/// <see cref="SimClock"/> after any real-world gap -- one second or one
/// month -- advances <see cref="CurrentTick"/> by exactly zero on its own
/// (plan §13: "auto-save ระหว่าง session; pause เมื่อ background; ไม่มี
/// wall-clock catch-up"). See
/// Thaivia.Core.Tests.NoWallClockCatchUpTests for both the behavioural
/// proof and a source-scan control test that this type really contains no
/// wall-clock read (and that the scanner itself is not a no-op).
/// </summary>
public sealed class SimClock
{
    public long CurrentTick { get; private set; }

    public bool IsPaused { get; private set; }

    public void Pause() => IsPaused = true;

    /// <summary>Resumes ticking. Deliberately takes no wall-clock/elapsed
    /// parameter of any kind: there is no way to hand this method "how
    /// long we were paused" even if a caller wanted to, so no catch-up
    /// computation can ever sneak in here later without changing this
    /// method's signature.</summary>
    public void Resume() => IsPaused = false;

    /// <summary>Advances the clock by exactly <paramref name="tickCount"/>
    /// logical ticks. This is the ONLY way <see cref="CurrentTick"/> ever
    /// changes.</summary>
    public void AdvanceTicks(long tickCount)
    {
        if (tickCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tickCount), "Tick count must not be negative.");
        }

        if (IsPaused)
        {
            throw new InvalidOperationException("Cannot advance a paused SimClock; call Resume() first.");
        }

        CurrentTick += tickCount;
    }
}
