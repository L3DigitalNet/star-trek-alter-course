namespace AlterCourse.Core.Gameplay;

/// <summary>Describes the validated result of changing exact power allocation.</summary>
public enum PowerAllocationOutcome
{
    /// <summary>The allocation committed.</summary>
    Accepted = 1,

    /// <summary>Shield allocation exceeds authored demand.</summary>
    ShieldDemandExceeded = 6,

    /// <summary>Weapon allocation exceeds authored demand.</summary>
    DirectedEnergyDemandExceeded = 7,

    /// <summary>Sensor allocation exceeds authored demand.</summary>
    SensorDemandExceeded = 2,

    /// <summary>Impulse allocation exceeds authored demand.</summary>
    ImpulseDemandExceeded = 3,

    /// <summary>Total allocation exceeds current generation.</summary>
    AvailablePowerExceeded = 4,

    /// <summary>Current tactical speed would become illegal.</summary>
    CurrentSpeedExceedsResultingMaximum = 5,
}
