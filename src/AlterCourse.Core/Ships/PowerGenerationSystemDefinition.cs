using AlterCourse.Core.Quantities;

namespace AlterCourse.Core.Ships;

/// <summary>Defines a power-generation component. Generators are neither repairable nor power consumers.</summary>
public sealed record PowerGenerationSystemDefinition : SystemDefinition
{
    /// <summary>Initializes a generator definition with positive nominal output.</summary>
    /// <remarks>
    /// The constructor accepts no repair or demand capability: generation is nonrepairable in the current
    /// simulation, and a generator drawing from its own supply has no meaning, so both combinations are
    /// unrepresentable rather than validated.
    /// </remarks>
    public PowerGenerationSystemDefinition(
        SystemDefinitionId id,
        string componentLabel,
        int commonOrder,
        bool conditionParticipation,
        PowerUnits nominalOutput
    )
        : base(id, componentLabel, commonOrder, conditionParticipation, repair: null, power: null)
    {
        if (nominalOutput.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nominalOutput), "Nominal output must be positive.");
        }

        NominalOutput = nominalOutput;
    }

    /// <inheritdoc />
    public override ShipSystemKind Kind => ShipSystemKind.PowerGeneration;

    /// <summary>Gets the output at full condition, before condition scaling.</summary>
    public PowerUnits NominalOutput { get; }
}
