// Pure C# -- no UnityEngine reference. See game/README.md.
using System;
using System.Collections.Generic;

namespace Thaivia.Core.Simulation.Mobility.Queues;

/// <summary>
/// A finite-capacity queue per <see cref="LinkKey"/>: each tick, arrivals
/// join the queue and up to the link's capacity depart. When arrivals stay
/// within capacity the queue never grows; the instant sustained arrivals
/// exceed capacity, the backlog grows by exactly (arrivals - capacity) per
/// tick -- congestion is a DIRECT, exact arithmetic consequence of the
/// capacity number this type is given, never a separately tuned "traffic
/// score" (spec: "congestion ต้องเกิดจาก capacity ไม่ใช่ hand-tuned traffic
/// score"). This makes the model exactly reproducible/testable with
/// integer arithmetic, which is also what a save file needs to restore
/// bit-for-bit (see <see cref="Save.SavedLinkQueue"/>).
/// </summary>
public sealed class LinkQueueSimulator
{
    private readonly Dictionary<LinkKey, long> _queueLength = new();
    private readonly Dictionary<LinkKey, long> _totalCompleted = new();
    private readonly Dictionary<LinkKey, long> _totalArrived = new();

    public IReadOnlyDictionary<LinkKey, long> QueueLengths => _queueLength;
    public IReadOnlyDictionary<LinkKey, long> TotalCompleted => _totalCompleted;
    public IReadOnlyDictionary<LinkKey, long> TotalArrived => _totalArrived;

    public long QueueLengthOf(LinkKey link) => _queueLength.TryGetValue(link, out var v) ? v : 0;

    /// <summary>Advances one link's queue by exactly one tick: enqueue
    /// <paramref name="arrivals"/>, then dequeue up to
    /// <paramref name="capacityVehPerTick"/>. Returns how many actually
    /// departed this tick (never more than capacity, never more than what
    /// was queued).</summary>
    public long Step(LinkKey link, long arrivals, int capacityVehPerTick)
    {
        if (arrivals < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(arrivals));
        }

        if (capacityVehPerTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacityVehPerTick));
        }

        var current = QueueLengthOf(link) + arrivals;
        var departed = Math.Min(current, capacityVehPerTick);
        var remaining = current - departed;

        _queueLength[link] = remaining;
        _totalCompleted[link] = (_totalCompleted.TryGetValue(link, out var completedSoFar) ? completedSoFar : 0) + departed;
        _totalArrived[link] = (_totalArrived.TryGetValue(link, out var arrivedSoFar) ? arrivedSoFar : 0) + arrivals;

        return departed;
    }

    public IReadOnlyDictionary<LinkKey, long> CaptureQueueLengths() => new Dictionary<LinkKey, long>(_queueLength);
    public IReadOnlyDictionary<LinkKey, long> CaptureTotalCompleted() => new Dictionary<LinkKey, long>(_totalCompleted);
    public IReadOnlyDictionary<LinkKey, long> CaptureTotalArrived() => new Dictionary<LinkKey, long>(_totalArrived);

    public static LinkQueueSimulator Restore(
        IReadOnlyDictionary<LinkKey, long> queueLengths,
        IReadOnlyDictionary<LinkKey, long> totalCompleted,
        IReadOnlyDictionary<LinkKey, long> totalArrived)
    {
        var sim = new LinkQueueSimulator();
        foreach (var (k, v) in queueLengths)
        {
            sim._queueLength[k] = v;
        }

        foreach (var (k, v) in totalCompleted)
        {
            sim._totalCompleted[k] = v;
        }

        foreach (var (k, v) in totalArrived)
        {
            sim._totalArrived[k] = v;
        }

        return sim;
    }
}
