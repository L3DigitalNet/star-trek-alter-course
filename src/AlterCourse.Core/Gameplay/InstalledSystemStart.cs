using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;

namespace AlterCourse.Core.Gameplay;

/// <summary>One explicitly declared installation: identity, definition reference, condition, and allocation.</summary>
public sealed record InstalledSystemStart(
    InstalledSystemId Id,
    SystemDefinitionId DefinitionId,
    SystemCondition Condition,
    PowerUnits? Allocation
);
