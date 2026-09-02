using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Gameplay;

internal sealed record TravelingState(TravelState Travel) : PlayerStrategicState;
