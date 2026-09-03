using System.Collections.Immutable;
using AlterCourse.Core.Gameplay;

namespace AlterCourse.Core.Sensors;

/// <summary>Owns one ship's bounded, canonically ordered sensor knowledge.</summary>
internal sealed record SensorKnowledge
{
    internal const int MaximumContactsPerObserver = SimulationState.MaximumShips - 1;

    internal SensorKnowledge(
        long nextContactId,
        IEnumerable<SensorContactTrack> contacts,
        ActiveSensorScanState? activeScan = null
    )
    {
        ArgumentNullException.ThrowIfNull(contacts);
        SensorContactTrack[] materialized = contacts.Take(MaximumContactsPerObserver + 1).ToArray();
        if (materialized.Length > MaximumContactsPerObserver)
        {
            throw new ArgumentException(
                $"Sensor knowledge supports at most {MaximumContactsPerObserver} retained contacts.",
                nameof(contacts)
            );
        }

        if (materialized.Any(contact => contact is null))
        {
            throw new ArgumentException("Sensor knowledge cannot contain a null contact.", nameof(contacts));
        }

        NextContactId = nextContactId;
        Contacts = [.. materialized.OrderBy(contact => contact.Id.Value)];
        ActiveScan = activeScan;
    }

    internal long NextContactId { get; init; }
    internal ImmutableArray<SensorContactTrack> Contacts { get; private init; }
    internal ActiveSensorScanState? ActiveScan { get; init; }

    internal static SensorKnowledge Empty { get; } = new(1, []);
}
