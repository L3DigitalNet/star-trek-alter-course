namespace AlterCourse.Core.Player;

/// <summary>
/// Identifies one generic player Engineering operation. Installation-targeted operations name their installation
/// through <see cref="EngineeringActionProjection.Target"/>, never through a per-kind member.
/// </summary>
public enum EngineeringOperation
{
    /// <summary>Applies the Balanced allocation over installed consumer demands.</summary>
    Balance = 1,

    /// <summary>Applies the priority allocation that satisfies one installed consumer first.</summary>
    Prioritize = 2,

    /// <summary>Begins a complete repair of one installed repairable system.</summary>
    BeginRepair = 3,

    /// <summary>Returns presentation focus to command.</summary>
    ReturnToCommand = 4,
}
