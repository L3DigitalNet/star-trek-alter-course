using System.Reflection;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Player;
using AlterCourse.Core.Sensors;

namespace AlterCourse.Core.Tests;

/// <summary>Verifies compile-time architecture constraints that span project boundaries.</summary>
public sealed class ArchitectureBoundaryTests
{
    /// <summary>Confirms the pure simulation assembly does not load Godot.</summary>
    [Fact]
    public void CoreAssemblyDoesNotReferenceGodot()
    {
        System.Reflection.AssemblyName[] references = typeof(CoreAssemblyMarker).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(
            references,
            reference => reference.Name?.StartsWith("Godot", StringComparison.Ordinal) == true
        );
    }

    /// <summary>Confirms actor-safe surfaces expose no hidden ship identity and no persistence transport type.</summary>
    /// <remarks>
    /// The single admitted exception is the player ship's own identity: an observer legitimately knows
    /// which ship it is. Every other <see cref="ShipInstanceId"/> on these surfaces would be a hidden
    /// correlation the player was never given, which is exactly what contact reporting must not leak.
    /// </remarks>
    [Fact]
    public void ActorSafeProjectionsExposeNoHiddenIdentityOrPersistenceTypes()
    {
        foreach (MemberInfo member in ActorSafeMembers())
        {
            Type memberType = MemberType(member);
            Assert.False(
                memberType == typeof(ShipInstanceId)
                    && !(
                        member.DeclaringType == typeof(PlayerShipProjection)
                        && string.Equals(member.Name, nameof(PlayerShipProjection.InstanceId), StringComparison.Ordinal)
                    ),
                $"{member.DeclaringType!.Name}.{member.Name} exposes a hidden ship identity."
            );
            Assert.False(
                memberType.Namespace?.StartsWith("AlterCourse.Core.Persistence", StringComparison.Ordinal) == true,
                $"{member.DeclaringType!.Name}.{member.Name} exposes a persistence transport type."
            );
        }
    }

    /// <summary>Confirms every actor-safe projection member is read-only, so a consumer cannot rewrite knowledge.</summary>
    [Fact]
    public void ActorSafeProjectionsAreImmutable()
    {
        foreach (MemberInfo member in ActorSafeMembers())
        {
            switch (member)
            {
                case PropertyInfo property:
                    Assert.True(
                        property.SetMethod is null
                            || property
                                .SetMethod.ReturnParameter.GetRequiredCustomModifiers()
                                .Any(modifier =>
                                    string.Equals(
                                        modifier.FullName,
                                        "System.Runtime.CompilerServices.IsExternalInit",
                                        StringComparison.Ordinal
                                    )
                                ),
                        $"{property.DeclaringType!.Name}.{property.Name} has a settable public property."
                    );
                    break;
                case FieldInfo field:
                    Assert.True(
                        field.IsInitOnly,
                        $"{field.DeclaringType!.Name}.{field.Name} is a writable public field."
                    );
                    break;
                default:
                    Assert.Fail($"Unexpected actor-safe member kind on {member.DeclaringType!.Name}.");
                    break;
            }
        }
    }

    private static IEnumerable<MemberInfo> ActorSafeMembers()
    {
        IEnumerable<Type> actorSafeTypes = typeof(CoreAssemblyMarker)
            .Assembly.GetExportedTypes()
            .Where(type =>
                !type.IsEnum
                && (
                    string.Equals(type.Namespace, "AlterCourse.Core.Player", StringComparison.Ordinal)
                    || type == typeof(SensorContactSnapshot)
                )
            );
        foreach (Type type in actorSafeTypes)
        {
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                yield return property;
            }

            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                yield return field;
            }
        }
    }

    private static Type MemberType(MemberInfo member) =>
        member switch
        {
            PropertyInfo property => property.PropertyType,
            FieldInfo field => field.FieldType,
            _ => throw new InvalidOperationException("Unexpected actor-safe member kind."),
        };
}
