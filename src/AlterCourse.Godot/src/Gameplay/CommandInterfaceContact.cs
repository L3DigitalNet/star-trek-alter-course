using AlterCourse.Core.Sensors;

namespace AlterCourse.Godot.Gameplay;

/// <summary>Provides one actor-safe sensor contact for Command Deck presentation.</summary>
public sealed record CommandInterfaceContact(
    SensorContactId Id,
    string Label,
    SensorContactStatus Status,
    SensorContactIdentification Identification,
    double ObservedXKilometers,
    double ObservedYKilometers,
    long ObservedAtMilliseconds,
    long ObservationAgeMilliseconds,
    string? KnownVesselDisplayName,
    string? KnownDesignDisplayName,
    bool IsActiveScanTarget
);
