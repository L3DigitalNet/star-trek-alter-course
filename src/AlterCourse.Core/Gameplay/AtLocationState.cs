using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Gameplay;

internal sealed record AtLocationState(LocationId LocationId) : PlayerStrategicState;
