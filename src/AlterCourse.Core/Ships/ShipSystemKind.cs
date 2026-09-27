namespace AlterCourse.Core.Ships;

/// <summary>Identifies one supported ship system by its stable semantic name.</summary>
public readonly record struct ShipSystemKind
{
    private ShipSystemKind(string value) => Value = value;

    /// <summary>Gets power generation identity.</summary>
    public static ShipSystemKind PowerGeneration { get; } = new("power-generation");

    /// <summary>Gets sensor identity.</summary>
    public static ShipSystemKind Sensors { get; } = new("sensors");

    /// <summary>Gets impulse propulsion identity.</summary>
    public static ShipSystemKind ImpulsePropulsion { get; } = new("impulse-propulsion");

    /// <summary>Gets shield identity.</summary>
    public static ShipSystemKind Shields { get; } = new("shields");

    /// <summary>Gets directed-energy weapon identity.</summary>
    public static ShipSystemKind DirectedEnergyWeapons { get; } = new("directed-energy-weapons");

    /// <summary>Gets the stable serialized identity.</summary>
    public string Value { get; }

    /// <summary>Parses one known semantic identity.</summary>
    public static ShipSystemKind Parse(string value) =>
        value switch
        {
            "power-generation" => PowerGeneration,
            "sensors" => Sensors,
            "impulse-propulsion" => ImpulsePropulsion,
            "shields" => Shields,
            "directed-energy-weapons" => DirectedEnergyWeapons,
            _ => throw new ArgumentException("Ship system identity is unsupported.", nameof(value)),
        };

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}
