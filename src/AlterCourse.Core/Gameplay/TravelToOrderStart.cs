using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Gameplay;

/// <summary>Declares a one-shot order correlated with active travel to one destination.</summary>
public sealed record TravelToOrderStart(LocationId Destination) : ShipOrderStart;
