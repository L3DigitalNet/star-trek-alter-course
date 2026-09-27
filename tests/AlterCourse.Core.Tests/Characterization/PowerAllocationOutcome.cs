namespace AlterCourse.Core.Tests.Characterization;

/// <summary>
/// FROZEN M6A BASELINE VOCABULARY — not a Core API. The pre-substrate power-allocation outcomes with their base
/// member names and values, so the byte-identical <see cref="M6ABaselineExpectations"/> and
/// <see cref="M6ABaselineRecords"/> keep binding to what M6A actually reported.
/// </summary>
/// <remarks>
/// C# resolves a simple name in the enclosing namespace before compilation-unit <c>using</c> imports, so the frozen
/// characterization files in this namespace see this enum rather than Core's generic one. Only
/// <see cref="M6ABaselineProbe"/> translates Core outcomes into it (a consumer-demand refusal becomes the member
/// named for that consumer's kind). One type per file because analyzer MA0048 requires it.
/// </remarks>
internal enum PowerAllocationOutcome
{
    Accepted = 1,
    SensorDemandExceeded = 2,
    ImpulseDemandExceeded = 3,
    AvailablePowerExceeded = 4,
    CurrentSpeedExceedsResultingMaximum = 5,
    ShieldDemandExceeded = 6,
    DirectedEnergyDemandExceeded = 7,
}
