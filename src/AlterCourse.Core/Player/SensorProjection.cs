
namespace AlterCourse.Core.Player;

/// <summary>Projects sensor integrity and repair state derived at projection time.</summary>
public sealed record SensorProjection
{
    internal SensorProjection(double integrity, double repairProgress, bool isRepairing) =>
        (Integrity, RepairProgress, IsRepairing) = (integrity, repairProgress, isRepairing);

    /// <summary>Gets bounded sensor integrity.</summary>
    public double Integrity { get; }

    /// <summary>Gets bounded repair progress, including one after completion.</summary>
    public double RepairProgress { get; }

    /// <summary>Gets whether a repair remains active.</summary>
    public bool IsRepairing { get; }
}
