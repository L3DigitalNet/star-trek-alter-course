using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.AI;

/// <summary>Contains only command-relevant state and observer-local knowledge available to one ship.</summary>
public sealed record ShipContactDecisionFacts
{
    private readonly ReadOnlyDecisionList<SensorContactSnapshot> _contacts;

    /// <summary>Initializes an immutable actor-safe decision snapshot.</summary>
    public ShipContactDecisionFacts(
        TacticalPosition ownPosition,
        TacticalMotion ownMotion,
        bool isAtLocation,
        SpeedKilometersPerSecond effectiveMaximumTacticalSpeed,
        IEnumerable<SensorContactSnapshot> contacts,
        IncomingHailFact? incomingHail = null
    )
    {
        ArgumentNullException.ThrowIfNull(contacts);
        SensorContactSnapshot[] materialized = contacts.ToArray();
        if (materialized.Any(contact => contact is null))
        {
            throw new ArgumentException("Decision contacts cannot contain null.", nameof(contacts));
        }

        if (materialized.Select(contact => contact.Id).Distinct().Count() != materialized.Length)
        {
            throw new ArgumentException(
                "Decision contacts require unique observer-local identities.",
                nameof(contacts)
            );
        }

        OwnPosition = ownPosition;
        OwnMotion = ownMotion;
        IsAtLocation = isAtLocation;
        EffectiveMaximumTacticalSpeed = effectiveMaximumTacticalSpeed;
        _contacts = new ReadOnlyDecisionList<SensorContactSnapshot>(materialized.OrderBy(contact => contact.Id.Value));
        IncomingHail = incomingHail;
    }

    /// <summary>Gets the deciding ship's authoritative local position.</summary>
    public TacticalPosition OwnPosition { get; }

    /// <summary>Gets the deciding ship's current local motion.</summary>
    public TacticalMotion OwnMotion { get; }

    /// <summary>Gets whether the deciding ship can accept a tactical course.</summary>
    public bool IsAtLocation { get; }

    /// <summary>Gets the deciding ship's current effective tactical-speed limit.</summary>
    public SpeedKilometersPerSecond EffectiveMaximumTacticalSpeed { get; }

    /// <summary>Gets canonically ordered observer-local contacts without truth identities.</summary>
    public IReadOnlyList<SensorContactSnapshot> Contacts => _contacts;

    /// <summary>Gets the optional incoming communication known to the deciding ship.</summary>
    public IncomingHailFact? IncomingHail { get; }
}
