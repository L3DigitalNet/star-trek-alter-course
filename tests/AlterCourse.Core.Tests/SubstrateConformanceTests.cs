using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Player;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Tests.Support;

namespace AlterCourse.Core.Tests;

/// <summary>
/// Enforces ADR 0014 conformance durably: common ship-system types carry no per-kind members, common commands are
/// addressed by installed identity, no static kind list survives, no capture path writes historical fixed-field DTOs,
/// removed bridges stay removed, and public collections cannot be mutated.
/// </summary>
/// <remarks>
/// <para>
/// Every rule asserts a type, member, or code-path contract, never a keyword count. Each scanner is paired with a
/// negative probe that must be reported, so an inert scanner cannot pass by inspecting nothing.
/// </para>
/// <para>
/// Exemptions are member-level only: each names one declaring type, one member, and the reason it is not common
/// plumbing (design §9.2(a), §11.3). An exemption that no longer matches a token-bearing member fails the suite, so
/// stale entries cannot accumulate and silently widen the allowance.
/// </para>
/// </remarks>
public sealed class SubstrateConformanceTests
{
    private const BindingFlags AllDeclared =
        BindingFlags.Public
        | BindingFlags.NonPublic
        | BindingFlags.Instance
        | BindingFlags.Static
        | BindingFlags.DeclaredOnly;

    // Name fragments of the five current kinds as they appear in member names (design §9.2(a)). "Power" and
    // "Propulsion" are deliberately absent: power is the common allocation concept (AvailablePower, PowerAllocation)
    // and "Propulsion" occurs only together with "Impulse". KindTokensCoverEveryKind keeps this list in step with the
    // closed kind vocabulary.
    private static readonly string[] KindTokens =
    [
        "Sensor",
        "Impulse",
        "Shield",
        "DirectedEnergy",
        "Weapon",
        "Generation",
    ];

    private static readonly Type[] CommonTypes =
    [
        typeof(ShipEngineeringState),
        typeof(InstalledSystem),
        typeof(InstalledSystemCollection),
        typeof(InstalledSystemIdAllocator),
        typeof(ShipSystemAddress),
        typeof(SystemDefinition),
        typeof(PowerAllocation),
        typeof(PowerAllocationEntry),
        typeof(SystemRepairState),
        typeof(ShipStart),
        typeof(ShipSystemsStart),
        typeof(InstalledSystemStart),
        typeof(InstalledSystemStateStart),
        typeof(SystemRepairStart),
        typeof(PowerAllocationResult),
        typeof(SystemRepairResult),
        typeof(EngineeringApplication),
        typeof(RepairApplication),
        typeof(EngineeringProjection),
        typeof(InstalledSystemProjection),
        typeof(EngineeringActionProjection),
        typeof(SystemRepairProjection),
        .. typeof(SaveModelsV10).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic),
    ];

    private static readonly MemberExemption[] ProductionExemptions =
    [
        new(
            typeof(ShipEngineeringState),
            nameof(ShipEngineeringState.NominalGeneration),
            "Ship-level supply total derived from the singleton generator through SupportedSingle (design §4)."
        ),
        new(
            typeof(EngineeringProjection),
            nameof(EngineeringProjection.NominalGeneration),
            "Projected ship-level supply total; not a per-installation field."
        ),
        new(
            typeof(EngineeringProjection),
            nameof(EngineeringProjection.EffectivePassiveSensorRange),
            "Typed ship-level sensor fact derived through SupportedSingle (design §4, §11.3)."
        ),
        new(
            typeof(SaveModelsV10.ShipSnapshotV10),
            nameof(SaveModelsV10.ShipSnapshotV10.SensorKnowledge),
            "Ship-owned observer knowledge (contacts), not installed-system state."
        ),
        new(
            typeof(SaveModelsV10.ActiveSensorScanSnapshotV10),
            nameof(SaveModelsV10.ActiveSensorScanSnapshotV10.SensorInstalledSystemId),
            "Specialized scan continuation keyed by the source installation's identity (design §3.2)."
        ),
        new(
            typeof(SaveModelsV10.ShipCombatSnapshotV10),
            nameof(SaveModelsV10.ShipCombatSnapshotV10.DirectedEnergyReadiness),
            "Specialized weapon readiness list keyed by installation identity (design §3.2)."
        ),
        new(
            typeof(SaveModelsV10.DirectedEnergyReadinessSnapshotV10),
            nameof(SaveModelsV10.DirectedEnergyReadinessSnapshotV10.WeaponInstalledSystemId),
            "The installation key of one weapon readiness entry (design §3.2)."
        ),
    ];

    /// <summary>
    /// Runtime state a capture helper may read. A persistence method taking one of these and yielding a historical
    /// ship DTO would be the lossy "serialize into V8/V9 fixed fields, then upgrade" path OP §10 forbids.
    /// </summary>
    private static readonly Type[] RuntimeStateTypes =
    [
        typeof(GameSimulation),
        typeof(SimulationState),
        typeof(ShipState),
        typeof(ShipEngineeringState),
        typeof(ShipCombatState),
        typeof(InstalledSystemCollection),
        typeof(InstalledSystem),
        typeof(SystemRepairState),
        typeof(SensorKnowledge),
        typeof(ActiveSensorScanState),
    ];

    /// <summary>Historical DTOs capture may still build although V10 does not embed them, each with its reason.</summary>
    private static readonly (Type Type, string Reason)[] CaptureIntermediateExemptions =
    [
        (
            typeof(SaveModelsV7.FactionSnapshotV7),
            "Faction-domain core that CaptureFactionV8 composes into the unchanged V8 faction record; it carries no "
                + "ship-system state and the V10 envelope reuses FactionSnapshotV8 verbatim."
        ),
    ];

    /// <summary>Common ship-system types expose no per-kind member except the listed member-level exemptions.</summary>
    [Fact]
    public void CommonTypesExposeNoPerKindMembersBeyondMemberLevelExemptions()
    {
        string[] violations = [.. CommonTypes.SelectMany(type => KindMemberViolations(type, ProductionExemptions))];

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    /// <summary>Every exemption still names an existing, token-bearing member of exactly its declaring type.</summary>
    [Fact]
    public void EveryExemptionMatchesALiveTokenBearingMember()
    {
        foreach (MemberExemption exemption in ProductionExemptions)
        {
            Assert.Contains(exemption.DeclaringType, CommonTypes);
            Assert.False(string.IsNullOrWhiteSpace(exemption.Reason));
            Assert.True(
                ScannedMembers(exemption.DeclaringType)
                    .Any(member =>
                        string.Equals(member.Name, exemption.Member, StringComparison.Ordinal)
                        && HasKindToken(member.Name)
                    ),
                $"Stale exemption {exemption.DeclaringType.Name}.{exemption.Member}: no such token-bearing member."
            );
        }
    }

    /// <summary>
    /// The scanner reports per-kind condition, allocation, and capability members even when their names resemble an
    /// exempted member, proving the exemptions are bound to one member of one type rather than token-wide.
    /// </summary>
    [Fact]
    public void KindMemberScannerReportsEveryNegativeProbe()
    {
        Assert.Contains(
            "ProbeState.SensorCondition",
            KindMemberViolations(typeof(ProbeState), ProductionExemptions),
            StringComparer.Ordinal
        );
        Assert.Contains(
            "ProbeAllocation.ShieldAllocation",
            KindMemberViolations(typeof(ProbeAllocation), ProductionExemptions),
            StringComparer.Ordinal
        );
        string[] projection = [.. KindMemberViolations(typeof(ProbeProjection), ProductionExemptions)];
        Assert.Contains("ProbeProjection.ImpulseCapability", projection, StringComparer.Ordinal);
        Assert.Contains("ProbeProjection.NominalGeneration", projection, StringComparer.Ordinal);
        Assert.Contains("ProbeProjection.EffectivePassiveSensorRange", projection, StringComparer.Ordinal);
        Assert.Contains(
            "ProbeState._weaponReadyAt",
            KindMemberViolations(typeof(ProbeState), ProductionExemptions),
            StringComparer.Ordinal
        );
        Assert.Empty(KindMemberViolations(typeof(ProbeCommon), ProductionExemptions));
    }

    /// <summary>The token list covers every member of the closed kind vocabulary, so a new kind cannot slip past.</summary>
    [Fact]
    public void KindTokensCoverEveryKind()
    {
        ShipSystemKind[] kinds =
        [
            .. typeof(ShipSystemKind)
                .GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(property => property.PropertyType == typeof(ShipSystemKind))
                .Select(property => (ShipSystemKind)property.GetValue(null)!),
        ];

        Assert.Equal(5, kinds.Length);
        foreach (ShipSystemKind kind in kinds)
        {
            string compact = kind.Value.Replace("-", string.Empty, StringComparison.Ordinal);
            Assert.True(
                KindTokens.Any(token => compact.Contains(token, StringComparison.OrdinalIgnoreCase)),
                $"Kind '{kind.Value}' has no member-name token."
            );
        }
    }

    /// <summary>Generic Engineering outcome and operation enums carry no per-kind members.</summary>
    [Theory]
    [InlineData(typeof(EngineeringOperation))]
    [InlineData(typeof(EngineeringActionUnavailableReason))]
    [InlineData(typeof(PowerAllocationOutcome))]
    [InlineData(typeof(SystemRepairOutcome))]
    public void GenericEngineeringEnumsHaveNoPerKindMembers(Type enumType)
    {
        Assert.Empty(KindEnumMembers(enumType));
        Assert.Equal(["SensorDemandExceeded"], KindEnumMembers(typeof(ProbeOutcome)));
    }

    /// <summary>
    /// The four common Engineering commands, and their trusted internal transitions, are addressed by installed
    /// identity and never by kind; no per-kind command exists beside them.
    /// </summary>
    [Fact]
    public void CommonEngineeringCommandsAreAddressedByInstalledIdentityNotKind()
    {
        MethodInfo[] publicCommands =
        [
            .. typeof(GameSimulation)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method =>
                    method.ReturnType == typeof(PowerAllocationResult)
                    || method.ReturnType == typeof(SystemRepairResult)
                ),
        ];
        MethodInfo[] trustedTransitions =
        [
            .. typeof(GameSimulation)
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(method =>
                    method.ReturnType == typeof(EngineeringApplication)
                    || method.ReturnType == typeof(RepairApplication)
                ),
        ];

        Assert.Equal<string>(
            ["ApplyBalancedAllocation", "ApplyPriorityAllocation", "BeginSystemRepair", "SetPowerAllocation"],
            publicCommands.Select(method => method.Name).Order(StringComparer.Ordinal),
            StringComparer.Ordinal
        );
        // Private helpers that build a refused application share the return type, so the four transitions are
        // asserted present rather than exhaustive; every member of the set is still checked for kind addressing.
        Assert.Subset(
            trustedTransitions.Select(method => method.Name).ToHashSet(StringComparer.Ordinal),
            new HashSet<string>(
                [
                    "ApplyShipAllocation",
                    "ApplyShipBalancedAllocation",
                    "ApplyShipPriorityAllocation",
                    "ApplyShipRepair",
                ],
                StringComparer.Ordinal
            )
        );
        Assert.All(publicCommands.Concat(trustedTransitions), method => Assert.Null(KindAddressingViolation(method)));
        Assert.Contains(
            "takes a kind parameter",
            KindAddressingViolation(typeof(ProbeCommands).GetMethod(nameof(ProbeCommands.PrioritizeKind))!),
            StringComparison.Ordinal
        );
        Assert.Contains(
            "per-kind name",
            KindAddressingViolation(typeof(ProbeCommands).GetMethod(nameof(ProbeCommands.RepairShields))!),
            StringComparison.Ordinal
        );
    }

    /// <summary>
    /// No static member anywhere in Core holds a list of kinds (by type or by value): the aim vocabulary and every
    /// ordering come from the loaded definition catalog, not an independently maintained list.
    /// </summary>
    [Fact]
    public void NoStaticKindListExistsInCore()
    {
        string[] violations =
        [
            .. typeof(CoreAssemblyMarker)
                .Assembly.GetTypes()
                .Where(type => !type.ContainsGenericParameters)
                .SelectMany(StaticKindListViolations),
        ];

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
        Assert.Contains(
            "ProbeKindList.Supported",
            StaticKindListViolations(typeof(ProbeKindList)),
            StringComparer.Ordinal
        );
        Assert.Contains(
            "ProbeKindList.SupportedNames",
            StaticKindListViolations(typeof(ProbeKindList)),
            StringComparer.Ordinal
        );
    }

    /// <summary>No persistence method turns runtime state into a historical fixed-field DTO.</summary>
    [Fact]
    public void NoPersistenceMethodCapturesRuntimeStateIntoHistoricalDtos()
    {
        HashSet<Type> forbidden = ForbiddenCaptureTypes();
        string[] violations =
        [
            .. typeof(GamePersistence)
                .GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly
                )
                .Select(method => LossyCaptureSignature(method, forbidden))
                .OfType<string>(),
        ];

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
        Assert.NotNull(
            LossyCaptureSignature(typeof(ProbeLossyCapture).GetMethod(nameof(ProbeLossyCapture.Capture))!, forbidden)
        );
    }

    /// <summary>
    /// The complete save path, walked through its IL, never constructs or reads a historical DTO that the V10 envelope
    /// does not itself reuse; the only intermediate exemption must still be in use.
    /// </summary>
    [Fact]
    public void V10SavePathReferencesNoHistoricalShipDto()
    {
        HashSet<Type> forbidden = ForbiddenCaptureTypes();
        MethodBase[] roots =
        [
            .. typeof(GamePersistence)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(method => method.Name is nameof(GamePersistence.Serialize) or nameof(GamePersistence.Save)),
        ];
        Assert.Equal(2, roots.Select(root => root.Name).Distinct(StringComparer.Ordinal).Count());

        IReadOnlyDictionary<Type, string> referenced = IlTypeReferenceWalker.ReferencedTypes(roots, IsPersistenceCode);

        string[] violations =
        [
            .. referenced
                .Where(pair => forbidden.Contains(pair.Key))
                .Select(pair => $"{pair.Key.DeclaringType?.Name}.{pair.Key.Name} reached by {pair.Value}"),
        ];
        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
        Assert.Contains(typeof(SaveModelsV10.SaveEnvelopeV10), referenced.Keys);
        Assert.All(CaptureIntermediateExemptions, exemption => Assert.Contains(exemption.Type, referenced.Keys));

        IReadOnlyDictionary<Type, string> probe = IlTypeReferenceWalker.ReferencedTypes(
            [typeof(ProbeLossyCapture).GetMethod(nameof(ProbeLossyCapture.Capture))!],
            _ => false
        );
        Assert.Contains(probe.Keys, forbidden.Contains);
    }

    /// <summary>Runtime (non-persistence) Core code never touches a save DTO of any version.</summary>
    [Fact]
    public void RuntimeCodeReferencesNoSaveDto()
    {
        MethodBase[] runtimeMethods =
        [
            .. typeof(CoreAssemblyMarker)
                .Assembly.GetTypes()
                .Where(type =>
                    type.Namespace?.StartsWith("AlterCourse.Core.Persistence", StringComparison.Ordinal) != true
                )
                .SelectMany(type =>
                    type.GetMethods(AllDeclared).Cast<MethodBase>().Concat(type.GetConstructors(AllDeclared))
                ),
        ];

        IReadOnlyDictionary<Type, string> referenced = IlTypeReferenceWalker.ReferencedTypes(
            runtimeMethods,
            _ => false
        );

        string[] violations =
        [
            .. referenced.Where(pair => IsSaveDto(pair.Key)).Select(pair => $"{pair.Key.Name} reached by {pair.Value}"),
        ];
        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    /// The temporary L3 bridges and the retired per-kind plumbing stay deleted: no bridge file, type, or member
    /// survives, and a ship design retains only identity, display name, and default loadout.
    /// </summary>
    [Fact]
    public void RemovedBridgesAndPerKindPlumbingStayRemoved()
    {
        foreach (
            string path in new[]
            {
                "src/AlterCourse.Core/Gameplay/GameSimulation.EngineeringAdapter.cs",
                "src/AlterCourse.Core/Ships/PowerAllocationPreset.cs",
                "src/AlterCourse.Core/Ships/ShipEngineeringDefinition.cs",
                "src/AlterCourse.Core/Ships/ShipSystemId.cs",
                "src/AlterCourse.Core/Player/EngineeringAction.cs",
            }
        )
        {
            Assert.False(File.Exists(Path.Combine(TestShipContent.RepositoryRoot, path)), $"{path} must stay deleted.");
        }

        Assembly core = typeof(CoreAssemblyMarker).Assembly;
        foreach (
            string typeName in new[]
            {
                "AlterCourse.Core.Ships.PowerAllocationPreset",
                "AlterCourse.Core.Ships.ShipEngineeringDefinition",
                "AlterCourse.Core.Ships.ShipSystemId",
                "AlterCourse.Core.Player.EngineeringAction",
            }
        )
        {
            Assert.Null(core.GetType(typeName));
        }

        AssertNoMembers(
            typeof(GamePersistence),
            "CaptureV8",
            "CaptureV9",
            "CaptureShipV7",
            "CaptureShipV9",
            "CaptureEngineeringV9",
            "RestoreV8",
            "RestoreV9",
            "RestoreShipV7",
            "RestoreShipV9"
        );
        AssertNoMembers(typeof(GameSimulation), "SupportsRepair", "ApplyPowerAllocationPreset", "RepairDurationFor");
        AssertNoMembers(typeof(CombatLegality), "SupportedSystems");
        Assert.Equal<string>(
            ["DesignDisplayName", "Id", "InitialLoadout"],
            typeof(ShipDefinition)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal),
            StringComparer.Ordinal
        );
        // RestoreV10 is the only save-DTO→runtime ship constructor (design §7.3).
        Assert.Equal<string>(
            ["RestoreShipV10"],
            typeof(GamePersistence)
                .GetMethods(AllDeclared)
                .Where(method => method.ReturnType == typeof(ShipState))
                .Select(method => method.Name),
            StringComparer.Ordinal
        );
    }

    /// <summary>Public collections on live state, values, starts, and projections reject mutation through a cast.</summary>
    [Fact]
    public void PublicCollectionsRejectMutationThroughCasts()
    {
        GameSimulation game = FirstGameSetup.Create(TestShipContent.Production());
        ShipState player = game.CaptureState().GetRequiredShip(game.CaptureState().PlayerShipId);
        EngineeringProjection engineering = game.GetPlayerProjection().Ship.Engineering;
        ShipSystemsStart start = TestShipStarts.Pathfinder();
        object[] owners =
        [
            player.Engineering,
            player.Engineering.Systems,
            player.Engineering.Allocation,
            start,
            ShipSystemsStart.Explicit(
                2,
                [new(new InstalledSystemId(1), new SystemDefinitionId("x"), new SystemCondition(1), null)]
            ),
            engineering,
            game.GetPlayerProjection().Ship.Combat,
            TestShipContent.Production().SystemDefinitions,
        ];

        string[] mutable = [.. owners.SelectMany(MutablePublicCollections)];
        Assert.True(mutable.Length == 0, string.Join(Environment.NewLine, mutable));
        Assert.True(player.Engineering.Systems.OfKind(ShipSystemKind.Sensors) is not IList { IsReadOnly: false });
        Assert.Equal<string>(
            ["ProbeMutable.Items"],
            MutablePublicCollections(new ProbeMutable([1, 2])),
            StringComparer.Ordinal
        );
    }

    private static bool HasKindToken(string name) =>
        KindTokens.Any(token => name.Contains(token, StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns the properties, fields, and methods a common type declares or inherits from common bases.</summary>
    private static IEnumerable<MemberInfo> ScannedMembers(Type type)
    {
        const BindingFlags flags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        foreach (PropertyInfo property in type.GetProperties(flags))
        {
            yield return property;
        }

        foreach (FieldInfo field in type.GetFields(flags))
        {
            // Auto-property backing fields are reported through their property; literal constants are limits.
            if (!field.IsDefined(typeof(CompilerGeneratedAttribute)) && !field.IsLiteral)
            {
                yield return field;
            }
        }

        foreach (MethodInfo method in type.GetMethods(flags | BindingFlags.DeclaredOnly))
        {
            if (!method.IsSpecialName && !method.IsDefined(typeof(CompilerGeneratedAttribute)))
            {
                yield return method;
            }
        }
    }

    private static IEnumerable<string> KindMemberViolations(Type type, IReadOnlyList<MemberExemption> exemptions) =>
        ScannedMembers(type)
            .Where(member => HasKindToken(member.Name))
            .Where(member =>
                !exemptions.Any(exemption =>
                    exemption.DeclaringType == type
                    && string.Equals(exemption.Member, member.Name, StringComparison.Ordinal)
                )
            )
            .Select(member => $"{type.Name}.{member.Name}")
            .Distinct(StringComparer.Ordinal);

    private static string[] KindEnumMembers(Type enumType) => [.. Enum.GetNames(enumType).Where(HasKindToken)];

    private static string? KindAddressingViolation(MethodInfo method)
    {
        if (method.GetParameters().Any(parameter => parameter.ParameterType == typeof(ShipSystemKind)))
        {
            return $"{method.Name} takes a kind parameter.";
        }

        return HasKindToken(method.Name) ? $"{method.Name} has a per-kind name." : null;
    }

    private static IEnumerable<string> StaticKindListViolations(Type type)
    {
        const BindingFlags flags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (FieldInfo field in type.GetFields(flags).Where(field => !field.IsLiteral))
        {
            if (IsKindList(field.FieldType, field.GetValue(null)))
            {
                yield return $"{type.Name}.{field.Name}";
            }
        }

        foreach (
            PropertyInfo property in type.GetProperties(flags)
                .Where(property => property.GetMethod is not null && property.GetIndexParameters().Length == 0)
        )
        {
            bool enumerable =
                typeof(IEnumerable).IsAssignableFrom(property.PropertyType) && property.PropertyType != typeof(string);
            if (enumerable && IsKindList(property.PropertyType, property.GetValue(null)))
            {
                yield return $"{type.Name}.{property.Name}";
            }
        }
    }

    /// <summary>
    /// A kind list is a collection typed over <see cref="ShipSystemKind"/>, or any collection whose values include at
    /// least two distinct kinds or kind strings (catching a string table or a kind-keyed dictionary).
    /// </summary>
    private static bool IsKindList(Type declared, object? value)
    {
        if (declared.IsArray && declared.GetElementType() == typeof(ShipSystemKind))
        {
            return true;
        }

        if (declared.GetInterfaces().Append(declared).Any(IsEnumerableOfKind))
        {
            return true;
        }

        if (value is not IEnumerable items || value is string)
        {
            return false;
        }

        HashSet<string> kindStrings =
        [
            "power-generation",
            "sensors",
            "impulse-propulsion",
            "shields",
            "directed-energy-weapons",
        ];
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (object? item in items)
        {
            object? key =
                item?.GetType() is { IsGenericType: true } itemType
                && itemType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>)
                    ? itemType.GetProperty("Key")!.GetValue(item)
                    : item;
            switch (key)
            {
                case ShipSystemKind kind:
                    found.Add(kind.Value);
                    break;
                case string text when kindStrings.Contains(text):
                    found.Add(text);
                    break;
            }
        }

        return found.Count >= 2;
    }

    private static bool IsEnumerableOfKind(Type type) =>
        type.IsGenericType
        && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
        && type.GetGenericArguments()[0] == typeof(ShipSystemKind);

    private static bool IsSaveDto(Type type) =>
        (type.DeclaringType ?? type).FullName is { } name
        && Regex.IsMatch(
            name,
            @"^AlterCourse\.Core\.Persistence\.SaveModelsV\d+$",
            RegexOptions.None,
            TimeSpan.FromSeconds(1)
        );

    /// <summary>
    /// Returns every historical (V1–V9) save DTO that the V10 envelope does not itself embed, minus the listed
    /// capture intermediates. The reused set is derived from the V10 DTO graph, not maintained by hand.
    /// </summary>
    private static HashSet<Type> ForbiddenCaptureTypes()
    {
        Type[] historical =
        [
            .. typeof(CoreAssemblyMarker)
                .Assembly.GetTypes()
                .Where(type => IsSaveDto(type) && type.DeclaringType is not null)
                .Where(type => type.DeclaringType != typeof(SaveModelsV10)),
        ];
        var reused = new HashSet<Type>();
        var pending = new Stack<Type>([typeof(SaveModelsV10.SaveEnvelopeV10)]);
        while (pending.Count > 0)
        {
            Type type = pending.Pop();
            foreach (Type reached in ReachableTypes(type))
            {
                if (IsSaveDto(reached) && reused.Add(reached))
                {
                    pending.Push(reached);
                }
            }
        }

        HashSet<Type> forbidden = [.. historical.Where(type => !reused.Contains(type))];
        forbidden.ExceptWith(CaptureIntermediateExemptions.Select(exemption => exemption.Type));
        Assert.Contains(typeof(SaveModelsV9.ShipSnapshotV9), forbidden);
        Assert.Contains(typeof(SaveModelsV9.EngineeringSnapshotV9), forbidden);
        Assert.DoesNotContain(typeof(SaveModelsV9.CombatStimulusSnapshotV9), forbidden);
        return forbidden;
    }

    private static IEnumerable<Type> ReachableTypes(Type type)
    {
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Type reached = property.PropertyType;
            reached = reached.IsArray ? reached.GetElementType()! : reached;
            yield return Nullable.GetUnderlyingType(reached) ?? reached;
        }

        // Polymorphic DTOs (for example the V3 order hierarchy) are embedded through their base type, so every
        // concrete subtype a reused base admits is reused too.
        foreach (Type derived in typeof(CoreAssemblyMarker).Assembly.GetTypes().Where(IsSaveDto))
        {
            if (derived.IsSubclassOf(type))
            {
                yield return derived;
            }
        }
    }

    private static string? LossyCaptureSignature(MethodInfo method, HashSet<Type> forbidden)
    {
        if (!method.GetParameters().Any(parameter => RuntimeStateTypes.Contains(parameter.ParameterType)))
        {
            return null;
        }

        Type result = method.ReturnType.IsArray ? method.ReturnType.GetElementType()! : method.ReturnType;
        return forbidden.Contains(result) ? $"{method.Name} captures runtime state into {result.Name}." : null;
    }

    private static bool IsPersistenceCode(MethodBase method) =>
        method.Module == typeof(CoreAssemblyMarker).Module
        && method.DeclaringType?.Namespace?.StartsWith("AlterCourse.Core.Persistence", StringComparison.Ordinal) == true
        && !method.IsAbstract;

    private static void AssertNoMembers(Type type, params string[] names)
    {
        foreach (string name in names)
        {
            Assert.Empty(type.GetMember(name, AllDeclared));
        }
    }

    /// <summary>
    /// Returns each public collection member that a caller could mutate by casting it to a writable interface.
    /// </summary>
    private static IEnumerable<string> MutablePublicCollections(object owner)
    {
        foreach (PropertyInfo property in owner.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (
                property.GetIndexParameters().Length > 0
                || !typeof(IEnumerable).IsAssignableFrom(property.PropertyType)
                || property.PropertyType == typeof(string)
            )
            {
                continue;
            }

            object? value = property.GetValue(owner);
            if (value is not null && AcceptsMutation(value))
            {
                yield return $"{owner.GetType().Name}.{property.Name}";
            }
        }
    }

    /// <summary>Attempts a clearing mutation through every writable collection interface the value implements.</summary>
    private static bool AcceptsMutation(object value)
    {
        if (value is IList list)
        {
            try
            {
                list.Clear();
                return true;
            }
            catch (NotSupportedException) { }
        }

        foreach (Type collection in value.GetType().GetInterfaces().Where(IsGenericCollection))
        {
            try
            {
                collection.GetMethod(nameof(ICollection<int>.Clear))!.Invoke(value, null);
                return true;
            }
            catch (TargetInvocationException exception) when (exception.InnerException is NotSupportedException) { }
        }

        return false;
    }

    private static bool IsGenericCollection(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICollection<>);

    /// <summary>One member-level allowance: a declaring type, one member name, and why it is not common plumbing.</summary>
    private sealed record MemberExemption(Type DeclaringType, string Member, string Reason);

    /// <summary>Stands in for common state that regressed to a named per-kind condition and a private readiness field.</summary>
    private sealed class ProbeState(long weaponReadyAt)
    {
        private readonly long _weaponReadyAt = weaponReadyAt;

        public double SensorCondition { get; init; }

        public long ReadyAt => _weaponReadyAt;
    }

    /// <summary>Stands in for an allocation value that regressed to a fixed per-kind field.</summary>
    private sealed record ProbeAllocation(PowerUnits ShieldAllocation);

    /// <summary>
    /// Stands in for a projection with a per-kind capability plus members named like exempted production members,
    /// which must still be reported because exemptions bind to their own declaring type.
    /// </summary>
    private sealed record ProbeProjection(
        double ImpulseCapability,
        PowerUnits NominalGeneration,
        DistanceKilometers EffectivePassiveSensorRange
    );

    /// <summary>A clean common shape the scanner must not report.</summary>
    private sealed record ProbeCommon(InstalledSystemId Id, PowerUnits? Allocation, double Capability);

    /// <summary>Stands in for an outcome enum that regressed to a per-kind member.</summary>
    private enum ProbeOutcome
    {
        Accepted = 1,
        SensorDemandExceeded = 2,
    }

    /// <summary>Stands in for commands that regressed to kind addressing.</summary>
    private static class ProbeCommands
    {
        public static PowerAllocationResult PrioritizeKind(ShipSystemKind kind) =>
            throw new NotSupportedException(kind.Value);

        public static SystemRepairResult RepairShields(InstalledSystemId target) =>
            new(target.Value > 0 ? SystemRepairOutcome.Accepted : SystemRepairOutcome.UnknownSystem);
    }

    /// <summary>Stands in for the retired static aim list, by type and as a string table.</summary>
    private static class ProbeKindList
    {
        internal static IReadOnlyList<ShipSystemKind> Supported { get; } =
        [ShipSystemKind.Sensors, ShipSystemKind.Shields];

        internal static readonly string[] SupportedNames = ["sensors", "shields"];
    }

    /// <summary>Stands in for a lossy capture that collapses per-weapon readiness into the V9 fixed field.</summary>
    private static class ProbeLossyCapture
    {
        public static SaveModelsV9.ShipCombatSnapshotV9 Capture(ShipState ship) =>
            new()
            {
                NextDirectedEnergyReadyAtMilliseconds = ship.Combat.WeaponReadiness.IsEmpty
                    ? 0
                    : ship.Combat.WeaponReadiness[0].ReadyAt.Milliseconds,
                PendingStimulus = null,
            };
    }

    /// <summary>Stands in for a projection that leaks a mutable list behind a read-only interface.</summary>
    private sealed class ProbeMutable(List<int> items)
    {
        public IReadOnlyList<int> Items { get; } = items;
    }
}
