// Pure C# -- no UnityEngine reference. See game/README.md.
using System;

namespace Thaivia.Core.Simulation.Cohorts;

/// <summary>
/// A household/cohort record (spec §11: "ประชากรเป็น household/cohort
/// records; รถ/คนที่วาดเป็น sample ไม่ใช่ตัวกำหนดประชากรจริง"). Population
/// truth lives ONLY in <see cref="HouseholdCount"/> x
/// <see cref="PeoplePerHousehold"/> (see <see cref="PopulationCount"/>).
/// <see cref="SampleAgentCount"/> is a separate, capped number meant only
/// to tell a renderer how many pedestrian sprites to draw -- it is
/// structurally incapable of feeding back into
/// <see cref="PopulationCount"/> because it is a method that takes a cap
/// and returns a value, not a stored field the population total reads
/// from.
/// </summary>
public sealed class HouseholdCohort
{
    public HouseholdCohort(string id, long homeBuildingSourceId, int householdCount, int peoplePerHousehold, int jobsHeld)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("HouseholdCohort.Id must not be empty.", nameof(id));
        }

        if (householdCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(householdCount));
        }

        if (peoplePerHousehold < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(peoplePerHousehold));
        }

        if (jobsHeld < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(jobsHeld));
        }

        Id = id;
        HomeBuildingSourceId = homeBuildingSourceId;
        HouseholdCount = householdCount;
        PeoplePerHousehold = peoplePerHousehold;
        JobsHeld = jobsHeld;
    }

    public string Id { get; }
    public long HomeBuildingSourceId { get; }
    public int HouseholdCount { get; }
    public int PeoplePerHousehold { get; }
    public int JobsHeld { get; }

    /// <summary>The source of population truth for this cohort.</summary>
    public long PopulationCount => (long)HouseholdCount * PeoplePerHousehold;

    /// <summary>How many individual sprites a renderer should sample for
    /// this cohort, capped by a caller-supplied device/screen budget
    /// (plan §15: "ตัวละครบนจอ เริ่มเพดาน 128 ตัว"). This number is
    /// derived fresh on every call and is never stored, so nothing else
    /// in this type can be read back as if it were population truth.</summary>
    public int SampleAgentCount(int cap)
    {
        if (cap < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cap));
        }

        return (int)Math.Min(cap, HouseholdCount);
    }
}
