using System.Reflection;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Player;
using AlterCourse.Core.Sensors;

namespace AlterCourse.Core.Tests;

/// <summary>Verifies compile-time architecture constraints that span project boundaries.</summary>
public sealed class ArchitectureBoundaryTests
{
    /// <summary>Bounds the walk against an unforeseen deep or generic-recursive projection shape.</summary>
    private const int MaxProjectionWalkDepth = 12;

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
    /// The check reaches through generic arguments and nested projection types, because a leak is just
    /// as real when it hides inside <c>IReadOnlyList&lt;ShipInstanceId&gt;</c> or two records down.
    /// </remarks>
    [Fact]
    public void ActorSafeProjectionsExposeNoHiddenIdentityOrPersistenceTypes()
    {
        foreach (MemberInfo member in ActorSafeMembers())
        {
            string? violation = FindActorSafetyViolation(member);
            Assert.True(violation is null, violation);
        }
    }

    /// <summary>Confirms the reachability walk itself detects leaks that an immediate-type check cannot see.</summary>
    /// <remarks>
    /// Without this negative case the production assertion above passes for the wrong reason: an inert
    /// walker that inspects nothing is indistinguishable from a correct one while the projections are
    /// clean. The probes below are the shapes a future projection change is most likely to introduce.
    /// </remarks>
    [Fact]
    public void ActorSafetyWalkDetectsIdentityAndPersistenceReachedIndirectly()
    {
        Assert.Contains(
            "hidden ship identity",
            FindActorSafetyViolation(ProbeMember<GenericLeakProbe>(nameof(GenericLeakProbe.Contacts))),
            StringComparison.Ordinal
        );
        Assert.Contains(
            "hidden ship identity",
            FindActorSafetyViolation(ProbeMember<NestedLeakProbe>(nameof(NestedLeakProbe.Inner))),
            StringComparison.Ordinal
        );
        Assert.Contains(
            "persistence transport type",
            FindActorSafetyViolation(ProbeMember<PersistenceLeakProbe>(nameof(PersistenceLeakProbe.Saves))),
            StringComparison.Ordinal
        );
        Assert.Null(FindActorSafetyViolation(ProbeMember<NestedLeakProbe>(nameof(NestedLeakProbe.Label))));
        Assert.Null(FindActorSafetyViolation(ProbeMember<SelfReferentialProbe>(nameof(SelfReferentialProbe.Next))));
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
        return actorSafeTypes.SelectMany(PublicInstanceMembers);
    }

    private static Type MemberType(MemberInfo member) =>
        member switch
        {
            PropertyInfo property => property.PropertyType,
            FieldInfo field => field.FieldType,
            _ => throw new InvalidOperationException("Unexpected actor-safe member kind."),
        };

    /// <summary>Returns a description of the first actor-safety leak reachable from a member, or null.</summary>
    private static string? FindActorSafetyViolation(MemberInfo member) =>
        FindActorSafetyViolation(
            MemberType(member),
            $"{member.DeclaringType!.Name}.{member.Name}",
            IsOwnIdentityMember(member),
            [],
            0
        );

    // `ownIdentityExempt` is true only for the player ship's own identity member itself. It is never
    // inherited by the types reached from that member, so an identity nested below it is still reported.
    private static string? FindActorSafetyViolation(
        Type type,
        string path,
        bool ownIdentityExempt,
        HashSet<Type> descended,
        int depth
    )
    {
        // The two leak checks run before the cycle guard: a type may be reached by several paths, and
        // only the first path may be exempt or shallow enough to descend. Skipping the check with the
        // descent would silently forgive every later occurrence.
        if (type == typeof(ShipInstanceId) && !ownIdentityExempt)
        {
            return $"{path} exposes a hidden ship identity.";
        }

        if (type.Namespace?.StartsWith("AlterCourse.Core.Persistence", StringComparison.Ordinal) == true)
        {
            return $"{path} exposes a persistence transport type.";
        }

        // Bounded and cycle-safe: `descended` stops a self-referential projection graph, and the depth
        // cap stops an unforeseen deep or generic-recursive shape from hanging the suite.
        if (depth >= MaxProjectionWalkDepth || !descended.Add(type))
        {
            return null;
        }

        foreach (Type argument in ContainedTypes(type))
        {
            string? violation = FindActorSafetyViolation(
                argument,
                $"{path} -> {argument.Name}",
                ownIdentityExempt: false,
                descended,
                depth + 1
            );
            if (violation is not null)
            {
                return violation;
            }
        }

        if (!IsInspectableProjectionType(type))
        {
            return null;
        }

        foreach (MemberInfo child in PublicInstanceMembers(type))
        {
            string? violation = FindActorSafetyViolation(
                MemberType(child),
                $"{path}.{child.Name}",
                IsOwnIdentityMember(child),
                descended,
                depth + 1
            );
            if (violation is not null)
            {
                return violation;
            }
        }

        return null;
    }

    private static bool IsOwnIdentityMember(MemberInfo member) =>
        member.DeclaringType == typeof(PlayerShipProjection)
        && string.Equals(member.Name, nameof(PlayerShipProjection.InstanceId), StringComparison.Ordinal);

    /// <summary>Returns the types a container carries, so a collection cannot hide its element type.</summary>
    private static Type[] ContainedTypes(Type type) =>
        type.IsArray ? [type.GetElementType()!] : type.GenericTypeArguments;

    /// <summary>Reports whether a type's own members are worth walking rather than treated as a leaf.</summary>
    /// <remarks>
    /// Framework types are leaves: their members belong to the BCL, not to this design, and descending
    /// into them would walk the whole runtime object graph. Their generic arguments are still inspected
    /// by <see cref="ContainedTypes"/>, which is where a projection's own types actually hide.
    /// </remarks>
    private static bool IsInspectableProjectionType(Type type) =>
        !type.IsPrimitive
        && !type.IsEnum
        && type != typeof(string)
        && type.Namespace?.StartsWith("System", StringComparison.Ordinal) != true;

    private static IEnumerable<MemberInfo> PublicInstanceMembers(Type type)
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

    private static PropertyInfo ProbeMember<T>(string name) =>
        typeof(T).GetProperty(name, BindingFlags.Instance | BindingFlags.Public)
        ?? throw new InvalidOperationException($"Probe member {typeof(T).Name}.{name} is missing.");

    /// <summary>Stands in for a projection that hides identity inside a collection type argument.</summary>
    private sealed record GenericLeakProbe(IReadOnlyList<ShipInstanceId> Contacts);

    /// <summary>Stands in for a projection whose leak is one nested projection type below its own members.</summary>
    private sealed record NestedLeakProbe(GenericLeakProbe Inner, string Label);

    /// <summary>Stands in for a projection that hands a save-transport record to an actor-safe consumer.</summary>
    private sealed record PersistenceLeakProbe(IReadOnlyList<GameSaveMetadata> Saves);

    /// <summary>Pins that a self-referential projection graph terminates instead of recursing forever.</summary>
    private sealed record SelfReferentialProbe(SelfReferentialProbe? Next, string Label);
}
