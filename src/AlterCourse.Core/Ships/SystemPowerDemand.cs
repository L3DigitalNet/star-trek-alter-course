using AlterCourse.Core.Quantities;

namespace AlterCourse.Core.Ships;

/// <summary>Declares that installations of a definition are allocatable power consumers with a nominal demand.</summary>
public sealed record SystemPowerDemand
{
    /// <summary>Initializes a positive nominal demand.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The demand is zero.</exception>
    public SystemPowerDemand(PowerUnits nominalDemand)
    {
        // A zero-demand consumer would make proportional allocation divide shares over a zero weight; a
        // definition that draws no power is expressed by omitting the capability instead.
        if (nominalDemand.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nominalDemand), "Nominal demand must be positive.");
        }

        NominalDemand = nominalDemand;
    }

    /// <summary>Gets the power the consumer needs for full capability.</summary>
    public PowerUnits NominalDemand { get; }
}
