namespace AlterCourse.Core.Ships;

/// <summary>Identifies one supported ship system by its stable semantic name.</summary>
public readonly record struct ShipSystemId
{
    private ShipSystemId(string value) => Value = value;

    /// <summary>Gets power generation identity.</summary>
    public static ShipSystemId PowerGeneration { get; } = new("power-generation");

    /// <summary>Gets sensor identity.</summary>
    public static ShipSystemId Sensors { get; } = new("sensors");

    /// <summary>Gets impulse propulsion identity.</summary>
    public static ShipSystemId ImpulsePropulsion { get; } = new("impulse-propulsion");

    /// <summary>Gets the stable serialized identity.</summary>
    public string Value { get; }

    /// <summary>Parses one known semantic identity.</summary>
    public static ShipSystemId Parse(string value) =>
        value switch
        {
            "power-generation" => PowerGeneration,
            "sensors" => Sensors,
            "impulse-propulsion" => ImpulsePropulsion,
            _ => throw new ArgumentException("Ship system identity is unsupported.", nameof(value)),
        };

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}
