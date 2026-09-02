
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Gameplay;

/// <summary>Requests travel to one known strategic destination.</summary>
public readonly record struct TravelIntent
{
    /// <summary>Initializes a travel request.</summary>
    public TravelIntent(LocationId destination)
    {
        if (string.IsNullOrWhiteSpace(destination.Value))
        {
            throw new ArgumentException("Travel requires an initialized destination.", nameof(destination));
        }

        Destination = destination;
    }

    /// <summary>Gets the requested destination.</summary>
    public LocationId Destination { get; }
}
