using AlterCourse.Core.Identity;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Gameplay;

internal readonly record struct ShipTravelCommand(ShipInstanceId TargetShipId, LocationId Destination);
