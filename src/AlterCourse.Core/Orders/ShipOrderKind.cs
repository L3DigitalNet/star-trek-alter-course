namespace AlterCourse.Core.Orders;

/// <summary>Identifies the finite set of supported ship-order behaviors.</summary>
public enum ShipOrderKind
{
    /// <summary>Travel to one strategic destination.</summary>
    TravelTo = 1,

    /// <summary>Traverse an explicit cyclic patrol route.</summary>
    PatrolRoute = 2,

    /// <summary>Hold until correlated scheduled work becomes due.</summary>
    HoldUntil = 3,
}
