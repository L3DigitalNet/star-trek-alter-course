namespace AlterCourse.Core.Sensors;

/// <summary>Describes the identity knowledge learned about a sensor contact.</summary>
public enum SensorContactIdentification
{
    /// <summary>The observer knows only the contact's observed sensor facts.</summary>
    Detected = 1,

    /// <summary>The observer has learned the contact's vessel and design display names.</summary>
    Identified = 2,
}
