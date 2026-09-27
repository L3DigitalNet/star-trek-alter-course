using System.Runtime.InteropServices;
using AlterCourse.Core.Identity;

namespace AlterCourse.Core.Ships;

/// <summary>
/// Names one installation globally by pairing its owning ship with the ship-local installed identity. Trusted
/// internal transitions take this pair so an installation is only ever resolved within the named ship.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct ShipSystemAddress(ShipInstanceId Ship, InstalledSystemId System);
