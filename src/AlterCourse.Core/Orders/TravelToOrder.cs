using AlterCourse.Core.Identity;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Orders;

/// <summary>Directs a ship toward one strategic destination.</summary>
public sealed record TravelToOrder : ShipOrder
{
    /// <summary>Initializes a travel order without requiring aggregate map context.</summary>
    /// <param name="id">The stable order identity.</param>
    /// <param name="destination">The initialized strategic destination.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/> or <paramref name="destination"/> is uninitialized.
    /// </exception>
    public TravelToOrder(ShipOrderId id, LocationId destination)
        : base(id, ShipOrderKind.TravelTo)
    {
        if (string.IsNullOrWhiteSpace(destination.Value))
        {
            throw new ArgumentException("A travel order requires an initialized destination.", nameof(destination));
        }

        Destination = destination;
    }

    /// <summary>Gets the requested strategic destination.</summary>
    public LocationId Destination { get; }
}
