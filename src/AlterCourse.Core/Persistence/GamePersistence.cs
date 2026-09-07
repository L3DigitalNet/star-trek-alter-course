using System.Text.Json;
using System.Text.Json.Serialization;
using AlterCourse.Core.AI;
using AlterCourse.Core.Content;
using AlterCourse.Core.Factions;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Orders;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;
using FiniteDoubleJsonConverter = AlterCourse.Core.Persistence.SaveModelsV1.FiniteDoubleJsonConverter;
using HoldUntilOrderSnapshotV3 = AlterCourse.Core.Persistence.SaveModelsV3.HoldUntilOrderSnapshotV3;
using PatrolRouteOrderSnapshotV3 = AlterCourse.Core.Persistence.SaveModelsV3.PatrolRouteOrderSnapshotV3;
using PlayerShipSnapshotV1 = AlterCourse.Core.Persistence.SaveModelsV1.PlayerShipSnapshotV1;
using SaveEnvelopeV1 = AlterCourse.Core.Persistence.SaveModelsV1.SaveEnvelopeV1;
using SaveEnvelopeV2 = AlterCourse.Core.Persistence.SaveModelsV2.SaveEnvelopeV2;
using SaveEnvelopeV3 = AlterCourse.Core.Persistence.SaveModelsV3.SaveEnvelopeV3;
using SaveEnvelopeV4 = AlterCourse.Core.Persistence.SaveModelsV4.SaveEnvelopeV4;
using SaveEnvelopeV5 = AlterCourse.Core.Persistence.SaveModelsV5.SaveEnvelopeV5;
using SaveEnvelopeV6 = AlterCourse.Core.Persistence.SaveModelsV6.SaveEnvelopeV6;
using SaveEnvelopeV7 = AlterCourse.Core.Persistence.SaveModelsV7.SaveEnvelopeV7;
using SaveMetadataV2 = AlterCourse.Core.Persistence.SaveModelsV2.SaveMetadataV2;
using ScheduledWorkSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.ScheduledWorkSnapshotV2;
using SchedulerSnapshotV1 = AlterCourse.Core.Persistence.SaveModelsV1.SchedulerSnapshotV1;
using SchedulerSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.SchedulerSnapshotV2;
using SensorRepairSnapshotV1 = AlterCourse.Core.Persistence.SaveModelsV1.SensorRepairSnapshotV1;
using SensorRepairSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.SensorRepairSnapshotV2;
using ShipOrderSnapshotV3 = AlterCourse.Core.Persistence.SaveModelsV3.ShipOrderSnapshotV3;
using ShipSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.ShipSnapshotV2;
using ShipSnapshotV3 = AlterCourse.Core.Persistence.SaveModelsV3.ShipSnapshotV3;
using ShipSnapshotV4 = AlterCourse.Core.Persistence.SaveModelsV4.ShipSnapshotV4;
using ShipSnapshotV5 = AlterCourse.Core.Persistence.SaveModelsV5.ShipSnapshotV5;
using ShipSnapshotV6 = AlterCourse.Core.Persistence.SaveModelsV6.ShipSnapshotV6;
using ShipSnapshotV7 = AlterCourse.Core.Persistence.SaveModelsV7.ShipSnapshotV7;
using SimulationSnapshotV1 = AlterCourse.Core.Persistence.SaveModelsV1.SimulationSnapshotV1;
using SimulationSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.SimulationSnapshotV2;
using SimulationSnapshotV3 = AlterCourse.Core.Persistence.SaveModelsV3.SimulationSnapshotV3;
using SimulationSnapshotV4 = AlterCourse.Core.Persistence.SaveModelsV4.SimulationSnapshotV4;
using SimulationSnapshotV5 = AlterCourse.Core.Persistence.SaveModelsV5.SimulationSnapshotV5;
using SimulationSnapshotV6 = AlterCourse.Core.Persistence.SaveModelsV6.SimulationSnapshotV6;
using SimulationSnapshotV7 = AlterCourse.Core.Persistence.SaveModelsV7.SimulationSnapshotV7;
using StrategicLocationSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.StrategicLocationSnapshotV2;
using StrategicMapSnapshotV1 = AlterCourse.Core.Persistence.SaveModelsV1.StrategicMapSnapshotV1;
using StrategicMapSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.StrategicMapSnapshotV2;
using StrategicPositionSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.StrategicPositionSnapshotV2;
using StrategicRouteSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.StrategicRouteSnapshotV2;
using StrategicStateSnapshotV1 = AlterCourse.Core.Persistence.SaveModelsV1.StrategicStateSnapshotV1;
using StrategicStateSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.StrategicStateSnapshotV2;
using TacticalMotionSnapshotV1 = AlterCourse.Core.Persistence.SaveModelsV1.TacticalMotionSnapshotV1;
using TacticalMotionSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.TacticalMotionSnapshotV2;
using TacticalPositionSnapshotV1 = AlterCourse.Core.Persistence.SaveModelsV1.TacticalPositionSnapshotV1;
using TacticalPositionSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.TacticalPositionSnapshotV2;
using TravelSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.TravelSnapshotV2;
using TravelToOrderSnapshotV3 = AlterCourse.Core.Persistence.SaveModelsV3.TravelToOrderSnapshotV3;

namespace AlterCourse.Core.Persistence;

/// <summary>Maps the authoritative simulation to and from the strict explicit JSON save contract.</summary>
public static class GamePersistence
{
    private const int V1SchemaVersion = 1;
    private const int V2SchemaVersion = 2;
    private const int V3SchemaVersion = 3;
    private const int V4SchemaVersion = 4;
    private const int V5SchemaVersion = 5;
    private const int V6SchemaVersion = 6;
    private const int CurrentSchemaVersion = 7;
    private const string V1SimulationRulesVersion = "first-playable-v1";
    private const string V2SimulationRulesVersion = "first-playable-v1";
    private const string V3SimulationRulesVersion = "active-world-orders-v1";
    private const string V4SimulationRulesVersion = "sensor-knowledge-first-contact-v1";
    private const string V5SimulationRulesVersion = "engineering-backbone-v1";

    // Historical rules identities stay frozen because every adjacent migration validates its source
    // before adding later state; changing one would relabel an old document instead of migrating it.
    private const string V6SimulationRulesVersion = "strategic-contact-reporting-v1";

    // Godot's gameplay shell asserts the current literal from the written save in
    // src/AlterCourse.Godot/tests/GameplayShellTest.gd, so changing it requires updating that end.
    private const string CurrentSimulationRulesVersion = "faction-intent-autonomous-assignment-v1";
    private const string TravelArrivalKind = "travelArrival";
    private const string SensorRepairCompletionKind = "sensorRepairCompletion";
    private const string SystemRepairCompletionKind = "systemRepairCompletion";
    private const string OrderWakeKind = "orderWake";
    private const string SensorContactLossKind = "sensorContactLoss";
    private const string ActiveSensorScanCompletionKind = "activeSensorScanCompletion";
    private const string ShipContactDecisionWakeKind = "shipContactDecisionWake";
    private const string FactionDecisionWakeKind = "factionDecisionWake";
    private const string ShipTargetKind = "ship";
    private const string FactionTargetKind = "faction";
    private const string PendingObjectiveStatus = "pending";
    private const string AssignedObjectiveStatus = "assigned";
    private const string SatisfiedObjectiveStatus = "satisfied";
    private const string CurrentContactStatus = "current";
    private const string StaleContactStatus = "stale";
    private const string LostContactStatus = "lost";
    private const string DetectedContactIdentification = "detected";
    private const string IdentifiedContactIdentification = "identified";
    private const string CautiousContactPosture = "cautiousContact";
    private const string AtLocationKind = "atLocation";
    private const string TravelingKind = "traveling";
    private const int MaximumSaveBytes = 128 * 1024 * 1024;
    private const int MaximumJsonDepth = 32;
    private const int MaximumMetadataTextLength = 128;

    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = MaximumJsonDepth,
    };

    /// <summary>Serializes a validated simulation and caller-supplied organization metadata as V7 UTF-8 JSON.</summary>
    public static byte[] Serialize(GameSimulation simulation, GameSaveMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(metadata);
        ValidateMetadata(metadata);

        SaveEnvelopeV7 envelope = CaptureV7(simulation.CaptureState(), metadata);
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(envelope, SerializerOptions);
        if (json.Length > MaximumSaveBytes)
        {
            throw new InvalidOperationException($"The V7 save exceeds the {MaximumSaveBytes}-byte contract limit.");
        }

        return json;
    }

    /// <summary>Loads bounded untrusted UTF-8 JSON into a new simulation without mutating another aggregate.</summary>
    public static LoadedGameSave Deserialize(
        ReadOnlySpan<byte> utf8Json,
        ShipDefinitionCatalog catalog,
        string sourceIdentity
    ) => Deserialize(utf8Json, catalog, FactionDefinitionCatalog.Empty, sourceIdentity);

    /// <summary>Loads bounded untrusted UTF-8 JSON with both immutable content catalogs.</summary>
    public static LoadedGameSave Deserialize(
        ReadOnlySpan<byte> utf8Json,
        ShipDefinitionCatalog catalog,
        FactionDefinitionCatalog factionCatalog,
        string sourceIdentity
    )
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(factionCatalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceIdentity);
        if (utf8Json.Length > MaximumSaveBytes)
        {
            throw Failure(
                GamePersistenceFailure.InvalidData,
                sourceIdentity,
                $"exceeds the {MaximumSaveBytes}-byte input limit."
            );
        }

        byte[] documentBytes = utf8Json.ToArray();
        try
        {
            using var document = JsonDocument.Parse(documentBytes, DocumentOptions);
            RejectDuplicateMembers(document.RootElement, sourceIdentity, "$", 0);
            int version = ReadSchemaVersion(document.RootElement, sourceIdentity);

            return version switch
            {
                V1SchemaVersion => LoadV1(documentBytes, catalog, factionCatalog, sourceIdentity),
                V2SchemaVersion => LoadV2(documentBytes, catalog, factionCatalog, sourceIdentity),
                V3SchemaVersion => LoadV3(documentBytes, catalog, factionCatalog, sourceIdentity),
                V4SchemaVersion => LoadV4(documentBytes, catalog, factionCatalog, sourceIdentity),
                V5SchemaVersion => LoadV5(documentBytes, catalog, factionCatalog, sourceIdentity),
                V6SchemaVersion => LoadV6(documentBytes, catalog, factionCatalog, sourceIdentity),
                CurrentSchemaVersion => LoadV7(documentBytes, catalog, factionCatalog, sourceIdentity),
                _ => throw Failure(
                    GamePersistenceFailure.UnsupportedVersion,
                    sourceIdentity,
                    $"declares unsupported schema version {version}."
                ),
            };
        }
        catch (GamePersistenceException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            string location = string.IsNullOrWhiteSpace(exception.Path) ? string.Empty : $" at '{exception.Path}'";
            throw Failure(
                GamePersistenceFailure.InvalidData,
                sourceIdentity,
                $"contains malformed or incompatible JSON{location}: {exception.Message}",
                exception
            );
        }
    }

    /// <summary>
    /// Writes a complete candidate beside the target, durably flushes it where supported, then uses
    /// same-filesystem atomic replacement visibility; this does not promise universal power-loss durability.
    /// </summary>
    public static void Save(string path, GameSimulation simulation, GameSaveMetadata metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        byte[] json = Serialize(simulation, metadata);
        string targetPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(targetPath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new ArgumentException("Save path must have a target directory.", nameof(path));
        }

        string fileName = Path.GetFileName(targetPath);
        string temporaryPath = Path.Combine(directory, $".{fileName}.{Path.GetRandomFileName()}.tmp");
        bool temporaryCreated = false;

        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                temporaryCreated = true;
                stream.Write(json);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, targetPath, overwrite: true);
            temporaryCreated = false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw Failure(
                GamePersistenceFailure.InputOutput,
                targetPath,
                $"could not be atomically replaced: {exception.Message}",
                exception
            );
        }
        finally
        {
            if (temporaryCreated && File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // Cleanup is secondary to the typed write failure already in flight. The
                    // isolated candidate may remain, but it must never replace that primary error.
                }
            }
        }
    }

    /// <summary>Reads a bounded save path and returns its newly reconstructed simulation and metadata.</summary>
    public static LoadedGameSave Load(string path, ShipDefinitionCatalog catalog) =>
        Load(path, catalog, FactionDefinitionCatalog.Empty);

    /// <summary>Reads a bounded save path using both immutable content catalogs.</summary>
    public static LoadedGameSave Load(
        string path,
        ShipDefinitionCatalog catalog,
        FactionDefinitionCatalog factionCatalog
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(factionCatalog);
        string sourceIdentity = Path.GetFullPath(path);

        try
        {
            using var stream = new FileStream(sourceIdentity, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaximumSaveBytes)
            {
                throw Failure(
                    GamePersistenceFailure.InvalidData,
                    sourceIdentity,
                    $"exceeds the {MaximumSaveBytes}-byte input limit."
                );
            }

            byte[] json = new byte[checked((int)stream.Length)];
            stream.ReadExactly(json);
            if (stream.ReadByte() != -1)
            {
                throw Failure(
                    GamePersistenceFailure.InvalidData,
                    sourceIdentity,
                    $"changed while being read or exceeds the {MaximumSaveBytes}-byte input limit."
                );
            }

            return Deserialize(json, catalog, factionCatalog, sourceIdentity);
        }
        catch (GamePersistenceException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw Failure(
                GamePersistenceFailure.InputOutput,
                sourceIdentity,
                $"could not be read: {exception.Message}",
                exception
            );
        }
    }

    private static SaveEnvelopeV7 CaptureV7(SimulationState state, GameSaveMetadata metadata)
    {
        if (state.Ships.Length > SimulationState.MaximumShips)
        {
            throw new InvalidOperationException(
                $"V7 persistence supports at most {SimulationState.MaximumShips} ships."
            );
        }

        if (state.Factions.Length > SimulationState.MaximumFactions)
        {
            throw new InvalidOperationException(
                $"V7 persistence supports at most {SimulationState.MaximumFactions} factions."
            );
        }

        return new SaveEnvelopeV7
        {
            SchemaVersion = CurrentSchemaVersion,
            SimulationRulesVersion = CurrentSimulationRulesVersion,
            Metadata = new SaveMetadataV2
            {
                SaveId = metadata.SaveId,
                DisplayName = metadata.DisplayName,
                CreatedAtUtc = metadata.CreatedAtUtc,
                SavedAtUtc = metadata.SavedAtUtc,
            },
            Simulation = new SimulationSnapshotV7
            {
                TimeMilliseconds = state.Time.Milliseconds,
                ShipAllocatorNextId = state.ShipIdAllocator.NextId,
                OrderAllocatorNextId = state.OrderIdAllocator.NextId,
                PlayerShipId = state.PlayerShipId.Value,
                Scheduler = CaptureSchedulerV7(state.Scheduler),
                StrategicMap = CaptureStrategicMapV2(state.StrategicMap),
                Ships = [.. state.Ships.OrderBy(ship => ship.InstanceId.Value).Select(CaptureShipV7)],
                Factions = [.. state.Factions.OrderBy(faction => faction.Id.Value).Select(CaptureFactionV7)],
            },
        };
    }

    private static SaveModelsV7.SchedulerSnapshotV7 CaptureSchedulerV7(SimulationScheduler scheduler) =>
        new()
        {
            NextWorkId = scheduler.NextWorkId,
            NextSequence = scheduler.NextSequence,
            OutstandingWork =
            [
                .. scheduler.OutstandingWork.Select(work => new SaveModelsV7.ScheduledWorkSnapshotV7
                {
                    Id = work.Id.Value,
                    DueTimeMilliseconds = work.DueTime.Milliseconds,
                    Sequence = work.Sequence,
                    Kind = CaptureWorkKind(work.Kind),
                    TargetKind = CaptureTargetKind(work.Target.Kind),
                    TargetShipId = work.Target.ShipId?.Value,
                    TargetFactionId = work.Target.FactionId?.Value,
                }),
            ],
        };

    private static StrategicMapSnapshotV2 CaptureStrategicMapV2(StrategicMap map) =>
        new()
        {
            Locations =
            [
                .. map.Locations.Select(location => new StrategicLocationSnapshotV2
                {
                    Id = location.Id.Value,
                    DisplayName = location.DisplayName,
                    Position = new StrategicPositionSnapshotV2
                    {
                        XUnitless = location.Position.X,
                        YUnitless = location.Position.Y,
                    },
                }),
            ],
            Routes =
            [
                .. map.Routes.Select(route => new StrategicRouteSnapshotV2
                {
                    Origin = route.Origin.Value,
                    Destination = route.Destination.Value,
                    DurationMilliseconds = route.Duration.Milliseconds,
                }),
            ],
        };

    private static ShipSnapshotV7 CaptureShipV7(ShipState ship) =>
        new()
        {
            InstanceId = ship.InstanceId.Value,
            DefinitionId = ship.DefinitionId.Value,
            DisplayName = ship.VesselDisplayName,
            TacticalPosition = new TacticalPositionSnapshotV2
            {
                XKilometers = ship.TacticalPosition.XKilometers,
                YKilometers = ship.TacticalPosition.YKilometers,
            },
            TacticalMotion = new TacticalMotionSnapshotV2
            {
                HeadingDegrees = ship.TacticalMotion.Heading.Value,
                SpeedKilometersPerSecond = ship.TacticalMotion.Speed.Value,
            },
            Engineering = new SaveModelsV5.EngineeringSnapshotV5
            {
                GenerationCondition = ship.Engineering.GenerationCondition.Value,
                SensorCondition = ship.Engineering.SensorCondition.Value,
                ImpulseCondition = ship.Engineering.ImpulseCondition.Value,
                SensorAllocation = ship.Engineering.Allocation.Sensors.Value,
                ImpulseAllocation = ship.Engineering.Allocation.ImpulsePropulsion.Value,
                ActiveRepair = ship.Engineering.ActiveRepair is null
                    ? null
                    : new SaveModelsV5.SystemRepairSnapshotV5
                    {
                        TargetSystem = ship.Engineering.ActiveRepair.TargetSystem.Value,
                        StartingCondition = ship.Engineering.ActiveRepair.StartingCondition.Value,
                        TargetCondition = ship.Engineering.ActiveRepair.TargetCondition.Value,
                        StartedAtMilliseconds = ship.Engineering.ActiveRepair.StartedAt.Milliseconds,
                        ExpectedCompletionMilliseconds = ship.Engineering.ActiveRepair.ExpectedCompletion.Milliseconds,
                        ScheduledCompletionId = ship.Engineering.ActiveRepair.ScheduledCompletionId.Value,
                    },
            },
            StrategicState = CaptureStrategicStateV2(ship.StrategicState),
            ActiveOrder = CaptureOrderV3(ship.ActiveOrder),
            SensorKnowledge = CaptureSensorKnowledgeV6(ship.SensorKnowledge),
            AutonomousState = CaptureAutonomousStateV4(ship.AutonomousState),
            DirectControllerFactionId = ship.DirectControllerFactionId?.Value,
        };

    private static SaveModelsV7.FactionSnapshotV7 CaptureFactionV7(FactionState faction) =>
        new()
        {
            Id = faction.Id.Value,
            DefinitionId = faction.DefinitionId.Value,
            PresenceObjective = faction.PresenceObjective is null
                ? null
                : new SaveModelsV7.EstablishPresenceObjectiveSnapshotV7
                {
                    TargetLocationId = faction.PresenceObjective.TargetLocationId.Value,
                    Status = CaptureObjectiveStatus(faction.PresenceObjective.Status),
                    AssignedShipId = faction.PresenceObjective.AssignedShipId?.Value,
                    AssignedOrderId = faction.PresenceObjective.AssignedOrderId?.Value,
                },
            PendingDecisionWake = faction.PendingDecisionWake is null
                ? null
                : new SaveModelsV7.PendingFactionDecisionWakeSnapshotV7
                {
                    WorkId = faction.PendingDecisionWake.WorkId.Value,
                    DueTimeMilliseconds = faction.PendingDecisionWake.DueTime.Milliseconds,
                },
        };

    private static SaveModelsV6.SensorKnowledgeSnapshotV6 CaptureSensorKnowledgeV6(SensorKnowledge knowledge) =>
        new()
        {
            NextContactId = knowledge.NextContactId,
            Contacts = [.. knowledge.Contacts.Select(CaptureContactV6)],
            ActiveScan = knowledge.ActiveScan is null
                ? null
                : new SaveModelsV4.ActiveSensorScanSnapshotV4
                {
                    TargetContactId = knowledge.ActiveScan.TargetContactId.Value,
                    StartedAtMilliseconds = knowledge.ActiveScan.StartedAt.Milliseconds,
                    ExpectedCompletionMilliseconds = knowledge.ActiveScan.ExpectedCompletion.Milliseconds,
                    ScheduledCompletionId = knowledge.ActiveScan.ScheduledCompletionId.Value,
                },
        };

    private static SaveModelsV6.SensorContactSnapshotV6 CaptureContactV6(SensorContactTrack contact) =>
        new()
        {
            Id = contact.Id.Value,
            TargetShipId = contact.TargetShipId.Value,
            LastObservedPosition = new TacticalPositionSnapshotV2
            {
                XKilometers = contact.LastObservedPosition.XKilometers,
                YKilometers = contact.LastObservedPosition.YKilometers,
            },
            LastObservedAtMilliseconds = contact.LastObservedAt.Milliseconds,
            // A null frame is written through as null rather than substituted: it marks a legacy
            // observation the schema history genuinely never qualified, and a save must not gain a
            // location the observer never recorded just because the writer had one available.
            ObservedAtLocationId = contact.ObservedAtLocationId?.Value,
            Status = CaptureContactStatus(contact.Status),
            Identification = CaptureContactIdentification(contact.Identification),
            KnownVesselDisplayName = contact.KnownVesselDisplayName,
            KnownDesignDisplayName = contact.KnownDesignDisplayName,
            LossWorkId = contact.LossWorkId?.Value,
            LossDueTimeMilliseconds = contact.LossDueTime?.Milliseconds,
        };

    private static SaveModelsV4.ShipAutonomousSnapshotV4 CaptureAutonomousStateV4(ShipAutonomousState autonomous) =>
        new()
        {
            ContactPosture = autonomous.ContactPosture is null
                ? null
                : CaptureContactPosture(autonomous.ContactPosture.Value),
            PendingContactDecisionWake = autonomous.PendingContactDecisionWake is null
                ? null
                : new SaveModelsV4.ContactDecisionWakeSnapshotV4
                {
                    ScheduledWorkId = autonomous.PendingContactDecisionWake.ScheduledWorkId.Value,
                    DueTimeMilliseconds = autonomous.PendingContactDecisionWake.DueTime.Milliseconds,
                },
        };

    private static ShipOrderSnapshotV3? CaptureOrderV3(ShipOrder? order) =>
        order switch
        {
            null => null,
            TravelToOrder travel => new TravelToOrderSnapshotV3
            {
                Id = travel.Id.Value,
                Destination = travel.Destination.Value,
            },
            PatrolRouteOrder patrol => new PatrolRouteOrderSnapshotV3
            {
                Id = patrol.Id.Value,
                Waypoints = [.. patrol.Waypoints.Select(waypoint => waypoint.Value)],
                NextWaypointIndex = patrol.NextWaypointIndex,
            },
            HoldUntilOrder hold => new HoldUntilOrderSnapshotV3
            {
                Id = hold.Id.Value,
                UntilMilliseconds = hold.Until.Milliseconds,
                ScheduledWakeId = hold.ScheduledWakeId.Value,
            },
            _ => throw new InvalidOperationException("Cannot persist an unknown ship order kind."),
        };

    private static StrategicStateSnapshotV2 CaptureStrategicStateV2(ShipStrategicState state) =>
        state switch
        {
            AtLocationState atLocation => new StrategicStateSnapshotV2
            {
                Kind = AtLocationKind,
                LocationId = atLocation.LocationId.Value,
                Travel = null,
            },
            TravelingState traveling => new StrategicStateSnapshotV2
            {
                Kind = TravelingKind,
                LocationId = null,
                Travel = new TravelSnapshotV2
                {
                    Origin = traveling.Travel.Origin.Value,
                    Destination = traveling.Travel.Destination.Value,
                    DepartureMilliseconds = traveling.Travel.Departure.Milliseconds,
                    ExpectedArrivalMilliseconds = traveling.Travel.ExpectedArrival.Milliseconds,
                    ScheduledArrivalId = traveling.Travel.ScheduledArrivalId.Value,
                },
            },
            _ => throw new InvalidOperationException("Cannot persist an unknown strategic state kind."),
        };

    private static string CaptureWorkKind(ScheduledWorkKind kind) =>
        kind switch
        {
            ScheduledWorkKind.TravelArrival => TravelArrivalKind,
            ScheduledWorkKind.SystemRepairCompletion => SystemRepairCompletionKind,
            ScheduledWorkKind.OrderWake => OrderWakeKind,
            ScheduledWorkKind.SensorContactLoss => SensorContactLossKind,
            ScheduledWorkKind.ActiveSensorScanCompletion => ActiveSensorScanCompletionKind,
            ScheduledWorkKind.ShipContactDecisionWake => ShipContactDecisionWakeKind,
            ScheduledWorkKind.FactionDecisionWake => FactionDecisionWakeKind,
            _ => throw new InvalidOperationException("Cannot persist an unknown scheduled work kind."),
        };

    private static string CaptureTargetKind(ScheduledWorkTargetKind kind) =>
        kind switch
        {
            ScheduledWorkTargetKind.Ship => ShipTargetKind,
            ScheduledWorkTargetKind.Faction => FactionTargetKind,
            _ => throw new InvalidOperationException("Cannot persist an unknown scheduled-work target kind."),
        };

    private static string CaptureObjectiveStatus(FactionObjectiveStatus status) =>
        status switch
        {
            FactionObjectiveStatus.Pending => PendingObjectiveStatus,
            FactionObjectiveStatus.Assigned => AssignedObjectiveStatus,
            FactionObjectiveStatus.Satisfied => SatisfiedObjectiveStatus,
            _ => throw new InvalidOperationException("Cannot persist an unknown faction objective status."),
        };

    private static string CaptureContactStatus(SensorContactStatus status) =>
        status switch
        {
            SensorContactStatus.Current => CurrentContactStatus,
            SensorContactStatus.Stale => StaleContactStatus,
            SensorContactStatus.Lost => LostContactStatus,
            _ => throw new InvalidOperationException("Cannot persist an unknown sensor contact status."),
        };

    private static string CaptureContactIdentification(SensorContactIdentification identification) =>
        identification switch
        {
            SensorContactIdentification.Detected => DetectedContactIdentification,
            SensorContactIdentification.Identified => IdentifiedContactIdentification,
            _ => throw new InvalidOperationException("Cannot persist an unknown sensor contact identification."),
        };

    private static string CaptureContactPosture(ShipContactPosture posture) =>
        posture switch
        {
            ShipContactPosture.CautiousContact => CautiousContactPosture,
            _ => throw new InvalidOperationException("Cannot persist an unknown ship contact posture."),
        };

    private static LoadedGameSave LoadV1(
        byte[] json,
        ShipDefinitionCatalog catalog,
        FactionDefinitionCatalog factionCatalog,
        string sourceIdentity
    )
    {
        try
        {
            SaveEnvelopeV1 envelope =
                JsonSerializer.Deserialize<SaveEnvelopeV1>(json, SerializerOptions)
                ?? throw new JsonException("The save root must be an object.");
            ValidateEnvelopeV1(envelope);
            SaveEnvelopeV2 migrated = MigrateV1ToV2(envelope, catalog);
            ValidateCandidateV2(migrated, catalog);
            SaveEnvelopeV3 migratedV3 = MigrateV2ToV3(migrated);
            ValidateCandidateV3(migratedV3, catalog);
            SaveEnvelopeV4 migratedV4 = MigrateV3ToV4(migratedV3);
            ValidateCandidateV4(migratedV4, catalog);
            SaveEnvelopeV5 migratedV5 = MigrateV4ToV5(migratedV4, catalog);
            ValidateCandidateV5(migratedV5, catalog);
            SaveEnvelopeV6 migratedV6 = MigrateV5ToV6(migratedV5);
            ValidateCandidateV6(migratedV6, catalog);
            return RestoreV7(MigrateV6ToV7(migratedV6), catalog, factionCatalog);
        }
        catch (GamePersistenceException)
        {
            throw;
        }
        catch (JsonException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception
                    is ArgumentException
                        or InvalidOperationException
                        or KeyNotFoundException
                        or OverflowException
            )
        {
            throw Failure(
                GamePersistenceFailure.InvalidData,
                sourceIdentity,
                $"violates the V1 semantic contract: {exception.Message}",
                exception
            );
        }
    }

    private static LoadedGameSave LoadV2(
        byte[] json,
        ShipDefinitionCatalog catalog,
        FactionDefinitionCatalog factionCatalog,
        string sourceIdentity
    )
    {
        try
        {
            SaveEnvelopeV2 envelope =
                JsonSerializer.Deserialize<SaveEnvelopeV2>(json, SerializerOptions)
                ?? throw new JsonException("The save root must be an object.");
            ValidateCandidateV2(envelope, catalog);
            SaveEnvelopeV3 migrated = MigrateV2ToV3(envelope);
            ValidateCandidateV3(migrated, catalog);
            SaveEnvelopeV4 migratedV4 = MigrateV3ToV4(migrated);
            ValidateCandidateV4(migratedV4, catalog);
            SaveEnvelopeV5 migratedV5 = MigrateV4ToV5(migratedV4, catalog);
            ValidateCandidateV5(migratedV5, catalog);
            SaveEnvelopeV6 migratedV6 = MigrateV5ToV6(migratedV5);
            ValidateCandidateV6(migratedV6, catalog);
            return RestoreV7(MigrateV6ToV7(migratedV6), catalog, factionCatalog);
        }
        catch (GamePersistenceException)
        {
            throw;
        }
        catch (JsonException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception
                    is ArgumentException
                        or InvalidOperationException
                        or KeyNotFoundException
                        or OverflowException
            )
        {
            throw Failure(
                GamePersistenceFailure.InvalidData,
                sourceIdentity,
                $"violates the V2 semantic contract: {exception.Message}",
                exception
            );
        }
    }

    private static LoadedGameSave LoadV3(
        byte[] json,
        ShipDefinitionCatalog catalog,
        FactionDefinitionCatalog factionCatalog,
        string sourceIdentity
    )
    {
        try
        {
            SaveEnvelopeV3 envelope =
                JsonSerializer.Deserialize<SaveEnvelopeV3>(json, SerializerOptions)
                ?? throw new JsonException("The save root must be an object.");
            ValidateCandidateV3(envelope, catalog);
            SaveEnvelopeV4 migratedV4 = MigrateV3ToV4(envelope);
            ValidateCandidateV4(migratedV4, catalog);
            SaveEnvelopeV5 migratedV5 = MigrateV4ToV5(migratedV4, catalog);
            ValidateCandidateV5(migratedV5, catalog);
            SaveEnvelopeV6 migratedV6 = MigrateV5ToV6(migratedV5);
            ValidateCandidateV6(migratedV6, catalog);
            return RestoreV7(MigrateV6ToV7(migratedV6), catalog, factionCatalog);
        }
        catch (GamePersistenceException)
        {
            throw;
        }
        catch (JsonException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception
                    is ArgumentException
                        or InvalidOperationException
                        or KeyNotFoundException
                        or OverflowException
            )
        {
            throw Failure(
                GamePersistenceFailure.InvalidData,
                sourceIdentity,
                $"violates the V3 semantic contract: {exception.Message}",
                exception
            );
        }
    }

    private static LoadedGameSave LoadV4(
        byte[] json,
        ShipDefinitionCatalog catalog,
        FactionDefinitionCatalog factionCatalog,
        string sourceIdentity
    )
    {
        try
        {
            SaveEnvelopeV4 envelope =
                JsonSerializer.Deserialize<SaveEnvelopeV4>(json, SerializerOptions)
                ?? throw new JsonException("The save root must be an object.");
            ValidateCandidateV4(envelope, catalog);
            SaveEnvelopeV5 migratedV5 = MigrateV4ToV5(envelope, catalog);
            ValidateCandidateV5(migratedV5, catalog);
            SaveEnvelopeV6 migratedV6 = MigrateV5ToV6(migratedV5);
            ValidateCandidateV6(migratedV6, catalog);
            return RestoreV7(MigrateV6ToV7(migratedV6), catalog, factionCatalog);
        }
        catch (GamePersistenceException)
        {
            throw;
        }
        catch (JsonException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception
                    is ArgumentException
                        or InvalidOperationException
                        or KeyNotFoundException
                        or OverflowException
            )
        {
            throw Failure(
                GamePersistenceFailure.InvalidData,
                sourceIdentity,
                $"violates the V4 semantic contract: {exception.Message}",
                exception
            );
        }
    }

    private static LoadedGameSave LoadV5(
        byte[] json,
        ShipDefinitionCatalog catalog,
        FactionDefinitionCatalog factionCatalog,
        string sourceIdentity
    )
    {
        try
        {
            SaveEnvelopeV5 envelope =
                JsonSerializer.Deserialize<SaveEnvelopeV5>(json, SerializerOptions)
                ?? throw new JsonException("The save root must be an object.");
            ValidateCandidateV5(envelope, catalog);
            SaveEnvelopeV6 migratedV6 = MigrateV5ToV6(envelope);
            ValidateCandidateV6(migratedV6, catalog);
            return RestoreV7(MigrateV6ToV7(migratedV6), catalog, factionCatalog);
        }
        catch (GamePersistenceException)
        {
            throw;
        }
        catch (JsonException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception
                    is ArgumentException
                        or InvalidOperationException
                        or KeyNotFoundException
                        or OverflowException
            )
        {
            throw Failure(
                GamePersistenceFailure.InvalidData,
                sourceIdentity,
                $"violates the V5 semantic contract: {exception.Message}",
                exception
            );
        }
    }

    private static LoadedGameSave LoadV6(
        byte[] json,
        ShipDefinitionCatalog catalog,
        FactionDefinitionCatalog factionCatalog,
        string sourceIdentity
    )
    {
        try
        {
            SaveEnvelopeV6 envelope =
                JsonSerializer.Deserialize<SaveEnvelopeV6>(json, SerializerOptions)
                ?? throw new JsonException("The save root must be an object.");
            ValidateCandidateV6(envelope, catalog);
            return RestoreV7(MigrateV6ToV7(envelope), catalog, factionCatalog);
        }
        catch (GamePersistenceException)
        {
            throw;
        }
        catch (JsonException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception
                    is ArgumentException
                        or InvalidOperationException
                        or KeyNotFoundException
                        or OverflowException
            )
        {
            throw Failure(
                GamePersistenceFailure.InvalidData,
                sourceIdentity,
                $"violates the V6 semantic contract: {exception.Message}",
                exception
            );
        }
    }

    private static LoadedGameSave LoadV7(
        byte[] json,
        ShipDefinitionCatalog catalog,
        FactionDefinitionCatalog factionCatalog,
        string sourceIdentity
    )
    {
        try
        {
            SaveEnvelopeV7 envelope =
                JsonSerializer.Deserialize<SaveEnvelopeV7>(json, SerializerOptions)
                ?? throw new JsonException("The save root must be an object.");
            return RestoreV7(envelope, catalog, factionCatalog);
        }
        catch (GamePersistenceException)
        {
            throw;
        }
        catch (JsonException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception
                    is ArgumentException
                        or InvalidOperationException
                        or KeyNotFoundException
                        or OverflowException
            )
        {
            throw Failure(
                GamePersistenceFailure.InvalidData,
                sourceIdentity,
                $"violates the V7 semantic contract: {exception.Message}",
                exception
            );
        }
    }

    private static SaveEnvelopeV2 MigrateV1ToV2(SaveEnvelopeV1 envelope, ShipDefinitionCatalog catalog)
    {
        SimulationSnapshotV1 source = envelope.Simulation;
        if (
            source.Scheduler is null
            || source.StrategicMap is null
            || source.StrategicState is null
            || source.PlayerShip is null
        )
        {
            throw new InvalidOperationException("Required V1 simulation members cannot be null.");
        }

        PlayerShipSnapshotV1 player = source.PlayerShip;
        ValidateText(player.DefinitionId, "Ship definition identity", ShipDefinitionId.MaximumLength);
        ShipDefinition definition = catalog.GetRequired(new ShipDefinitionId(player.DefinitionId));

        // V1 predates runtime vessel names. During pre-1.0 migration only, the authored design label
        // supplies the missing value deterministically; V2 persists it and never repeats this fallback.
        return new SaveEnvelopeV2
        {
            SchemaVersion = V2SchemaVersion,
            SimulationRulesVersion = envelope.SimulationRulesVersion,
            Metadata = new SaveMetadataV2
            {
                SaveId = envelope.Metadata.SaveId,
                DisplayName = envelope.Metadata.DisplayName,
                CreatedAtUtc = envelope.Metadata.CreatedAtUtc,
                SavedAtUtc = envelope.Metadata.SavedAtUtc,
            },
            Simulation = new SimulationSnapshotV2
            {
                TimeMilliseconds = source.TimeMilliseconds,
                ShipAllocatorNextId = source.ShipAllocatorNextId,
                PlayerShipId = player.InstanceId,
                Scheduler = MigrateSchedulerV1(source.Scheduler, player.InstanceId),
                StrategicMap = MigrateMapV1(source.StrategicMap),
                Ships =
                [
                    new ShipSnapshotV2
                    {
                        InstanceId = player.InstanceId,
                        DefinitionId = player.DefinitionId,
                        DisplayName = definition.DesignDisplayName,
                        TacticalPosition = MigrateTacticalPositionV1(player.TacticalPosition),
                        TacticalMotion = MigrateTacticalMotionV1(player.TacticalMotion),
                        SensorIntegrity = player.SensorIntegrity,
                        SensorRepair = MigrateSensorRepairV1(player.SensorRepair),
                        StrategicState = MigrateStrategicStateV1(source.StrategicState),
                    },
                ],
            },
        };
    }

    private static SaveEnvelopeV3 MigrateV2ToV3(SaveEnvelopeV2 envelope) =>
        new()
        {
            SchemaVersion = V3SchemaVersion,
            SimulationRulesVersion = V3SimulationRulesVersion,
            Metadata = envelope.Metadata,
            Simulation = new SimulationSnapshotV3
            {
                TimeMilliseconds = envelope.Simulation.TimeMilliseconds,
                ShipAllocatorNextId = envelope.Simulation.ShipAllocatorNextId,
                OrderAllocatorNextId = ShipOrderIdAllocator.Create().NextId,
                PlayerShipId = envelope.Simulation.PlayerShipId,
                Scheduler = envelope.Simulation.Scheduler,
                StrategicMap = envelope.Simulation.StrategicMap,
                Ships =
                [
                    .. envelope.Simulation.Ships.Select(ship => new ShipSnapshotV3
                    {
                        InstanceId = ship.InstanceId,
                        DefinitionId = ship.DefinitionId,
                        DisplayName = ship.DisplayName,
                        TacticalPosition = ship.TacticalPosition,
                        TacticalMotion = ship.TacticalMotion,
                        SensorIntegrity = ship.SensorIntegrity,
                        SensorRepair = ship.SensorRepair,
                        StrategicState = ship.StrategicState,
                        ActiveOrder = null,
                    }),
                ],
            },
        };

    private static SaveEnvelopeV4 MigrateV3ToV4(SaveEnvelopeV3 envelope) =>
        new()
        {
            SchemaVersion = V4SchemaVersion,
            SimulationRulesVersion = V4SimulationRulesVersion,
            Metadata = envelope.Metadata,
            Simulation = new SimulationSnapshotV4
            {
                TimeMilliseconds = envelope.Simulation.TimeMilliseconds,
                ShipAllocatorNextId = envelope.Simulation.ShipAllocatorNextId,
                OrderAllocatorNextId = envelope.Simulation.OrderAllocatorNextId,
                PlayerShipId = envelope.Simulation.PlayerShipId,
                Scheduler = envelope.Simulation.Scheduler,
                StrategicMap = envelope.Simulation.StrategicMap,
                Ships =
                [
                    .. envelope.Simulation.Ships.Select(ship => new ShipSnapshotV4
                    {
                        InstanceId = ship.InstanceId,
                        DefinitionId = ship.DefinitionId,
                        DisplayName = ship.DisplayName,
                        TacticalPosition = ship.TacticalPosition,
                        TacticalMotion = ship.TacticalMotion,
                        SensorIntegrity = ship.SensorIntegrity,
                        SensorRepair = ship.SensorRepair,
                        StrategicState = ship.StrategicState,
                        ActiveOrder = ship.ActiveOrder,
                        SensorKnowledge = new SaveModelsV4.SensorKnowledgeSnapshotV4
                        {
                            NextContactId = 1,
                            Contacts = [],
                            ActiveScan = null,
                        },
                        AutonomousState = new SaveModelsV4.ShipAutonomousSnapshotV4
                        {
                            ContactPosture = null,
                            PendingContactDecisionWake = null,
                        },
                    }),
                ],
            },
        };

    private static SaveEnvelopeV5 MigrateV4ToV5(SaveEnvelopeV4 envelope, ShipDefinitionCatalog catalog) =>
        new()
        {
            SchemaVersion = V5SchemaVersion,
            SimulationRulesVersion = V5SimulationRulesVersion,
            Metadata = envelope.Metadata,
            Simulation = new SimulationSnapshotV5
            {
                TimeMilliseconds = envelope.Simulation.TimeMilliseconds,
                ShipAllocatorNextId = envelope.Simulation.ShipAllocatorNextId,
                OrderAllocatorNextId = envelope.Simulation.OrderAllocatorNextId,
                PlayerShipId = envelope.Simulation.PlayerShipId,
                Scheduler = MigrateSchedulerV4(envelope.Simulation.Scheduler),
                StrategicMap = envelope.Simulation.StrategicMap,
                Ships = [.. envelope.Simulation.Ships.Select(ship => MigrateShipV4(ship, catalog))],
            },
        };

    private static SchedulerSnapshotV2 MigrateSchedulerV4(SchedulerSnapshotV2 scheduler) =>
        new()
        {
            NextWorkId = scheduler.NextWorkId,
            NextSequence = scheduler.NextSequence,
            OutstandingWork =
            [
                .. scheduler.OutstandingWork.Select(work => new ScheduledWorkSnapshotV2
                {
                    Id = work.Id,
                    DueTimeMilliseconds = work.DueTimeMilliseconds,
                    Sequence = work.Sequence,
                    Kind = string.Equals(work.Kind, SensorRepairCompletionKind, StringComparison.Ordinal)
                        ? SystemRepairCompletionKind
                        : work.Kind,
                    TargetShipId = work.TargetShipId,
                }),
            ],
        };

    private static ShipSnapshotV5 MigrateShipV4(ShipSnapshotV4 ship, ShipDefinitionCatalog catalog)
    {
        ShipDefinition definition = catalog.GetRequired(new ShipDefinitionId(ship.DefinitionId));
        return new ShipSnapshotV5
        {
            InstanceId = ship.InstanceId,
            DefinitionId = ship.DefinitionId,
            DisplayName = ship.DisplayName,
            TacticalPosition = ship.TacticalPosition,
            TacticalMotion = ship.TacticalMotion,
            Engineering = new SaveModelsV5.EngineeringSnapshotV5
            {
                GenerationCondition = 1,
                SensorCondition = ship.SensorIntegrity,
                ImpulseCondition = 1,
                SensorAllocation = definition.Engineering.NominalSensorDemand.Value,
                ImpulseAllocation = definition.Engineering.NominalImpulseDemand.Value,
                // V4 encoded only sensor repair. V5 changes both the target identity and
                // scheduled kind while preserving the exact temporal/work correlation.
                ActiveRepair = ship.SensorRepair is null
                    ? null
                    : new SaveModelsV5.SystemRepairSnapshotV5
                    {
                        TargetSystem = ShipSystemId.Sensors.Value,
                        StartingCondition = ship.SensorRepair.StartingIntegrity,
                        TargetCondition = ship.SensorRepair.TargetIntegrity,
                        StartedAtMilliseconds = ship.SensorRepair.StartedAtMilliseconds,
                        ExpectedCompletionMilliseconds = ship.SensorRepair.ExpectedCompletionMilliseconds,
                        ScheduledCompletionId = ship.SensorRepair.ScheduledCompletionId,
                    },
            },
            StrategicState = ship.StrategicState,
            ActiveOrder = ship.ActiveOrder,
            SensorKnowledge = ship.SensorKnowledge,
            AutonomousState = ship.AutonomousState,
        };
    }

    /// <remarks>
    /// Every migrated contact keeps a null observed location. The frame is deliberately NOT derived
    /// from the observer's <c>StrategicStateSnapshotV2</c>, nor from the target's:
    /// <list type="bullet">
    /// <item>V5 never validated observer/target co-location, so a structurally valid V5 file may hold
    /// a current contact whose observer is traveling or somewhere else entirely.</item>
    /// <item>A stale or lost contact's observation location is unrecoverable in principle — V5 keeps
    /// the observed position and time while both ships remain free to travel afterwards, and those
    /// contacts are exactly the population retained reports exist to serve.</item>
    /// <item>ADR 0006 treats save data as untrusted input and forbids a migration from silently
    /// inventing consequential state; a derived frame would be an unverifiable claim presented to the
    /// player as an observation the observer never recorded.</item>
    /// </list>
    /// Downstream, a null frame keeps the contact on the tactical surface while omitting it from the
    /// strategic report projection, so a V5 save loses nothing it ever carried.
    /// </remarks>
    private static SaveEnvelopeV6 MigrateV5ToV6(SaveEnvelopeV5 envelope) =>
        new()
        {
            SchemaVersion = V6SchemaVersion,
            SimulationRulesVersion = V6SimulationRulesVersion,
            Metadata = envelope.Metadata,
            Simulation = new SimulationSnapshotV6
            {
                TimeMilliseconds = envelope.Simulation.TimeMilliseconds,
                ShipAllocatorNextId = envelope.Simulation.ShipAllocatorNextId,
                OrderAllocatorNextId = envelope.Simulation.OrderAllocatorNextId,
                PlayerShipId = envelope.Simulation.PlayerShipId,
                Scheduler = envelope.Simulation.Scheduler,
                StrategicMap = envelope.Simulation.StrategicMap,
                Ships = [.. envelope.Simulation.Ships.Select(MigrateShipV5)],
            },
        };

    /// <remarks>
    /// V6 has no political state. The migration preserves that absence rather than bootstrapping
    /// current faction definitions, which would invent controllers, objectives, and future work.
    /// Historical scheduled work remains ship-owned with its exact identity and ordering fields.
    /// </remarks>
    private static SaveEnvelopeV7 MigrateV6ToV7(SaveEnvelopeV6 envelope) =>
        new()
        {
            SchemaVersion = CurrentSchemaVersion,
            SimulationRulesVersion = CurrentSimulationRulesVersion,
            Metadata = envelope.Metadata,
            Simulation = new SimulationSnapshotV7
            {
                TimeMilliseconds = envelope.Simulation.TimeMilliseconds,
                ShipAllocatorNextId = envelope.Simulation.ShipAllocatorNextId,
                OrderAllocatorNextId = envelope.Simulation.OrderAllocatorNextId,
                PlayerShipId = envelope.Simulation.PlayerShipId,
                Scheduler = new SaveModelsV7.SchedulerSnapshotV7
                {
                    NextWorkId = envelope.Simulation.Scheduler.NextWorkId,
                    NextSequence = envelope.Simulation.Scheduler.NextSequence,
                    OutstandingWork =
                    [
                        .. envelope.Simulation.Scheduler.OutstandingWork.Select(
                            work => new SaveModelsV7.ScheduledWorkSnapshotV7
                            {
                                Id = work.Id,
                                DueTimeMilliseconds = work.DueTimeMilliseconds,
                                Sequence = work.Sequence,
                                Kind = work.Kind,
                                TargetKind = ShipTargetKind,
                                TargetShipId = work.TargetShipId,
                                TargetFactionId = null,
                            }
                        ),
                    ],
                },
                StrategicMap = envelope.Simulation.StrategicMap,
                Ships =
                [
                    .. envelope.Simulation.Ships.Select(ship => new ShipSnapshotV7
                    {
                        InstanceId = ship.InstanceId,
                        DefinitionId = ship.DefinitionId,
                        DisplayName = ship.DisplayName,
                        TacticalPosition = ship.TacticalPosition,
                        TacticalMotion = ship.TacticalMotion,
                        Engineering = ship.Engineering,
                        StrategicState = ship.StrategicState,
                        ActiveOrder = ship.ActiveOrder,
                        SensorKnowledge = ship.SensorKnowledge,
                        AutonomousState = ship.AutonomousState,
                        DirectControllerFactionId = null,
                    }),
                ],
                Factions = [],
            },
        };

    private static ShipSnapshotV6 MigrateShipV5(ShipSnapshotV5 ship) =>
        new()
        {
            InstanceId = ship.InstanceId,
            DefinitionId = ship.DefinitionId,
            DisplayName = ship.DisplayName,
            TacticalPosition = ship.TacticalPosition,
            TacticalMotion = ship.TacticalMotion,
            Engineering = ship.Engineering,
            StrategicState = ship.StrategicState,
            ActiveOrder = ship.ActiveOrder,
            SensorKnowledge = new SaveModelsV6.SensorKnowledgeSnapshotV6
            {
                NextContactId = ship.SensorKnowledge.NextContactId,
                Contacts =
                [
                    .. ship.SensorKnowledge.Contacts.Select(contact => new SaveModelsV6.SensorContactSnapshotV6
                    {
                        Id = contact.Id,
                        TargetShipId = contact.TargetShipId,
                        LastObservedPosition = contact.LastObservedPosition,
                        LastObservedAtMilliseconds = contact.LastObservedAtMilliseconds,
                        ObservedAtLocationId = null,
                        Status = contact.Status,
                        Identification = contact.Identification,
                        KnownVesselDisplayName = contact.KnownVesselDisplayName,
                        KnownDesignDisplayName = contact.KnownDesignDisplayName,
                        LossWorkId = contact.LossWorkId,
                        LossDueTimeMilliseconds = contact.LossDueTimeMilliseconds,
                    }),
                ],
                ActiveScan = ship.SensorKnowledge.ActiveScan,
            },
            AutonomousState = ship.AutonomousState,
        };

    private static SchedulerSnapshotV2 MigrateSchedulerV1(SchedulerSnapshotV1 source, long targetShipId)
    {
        if (source.OutstandingWork is null)
        {
            throw new InvalidOperationException("V1 outstanding scheduler work is required.");
        }

        return new SchedulerSnapshotV2
        {
            NextWorkId = source.NextWorkId,
            NextSequence = source.NextSequence,
            OutstandingWork =
            [
                .. source.OutstandingWork.Select(item =>
                {
                    if (item is null)
                    {
                        throw new InvalidOperationException("V1 scheduler work items cannot be null.");
                    }

                    return new ScheduledWorkSnapshotV2
                    {
                        Id = item.Id,
                        DueTimeMilliseconds = item.DueTimeMilliseconds,
                        Sequence = item.Sequence,
                        Kind = item.Kind,
                        TargetShipId = targetShipId,
                    };
                }),
            ],
        };
    }

    private static StrategicMapSnapshotV2 MigrateMapV1(StrategicMapSnapshotV1 source)
    {
        if (source.Locations is null || source.Routes is null)
        {
            throw new InvalidOperationException("V1 strategic map collections are required.");
        }

        return new StrategicMapSnapshotV2
        {
            Locations =
            [
                .. source.Locations.Select(location =>
                {
                    if (location is null || location.Position is null)
                    {
                        throw new InvalidOperationException("V1 strategic location data cannot be null.");
                    }

                    return new StrategicLocationSnapshotV2
                    {
                        Id = location.Id,
                        DisplayName = location.DisplayName,
                        Position = new StrategicPositionSnapshotV2
                        {
                            XUnitless = location.Position.XUnitless,
                            YUnitless = location.Position.YUnitless,
                        },
                    };
                }),
            ],
            Routes =
            [
                .. source.Routes.Select(route =>
                {
                    if (route is null)
                    {
                        throw new InvalidOperationException("V1 strategic route data cannot be null.");
                    }

                    return new StrategicRouteSnapshotV2
                    {
                        Origin = route.Origin,
                        Destination = route.Destination,
                        DurationMilliseconds = route.DurationMilliseconds,
                    };
                }),
            ],
        };
    }

    private static TacticalPositionSnapshotV2 MigrateTacticalPositionV1(TacticalPositionSnapshotV1? source)
    {
        if (source is null)
        {
            throw new InvalidOperationException("V1 tactical position is required.");
        }

        return new TacticalPositionSnapshotV2 { XKilometers = source.XKilometers, YKilometers = source.YKilometers };
    }

    private static TacticalMotionSnapshotV2 MigrateTacticalMotionV1(TacticalMotionSnapshotV1? source)
    {
        if (source is null)
        {
            throw new InvalidOperationException("V1 tactical motion is required.");
        }

        return new TacticalMotionSnapshotV2
        {
            HeadingDegrees = source.HeadingDegrees,
            SpeedKilometersPerSecond = source.SpeedKilometersPerSecond,
        };
    }

    private static SensorRepairSnapshotV2? MigrateSensorRepairV1(SensorRepairSnapshotV1? source) =>
        source is null
            ? null
            : new SensorRepairSnapshotV2
            {
                StartingIntegrity = source.StartingIntegrity,
                TargetIntegrity = source.TargetIntegrity,
                StartedAtMilliseconds = source.StartedAtMilliseconds,
                ExpectedCompletionMilliseconds = source.ExpectedCompletionMilliseconds,
                ScheduledCompletionId = source.ScheduledCompletionId,
            };

    private static StrategicStateSnapshotV2 MigrateStrategicStateV1(StrategicStateSnapshotV1 source) =>
        new()
        {
            Kind = source.Kind,
            LocationId = source.LocationId,
            Travel = source.Travel is null
                ? null
                : new TravelSnapshotV2
                {
                    Origin = source.Travel.Origin,
                    Destination = source.Travel.Destination,
                    DepartureMilliseconds = source.Travel.DepartureMilliseconds,
                    ExpectedArrivalMilliseconds = source.Travel.ExpectedArrivalMilliseconds,
                    ScheduledArrivalId = source.Travel.ScheduledArrivalId,
                },
        };

    private static void ValidateEnvelopeV1(SaveEnvelopeV1 envelope)
    {
        if (envelope.SchemaVersion != V1SchemaVersion)
        {
            throw new InvalidOperationException("The V1 mapper received a different schema version.");
        }

        if (!string.Equals(envelope.SimulationRulesVersion, V1SimulationRulesVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Simulation rules version '{envelope.SimulationRulesVersion}' is unsupported."
            );
        }

        if (envelope.Metadata is null || envelope.Simulation is null)
        {
            throw new InvalidOperationException("Required V1 envelope members cannot be null.");
        }
    }

    private static LoadedGameSave RestoreV6(SaveEnvelopeV6 envelope, ShipDefinitionCatalog catalog)
    {
        ValidateCandidateV6(envelope, catalog);
        SimulationSnapshotV6 snapshot = envelope.Simulation;
        var time = new SimulationTime(snapshot.TimeMilliseconds);
        StrategicMap map = RestoreMapV2(snapshot.StrategicMap);
        ShipState[] ships = [.. snapshot.Ships.Select(RestoreShipV6)];
        SimulationScheduler scheduler = RestoreSchedulerV2(snapshot.Scheduler, V6SchemaVersion);
        var state = new SimulationState(
            time,
            scheduler,
            ShipInstanceIdAllocator.Restore(snapshot.ShipAllocatorNextId),
            map,
            new ShipInstanceId(snapshot.PlayerShipId),
            ships,
            ShipOrderIdAllocator.Restore(snapshot.OrderAllocatorNextId)
        );
        var metadata = new GameSaveMetadata(
            envelope.Metadata.SaveId,
            envelope.Metadata.DisplayName,
            envelope.Metadata.CreatedAtUtc,
            envelope.Metadata.SavedAtUtc
        );
        return new LoadedGameSave(metadata, GameSimulation.RestoreState(state, catalog));
    }

    private static LoadedGameSave RestoreV7(
        SaveEnvelopeV7 envelope,
        ShipDefinitionCatalog catalog,
        FactionDefinitionCatalog factionCatalog
    )
    {
        ValidateCandidateV7(envelope, catalog);
        SimulationSnapshotV7 snapshot = envelope.Simulation;
        var state = new SimulationState(
            new SimulationTime(snapshot.TimeMilliseconds),
            RestoreSchedulerV7(snapshot.Scheduler),
            ShipInstanceIdAllocator.Restore(snapshot.ShipAllocatorNextId),
            RestoreMapV2(snapshot.StrategicMap),
            new ShipInstanceId(snapshot.PlayerShipId),
            snapshot.Ships.Select(RestoreShipV7),
            ShipOrderIdAllocator.Restore(snapshot.OrderAllocatorNextId),
            snapshot.Factions.Select(RestoreFactionV7)
        );
        var metadata = new GameSaveMetadata(
            envelope.Metadata.SaveId,
            envelope.Metadata.DisplayName,
            envelope.Metadata.CreatedAtUtc,
            envelope.Metadata.SavedAtUtc
        );
        return new LoadedGameSave(metadata, GameSimulation.RestoreState(state, catalog, factionCatalog));
    }

    private static void ValidateCandidateV2(SaveEnvelopeV2 envelope, ShipDefinitionCatalog catalog)
    {
        if (envelope.SchemaVersion != V2SchemaVersion)
        {
            throw new InvalidOperationException("The V2 mapper received a different schema version.");
        }

        if (!string.Equals(envelope.SimulationRulesVersion, V2SimulationRulesVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Simulation rules version '{envelope.SimulationRulesVersion}' is unsupported."
            );
        }

        if (envelope.Metadata is null || envelope.Simulation is null)
        {
            throw new InvalidOperationException("Required V2 envelope members cannot be null.");
        }

        ValidateMetadata(
            new GameSaveMetadata(
                envelope.Metadata.SaveId,
                envelope.Metadata.DisplayName,
                envelope.Metadata.CreatedAtUtc,
                envelope.Metadata.SavedAtUtc
            )
        );

        ValidateSimulationCandidateV2(envelope.Simulation, catalog, V2SchemaVersion);
    }

    private static void ValidateCandidateV3(SaveEnvelopeV3 envelope, ShipDefinitionCatalog catalog)
    {
        if (envelope.SchemaVersion != V3SchemaVersion)
        {
            throw new InvalidOperationException("The V3 mapper received a different schema version.");
        }

        if (!string.Equals(envelope.SimulationRulesVersion, V3SimulationRulesVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Simulation rules version '{envelope.SimulationRulesVersion}' is unsupported."
            );
        }

        if (envelope.Metadata is null || envelope.Simulation is null)
        {
            throw new InvalidOperationException("Required V3 envelope members cannot be null.");
        }

        ValidateMetadata(
            new GameSaveMetadata(
                envelope.Metadata.SaveId,
                envelope.Metadata.DisplayName,
                envelope.Metadata.CreatedAtUtc,
                envelope.Metadata.SavedAtUtc
            )
        );

        SimulationSnapshotV3 snapshot = envelope.Simulation;
        if (snapshot.Ships is null)
        {
            throw new InvalidOperationException("Required V3 simulation members cannot be null.");
        }

        ValidateSimulationCandidateV2(ToBaseSnapshotV2(snapshot), catalog, V3SchemaVersion);
        ValidateOrderCandidatesV3(snapshot);
    }

    private static void ValidateCandidateV4(SaveEnvelopeV4 envelope, ShipDefinitionCatalog catalog)
    {
        if (envelope.SchemaVersion != V4SchemaVersion)
        {
            throw new InvalidOperationException("The V4 mapper received a different schema version.");
        }

        if (!string.Equals(envelope.SimulationRulesVersion, V4SimulationRulesVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Simulation rules version '{envelope.SimulationRulesVersion}' is unsupported."
            );
        }

        if (envelope.Metadata is null || envelope.Simulation is null)
        {
            throw new InvalidOperationException("Required V4 envelope members cannot be null.");
        }

        ValidateMetadata(
            new GameSaveMetadata(
                envelope.Metadata.SaveId,
                envelope.Metadata.DisplayName,
                envelope.Metadata.CreatedAtUtc,
                envelope.Metadata.SavedAtUtc
            )
        );

        SimulationSnapshotV4 snapshot = envelope.Simulation;
        if (snapshot.Ships is null)
        {
            throw new InvalidOperationException("Required V4 simulation members cannot be null.");
        }

        SimulationSnapshotV3 baseSnapshot = ToBaseSnapshotV3(snapshot);
        ValidateSimulationCandidateV2(ToBaseSnapshotV2(baseSnapshot), catalog, V4SchemaVersion);
        ValidateOrderCandidatesV3(baseSnapshot);
        ValidateSensorCandidatesV4(snapshot);
    }

    private static void ValidateCandidateV5(SaveEnvelopeV5 envelope, ShipDefinitionCatalog catalog)
    {
        if (envelope.SchemaVersion != V5SchemaVersion)
        {
            throw new InvalidOperationException("The V5 mapper received a different schema version.");
        }

        if (!string.Equals(envelope.SimulationRulesVersion, V5SimulationRulesVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Simulation rules version '{envelope.SimulationRulesVersion}' is unsupported."
            );
        }

        if (envelope.Metadata is null || envelope.Simulation is null)
        {
            throw new InvalidOperationException("Required V5 envelope members cannot be null.");
        }

        ValidateMetadata(
            new GameSaveMetadata(
                envelope.Metadata.SaveId,
                envelope.Metadata.DisplayName,
                envelope.Metadata.CreatedAtUtc,
                envelope.Metadata.SavedAtUtc
            )
        );

        ValidateSimulationCandidateV5(envelope.Simulation, catalog, V5SchemaVersion);
    }

    private static void ValidateCandidateV6(SaveEnvelopeV6 envelope, ShipDefinitionCatalog catalog)
    {
        if (envelope.SchemaVersion != V6SchemaVersion)
        {
            throw new InvalidOperationException("The V6 mapper received a different schema version.");
        }

        if (!string.Equals(envelope.SimulationRulesVersion, V6SimulationRulesVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Simulation rules version '{envelope.SimulationRulesVersion}' is unsupported."
            );
        }

        if (envelope.Metadata is null || envelope.Simulation is null)
        {
            throw new InvalidOperationException("Required V6 envelope members cannot be null.");
        }

        ValidateMetadata(
            new GameSaveMetadata(
                envelope.Metadata.SaveId,
                envelope.Metadata.DisplayName,
                envelope.Metadata.CreatedAtUtc,
                envelope.Metadata.SavedAtUtc
            )
        );

        SimulationSnapshotV6 snapshot = envelope.Simulation;
        if (snapshot.Ships is null || snapshot.Scheduler is null || snapshot.StrategicMap is null)
        {
            throw new InvalidOperationException("Required V6 simulation members cannot be null.");
        }

        // V6 changes exactly one leaf member, so the whole V5 candidate contract is reused against a
        // downgraded view rather than restated. Ordering is load-bearing: the shared pass establishes
        // that ships, contacts, and the strategic map are structurally present and well formed, which
        // the observed-location pass below then relies on instead of repeating.
        ValidateSimulationCandidateV5(ToBaseSnapshotV5(snapshot), catalog, V6SchemaVersion);
        ValidateObservedLocationCandidatesV6(snapshot);
    }

    private static void ValidateCandidateV7(SaveEnvelopeV7 envelope, ShipDefinitionCatalog catalog)
    {
        if (envelope.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidOperationException("The V7 mapper received a different schema version.");
        }

        if (!string.Equals(envelope.SimulationRulesVersion, CurrentSimulationRulesVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Simulation rules version '{envelope.SimulationRulesVersion}' is unsupported."
            );
        }

        if (envelope.Metadata is null || envelope.Simulation is null)
        {
            throw new InvalidOperationException("Required V7 envelope members cannot be null.");
        }

        ValidateMetadata(
            new GameSaveMetadata(
                envelope.Metadata.SaveId,
                envelope.Metadata.DisplayName,
                envelope.Metadata.CreatedAtUtc,
                envelope.Metadata.SavedAtUtc
            )
        );
        SimulationSnapshotV7 snapshot = envelope.Simulation;
        if (
            snapshot.Ships is null
            || snapshot.Factions is null
            || snapshot.Scheduler is null
            || snapshot.StrategicMap is null
        )
        {
            throw new InvalidOperationException("Required V7 simulation members cannot be null.");
        }

        ValidateSimulationCandidateV5(ToBaseSnapshotV5(snapshot), catalog, V6SchemaVersion);
        ValidateObservedLocationCandidatesV6(ToObservedSnapshotV6(snapshot));
        ValidateFactionShapeV7(snapshot);
        ValidateSchedulerCandidateV7(snapshot);
    }

    private static void ValidateSimulationCandidateV5(
        SimulationSnapshotV5 snapshot,
        ShipDefinitionCatalog catalog,
        int sourceSchemaVersion
    )
    {
        if (snapshot.Ships is null || snapshot.Scheduler is null || snapshot.StrategicMap is null)
        {
            throw new InvalidOperationException("Required V5 simulation members cannot be null.");
        }

        SimulationSnapshotV3 baseSnapshot = ToBaseSnapshotV3(snapshot);
        ValidateSimulationCandidateV2(ToBaseSnapshotV2(baseSnapshot), catalog, V4SchemaVersion);
        ValidateOrderCandidatesV3(baseSnapshot);
        ValidateSensorCandidatesV4(ToSensorSnapshotV4(snapshot));

        HashSet<long> shipIds = [.. snapshot.Ships.Select(ship => ship.InstanceId)];
        ValidateSchedulerCandidateV2(snapshot.Scheduler, snapshot.TimeMilliseconds, shipIds, sourceSchemaVersion);
        foreach (ShipSnapshotV5 ship in snapshot.Ships)
        {
            ValidateEngineeringCandidateV5(
                ship,
                catalog.GetRequired(new ShipDefinitionId(ship.DefinitionId)),
                snapshot.Scheduler,
                snapshot.TimeMilliseconds
            );
        }
    }

    /// <remarks>
    /// Runs after the shared V5 candidate pass, which has already rejected null ships, null sensor
    /// knowledge, and a malformed strategic map, so this pass only has to decide the new member. A
    /// present frame is bounded and resolved here rather than left to the aggregate, so an unknown
    /// location identity fails as untrusted input before any live state is replaced.
    /// </remarks>
    private static void ValidateObservedLocationCandidatesV6(SimulationSnapshotV6 snapshot)
    {
        HashSet<string> locationIds = [.. snapshot.StrategicMap.Locations.Select(location => location.Id)];
        foreach (ShipSnapshotV6 ship in snapshot.Ships)
        {
            foreach (SaveModelsV6.SensorContactSnapshotV6 contact in ship.SensorKnowledge.Contacts)
            {
                if (contact.ObservedAtLocationId is null)
                {
                    continue;
                }

                ValidateText(
                    contact.ObservedAtLocationId,
                    "Observed contact location identity",
                    LocationId.MaximumLength
                );
                if (!locationIds.Contains(contact.ObservedAtLocationId))
                {
                    throw new InvalidOperationException(
                        "Every observed contact location must exist in the strategic map."
                    );
                }
            }
        }
    }

    /// <remarks>
    /// Drops the V6 observed-location frame to reuse the V5 candidate contract. Every collection is
    /// copied defensively because this runs before validation: a null ship or null contact array in an
    /// untrusted document must reach the V5 checks that report it, not raise a null dereference here
    /// that escapes the load path's typed failure translation.
    /// </remarks>
    private static SimulationSnapshotV5 ToBaseSnapshotV5(SimulationSnapshotV6 snapshot) =>
        new()
        {
            TimeMilliseconds = snapshot.TimeMilliseconds,
            ShipAllocatorNextId = snapshot.ShipAllocatorNextId,
            OrderAllocatorNextId = snapshot.OrderAllocatorNextId,
            PlayerShipId = snapshot.PlayerShipId,
            Scheduler = snapshot.Scheduler,
            StrategicMap = snapshot.StrategicMap,
            Ships = [.. snapshot.Ships.Select(ship => ship is null ? null! : ToBaseShipV5(ship))],
        };

    private static ShipSnapshotV5 ToBaseShipV5(ShipSnapshotV6 ship) =>
        new()
        {
            InstanceId = ship.InstanceId,
            DefinitionId = ship.DefinitionId,
            DisplayName = ship.DisplayName,
            TacticalPosition = ship.TacticalPosition,
            TacticalMotion = ship.TacticalMotion,
            Engineering = ship.Engineering,
            StrategicState = ship.StrategicState,
            ActiveOrder = ship.ActiveOrder,
            SensorKnowledge = ship.SensorKnowledge is null
                ? null!
                : new SaveModelsV4.SensorKnowledgeSnapshotV4
                {
                    NextContactId = ship.SensorKnowledge.NextContactId,
                    Contacts = ship.SensorKnowledge.Contacts is null
                        ? null!
                        :
                        [
                            .. ship.SensorKnowledge.Contacts.Select(contact =>
                                contact is null
                                    ? null!
                                    : new SaveModelsV4.SensorContactSnapshotV4
                                    {
                                        Id = contact.Id,
                                        TargetShipId = contact.TargetShipId,
                                        LastObservedPosition = contact.LastObservedPosition,
                                        LastObservedAtMilliseconds = contact.LastObservedAtMilliseconds,
                                        Status = contact.Status,
                                        Identification = contact.Identification,
                                        KnownVesselDisplayName = contact.KnownVesselDisplayName,
                                        KnownDesignDisplayName = contact.KnownDesignDisplayName,
                                        LossWorkId = contact.LossWorkId,
                                        LossDueTimeMilliseconds = contact.LossDueTimeMilliseconds,
                                    }
                            ),
                        ],
                    ActiveScan = ship.SensorKnowledge.ActiveScan,
                },
            AutonomousState = ship.AutonomousState,
        };

    private static SimulationSnapshotV6 ToObservedSnapshotV6(SimulationSnapshotV7 snapshot) =>
        new()
        {
            TimeMilliseconds = snapshot.TimeMilliseconds,
            ShipAllocatorNextId = snapshot.ShipAllocatorNextId,
            OrderAllocatorNextId = snapshot.OrderAllocatorNextId,
            PlayerShipId = snapshot.PlayerShipId,
            Scheduler = ToShipSchedulerV2(snapshot.Scheduler),
            StrategicMap = snapshot.StrategicMap,
            Ships = [.. snapshot.Ships.Select(ship => ship is null ? null! : ToShipV6(ship))],
        };

    private static SimulationSnapshotV5 ToBaseSnapshotV5(SimulationSnapshotV7 snapshot) =>
        ToBaseSnapshotV5(ToObservedSnapshotV6(snapshot));

    private static ShipSnapshotV6 ToShipV6(ShipSnapshotV7 ship) =>
        new()
        {
            InstanceId = ship.InstanceId,
            DefinitionId = ship.DefinitionId,
            DisplayName = ship.DisplayName,
            TacticalPosition = ship.TacticalPosition,
            TacticalMotion = ship.TacticalMotion,
            Engineering = ship.Engineering,
            StrategicState = ship.StrategicState,
            ActiveOrder = ship.ActiveOrder,
            SensorKnowledge = ship.SensorKnowledge,
            AutonomousState = ship.AutonomousState,
        };

    private static SchedulerSnapshotV2 ToShipSchedulerV2(SaveModelsV7.SchedulerSnapshotV7 scheduler) =>
        new()
        {
            NextWorkId = scheduler.NextWorkId,
            NextSequence = scheduler.NextSequence,
            OutstandingWork = scheduler.OutstandingWork is null
                ? null!
                :
                [
                    .. scheduler
                        .OutstandingWork.Where(work =>
                            work is not null && string.Equals(work.TargetKind, ShipTargetKind, StringComparison.Ordinal)
                        )
                        .Select(work => new ScheduledWorkSnapshotV2
                        {
                            Id = work.Id,
                            DueTimeMilliseconds = work.DueTimeMilliseconds,
                            Sequence = work.Sequence,
                            Kind = work.Kind,
                            TargetShipId = work.TargetShipId ?? 0,
                        }),
                ],
        };

    private static SimulationSnapshotV3 ToBaseSnapshotV3(SimulationSnapshotV5 snapshot) =>
        new()
        {
            TimeMilliseconds = snapshot.TimeMilliseconds,
            ShipAllocatorNextId = snapshot.ShipAllocatorNextId,
            OrderAllocatorNextId = snapshot.OrderAllocatorNextId,
            PlayerShipId = snapshot.PlayerShipId,
            Scheduler = new SchedulerSnapshotV2
            {
                NextWorkId = snapshot.Scheduler.NextWorkId,
                NextSequence = snapshot.Scheduler.NextSequence,
                OutstandingWork =
                [
                    .. snapshot.Scheduler.OutstandingWork.Where(work =>
                        !string.Equals(work.Kind, SystemRepairCompletionKind, StringComparison.Ordinal)
                    ),
                ],
            },
            StrategicMap = snapshot.StrategicMap,
            Ships =
            [
                .. snapshot.Ships.Select(ship =>
                    ship is null
                        ? null!
                        : new ShipSnapshotV3
                        {
                            InstanceId = ship.InstanceId,
                            DefinitionId = ship.DefinitionId,
                            DisplayName = ship.DisplayName,
                            TacticalPosition = ship.TacticalPosition,
                            TacticalMotion = ship.TacticalMotion,
                            SensorIntegrity = ship.Engineering?.SensorCondition ?? double.NaN,
                            SensorRepair = null,
                            StrategicState = ship.StrategicState,
                            ActiveOrder = ship.ActiveOrder,
                        }
                ),
            ],
        };

    private static SimulationSnapshotV4 ToSensorSnapshotV4(SimulationSnapshotV5 snapshot) =>
        new()
        {
            TimeMilliseconds = snapshot.TimeMilliseconds,
            ShipAllocatorNextId = snapshot.ShipAllocatorNextId,
            OrderAllocatorNextId = snapshot.OrderAllocatorNextId,
            PlayerShipId = snapshot.PlayerShipId,
            Scheduler = snapshot.Scheduler,
            StrategicMap = snapshot.StrategicMap,
            Ships =
            [
                .. snapshot.Ships.Select(ship =>
                    ship is null
                        ? null!
                        : new ShipSnapshotV4
                        {
                            InstanceId = ship.InstanceId,
                            DefinitionId = ship.DefinitionId,
                            DisplayName = ship.DisplayName,
                            TacticalPosition = ship.TacticalPosition,
                            TacticalMotion = ship.TacticalMotion,
                            SensorIntegrity = ship.Engineering?.SensorCondition ?? double.NaN,
                            SensorRepair = null,
                            StrategicState = ship.StrategicState,
                            ActiveOrder = ship.ActiveOrder,
                            SensorKnowledge = ship.SensorKnowledge,
                            AutonomousState = ship.AutonomousState,
                        }
                ),
            ],
        };

    private static SimulationSnapshotV3 ToBaseSnapshotV3(SimulationSnapshotV4 snapshot) =>
        new()
        {
            TimeMilliseconds = snapshot.TimeMilliseconds,
            ShipAllocatorNextId = snapshot.ShipAllocatorNextId,
            OrderAllocatorNextId = snapshot.OrderAllocatorNextId,
            PlayerShipId = snapshot.PlayerShipId,
            Scheduler = snapshot.Scheduler,
            StrategicMap = snapshot.StrategicMap,
            Ships =
            [
                .. snapshot.Ships.Select(ship =>
                    ship is null
                        ? null!
                        : new ShipSnapshotV3
                        {
                            InstanceId = ship.InstanceId,
                            DefinitionId = ship.DefinitionId,
                            DisplayName = ship.DisplayName,
                            TacticalPosition = ship.TacticalPosition,
                            TacticalMotion = ship.TacticalMotion,
                            SensorIntegrity = ship.SensorIntegrity,
                            SensorRepair = ship.SensorRepair,
                            StrategicState = ship.StrategicState,
                            ActiveOrder = ship.ActiveOrder,
                        }
                ),
            ],
        };

    private static void ValidateSensorCandidatesV4(SimulationSnapshotV4 snapshot)
    {
        foreach (ShipSnapshotV4? ship in snapshot.Ships)
        {
            if (ship?.SensorKnowledge is null || ship.AutonomousState is null)
            {
                throw new InvalidOperationException("V4 ship sensor knowledge and autonomous state are required.");
            }

            if (ship.SensorKnowledge.Contacts is null)
            {
                throw new InvalidOperationException("V4 sensor contact collection is required.");
            }

            EnsureCount(
                ship.SensorKnowledge.Contacts.Length,
                SensorKnowledge.MaximumContactsPerObserver,
                "sensor contacts per observer"
            );
            foreach (SaveModelsV4.SensorContactSnapshotV4? contact in ship.SensorKnowledge.Contacts)
            {
                if (contact?.LastObservedPosition is null)
                {
                    throw new InvalidOperationException("V4 sensor contact and observed position are required.");
                }
            }
        }
    }

    private static void ValidateEngineeringCandidateV5(
        ShipSnapshotV5 ship,
        ShipDefinition definition,
        SchedulerSnapshotV2 scheduler,
        long currentTime
    )
    {
        if (ship.Engineering is null)
        {
            throw new InvalidOperationException("V5 ship Engineering state is required.");
        }

        SaveModelsV5.EngineeringSnapshotV5 engineering = ship.Engineering;
        EnsureUnitInterval(engineering.GenerationCondition, "Generation condition");
        EnsureUnitInterval(engineering.SensorCondition, "Sensor condition");
        EnsureUnitInterval(engineering.ImpulseCondition, "Impulse condition");
        var state = new ShipEngineeringState(
            new SystemCondition(engineering.GenerationCondition),
            new SystemCondition(engineering.SensorCondition),
            new SystemCondition(engineering.ImpulseCondition),
            new PowerAllocation(
                new PowerUnits(engineering.SensorAllocation),
                new PowerUnits(engineering.ImpulseAllocation)
            )
        );
        state.Validate(definition.Engineering);

        double effectiveMaximumSpeed =
            definition.MaximumTacticalSpeed.Value * state.ImpulseCapability(definition.Engineering);
        if (ship.TacticalMotion.SpeedKilometersPerSecond > effectiveMaximumSpeed)
        {
            throw new InvalidOperationException("Ship tactical speed exceeds its effective impulse capability.");
        }

        ScheduledWorkSnapshotV2[] repairWork =
        [
            .. scheduler.OutstandingWork.Where(work =>
                work.TargetShipId == ship.InstanceId
                && string.Equals(work.Kind, SystemRepairCompletionKind, StringComparison.Ordinal)
            ),
        ];
        ValidateRepairCandidateV5(ship, definition, currentTime, engineering, repairWork);
    }

    private static void ValidateRepairCandidateV5(
        ShipSnapshotV5 ship,
        ShipDefinition definition,
        long currentTime,
        SaveModelsV5.EngineeringSnapshotV5 engineering,
        ScheduledWorkSnapshotV2[] repairWork
    )
    {
        if (engineering.ActiveRepair is null)
        {
            if (repairWork.Length != 0)
            {
                throw new InvalidOperationException("Scheduled system completion has no correlated active repair.");
            }

            return;
        }

        SaveModelsV5.SystemRepairSnapshotV5 repair = engineering.ActiveRepair;
        var targetSystem = ShipSystemId.Parse(repair.TargetSystem);
        if (targetSystem != ShipSystemId.Sensors && targetSystem != ShipSystemId.ImpulsePropulsion)
        {
            throw new InvalidOperationException("Only sensors and impulse propulsion are repairable.");
        }

        ValidateRepairTimingV5(repair, currentTime);

        if (
            repair.ExpectedCompletionMilliseconds - repair.StartedAtMilliseconds
            != definition.Engineering.RepairDurationFor(targetSystem).Milliseconds
        )
        {
            throw new InvalidOperationException("System repair duration does not match its ship definition.");
        }

        double progress =
            (double)(currentTime - repair.StartedAtMilliseconds)
            / (repair.ExpectedCompletionMilliseconds - repair.StartedAtMilliseconds);
        double expectedCondition =
            repair.StartingCondition + ((repair.TargetCondition - repair.StartingCondition) * progress);
        double actualCondition =
            targetSystem == ShipSystemId.Sensors ? engineering.SensorCondition : engineering.ImpulseCondition;
        if (actualCondition != expectedCondition)
        {
            throw new InvalidOperationException("System condition does not match the active repair at current time.");
        }

        if (
            repairWork.Length != 1
            || repairWork[0].Id != repair.ScheduledCompletionId
            || repairWork[0].DueTimeMilliseconds != repair.ExpectedCompletionMilliseconds
        )
        {
            throw new InvalidOperationException(
                "Active system repair must have exactly one same-target correlated scheduled work item."
            );
        }
    }

    private static void ValidateRepairTimingV5(SaveModelsV5.SystemRepairSnapshotV5 repair, long currentTime)
    {
        EnsureUnitInterval(repair.StartingCondition, "System repair starting condition");
        EnsureUnitInterval(repair.TargetCondition, "System repair target condition");
        if (repair.TargetCondition <= repair.StartingCondition)
        {
            throw new InvalidOperationException("System repair target must exceed its starting condition.");
        }

        EnsureFixedStep(repair.StartedAtMilliseconds, "System repair start");
        EnsureFixedStep(repair.ExpectedCompletionMilliseconds, "System repair completion");
        if (currentTime < repair.StartedAtMilliseconds || currentTime >= repair.ExpectedCompletionMilliseconds)
        {
            throw new InvalidOperationException("Active system repair does not contain the current simulation time.");
        }
    }

    private static SimulationSnapshotV2 ToBaseSnapshotV2(SimulationSnapshotV3 snapshot) =>
        new()
        {
            TimeMilliseconds = snapshot.TimeMilliseconds,
            ShipAllocatorNextId = snapshot.ShipAllocatorNextId,
            PlayerShipId = snapshot.PlayerShipId,
            Scheduler = snapshot.Scheduler,
            StrategicMap = snapshot.StrategicMap,
            Ships =
            [
                .. snapshot.Ships.Select(ship =>
                    ship is null
                        ? null!
                        : new ShipSnapshotV2
                        {
                            InstanceId = ship.InstanceId,
                            DefinitionId = ship.DefinitionId,
                            DisplayName = ship.DisplayName,
                            TacticalPosition = ship.TacticalPosition,
                            TacticalMotion = ship.TacticalMotion,
                            SensorIntegrity = ship.SensorIntegrity,
                            SensorRepair = ship.SensorRepair,
                            StrategicState = ship.StrategicState,
                        }
                ),
            ],
        };

    private static void ValidateOrderCandidatesV3(SimulationSnapshotV3 snapshot)
    {
        if (snapshot.OrderAllocatorNextId <= 0 || snapshot.OrderAllocatorNextId == long.MaxValue)
        {
            throw new InvalidOperationException("Order allocator must be positive and retain continuation headroom.");
        }

        var orderIds = new HashSet<long>();
        long greatestOrderId = 0;
        foreach (ShipSnapshotV3 ship in snapshot.Ships)
        {
            if (ship.ActiveOrder is null)
            {
                continue;
            }

            if (ship.InstanceId == snapshot.PlayerShipId)
            {
                throw new InvalidOperationException("The player ship cannot have an autonomous order.");
            }

            if (ship.ActiveOrder.Id <= 0 || !orderIds.Add(ship.ActiveOrder.Id))
            {
                throw new InvalidOperationException("Active ship order identities must be positive and unique.");
            }

            greatestOrderId = Math.Max(greatestOrderId, ship.ActiveOrder.Id);
            ValidateOrderCandidateV3(ship.ActiveOrder);
        }

        if (snapshot.OrderAllocatorNextId <= greatestOrderId)
        {
            throw new InvalidOperationException("Order allocator must follow every active order identity.");
        }
    }

    private static void ValidateOrderCandidateV3(ShipOrderSnapshotV3 order)
    {
        switch (order)
        {
            case TravelToOrderSnapshotV3 travel:
                ValidateText(travel.Destination, "TravelTo destination", LocationId.MaximumLength);
                break;
            case PatrolRouteOrderSnapshotV3 patrol:
                if (patrol.Waypoints is null)
                {
                    throw new InvalidOperationException("Patrol waypoints are required.");
                }

                EnsureCount(patrol.Waypoints.Length, PatrolRouteOrder.MaximumWaypointCount, "patrol waypoints");
                foreach (string waypoint in patrol.Waypoints)
                {
                    ValidateText(waypoint, "Patrol waypoint", LocationId.MaximumLength);
                }

                break;
            case HoldUntilOrderSnapshotV3 hold:
                if (hold.ScheduledWakeId <= 0)
                {
                    throw new InvalidOperationException("HoldUntil scheduled wake identity must be positive.");
                }

                break;
            default:
                throw new InvalidOperationException("Active ship order kind is unknown.");
        }
    }

    private static void ValidateSimulationCandidateV2(
        SimulationSnapshotV2 snapshot,
        ShipDefinitionCatalog catalog,
        int sourceSchemaVersion
    )
    {
        if (snapshot.Scheduler is null || snapshot.StrategicMap is null || snapshot.Ships is null)
        {
            throw new InvalidOperationException("Required V2 simulation members cannot be null.");
        }

        if (snapshot.TimeMilliseconds < 0)
        {
            throw new InvalidOperationException("Current simulation time cannot be negative.");
        }

        if (snapshot.TimeMilliseconds > long.MaxValue - SimulationFixedStep.Duration.Milliseconds)
        {
            throw new InvalidOperationException(
                "Current simulation time lacks one fixed-step of continuation headroom."
            );
        }

        EnsureFixedStep(snapshot.TimeMilliseconds, "Current simulation time");
        ValidateMapCandidateV2(snapshot.StrategicMap);

        (HashSet<long> shipIds, Dictionary<long, ShipDefinition> definitions) = ValidateShipIdentitiesV2(
            snapshot.Ships,
            catalog
        );

        if (snapshot.PlayerShipId <= 0 || !shipIds.Contains(snapshot.PlayerShipId))
        {
            throw new InvalidOperationException("Player ship identity must resolve exactly once.");
        }

        long maximumShipId = shipIds.Max();
        if (snapshot.ShipAllocatorNextId <= maximumShipId || snapshot.ShipAllocatorNextId == long.MaxValue)
        {
            throw new InvalidOperationException(
                "Ship allocator must follow every ship identity and retain continuation headroom."
            );
        }

        ValidateSchedulerCandidateV2(snapshot.Scheduler, snapshot.TimeMilliseconds, shipIds, sourceSchemaVersion);
        foreach (ShipSnapshotV2 ship in snapshot.Ships)
        {
            ValidateShipCandidateV2(
                ship,
                definitions[ship.InstanceId],
                snapshot.StrategicMap,
                snapshot.Scheduler,
                snapshot.TimeMilliseconds
            );
        }
    }

    private static (HashSet<long> Ids, Dictionary<long, ShipDefinition> Definitions) ValidateShipIdentitiesV2(
        ShipSnapshotV2[] ships,
        ShipDefinitionCatalog catalog
    )
    {
        if (ships.Length == 0)
        {
            throw new InvalidOperationException("The save must contain at least one ship.");
        }

        EnsureCount(ships.Length, SimulationState.MaximumShips, "ships");
        var shipIds = new HashSet<long>();
        var definitions = new Dictionary<long, ShipDefinition>();
        foreach (ShipSnapshotV2? ship in ships)
        {
            if (ship is null)
            {
                throw new InvalidOperationException("Ship snapshots cannot be null.");
            }

            if (ship.InstanceId <= 0 || !shipIds.Add(ship.InstanceId))
            {
                throw new InvalidOperationException("Ship identities must be positive and unique.");
            }

            ValidateText(ship.DefinitionId, "Ship definition identity", ShipDefinitionId.MaximumLength);
            ValidateText(ship.DisplayName, "Ship display name", ShipState.MaximumVesselDisplayNameLength);
            definitions.Add(ship.InstanceId, catalog.GetRequired(new ShipDefinitionId(ship.DefinitionId)));
        }

        return (shipIds, definitions);
    }

    private static void ValidateMapCandidateV2(StrategicMapSnapshotV2 map)
    {
        if (map.Locations is null || map.Routes is null)
        {
            throw new InvalidOperationException("Strategic map collections are required.");
        }

        if (map.Locations.Length == 0)
        {
            throw new InvalidOperationException("A strategic map requires at least one location.");
        }

        EnsureCount(map.Locations.Length, StrategicMap.MaximumLocations, "strategic locations");
        EnsureCount(map.Routes.Length, StrategicMap.MaximumRoutes, "strategic routes");
        var locationIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (StrategicLocationSnapshotV2? location in map.Locations)
        {
            if (location is null || location.Position is null)
            {
                throw new InvalidOperationException("Strategic location data cannot be null.");
            }

            ValidateText(location.Id, "Location identity", LocationId.MaximumLength);
            ValidateText(location.DisplayName, "Location display name", StrategicLocation.MaximumDisplayNameLength);
            if (!locationIds.Add(location.Id))
            {
                throw new InvalidOperationException("Strategic location identities must be unique.");
            }

            EnsureFinite(location.Position.XUnitless, "Strategic location X position");
            EnsureFinite(location.Position.YUnitless, "Strategic location Y position");
        }

        ValidateRouteCandidatesV2(map.Routes, locationIds);
    }

    private static void ValidateRouteCandidatesV2(StrategicRouteSnapshotV2[] routes, HashSet<string> locationIds)
    {
        var connections = new HashSet<string>(StringComparer.Ordinal);
        foreach (StrategicRouteSnapshotV2? route in routes)
        {
            if (route is null)
            {
                throw new InvalidOperationException("Strategic route data cannot be null.");
            }

            ValidateText(route.Origin, "Route origin", LocationId.MaximumLength);
            ValidateText(route.Destination, "Route destination", LocationId.MaximumLength);
            if (!locationIds.Contains(route.Origin) || !locationIds.Contains(route.Destination))
            {
                throw new InvalidOperationException("Every route endpoint must exist in the strategic map.");
            }

            if (route.DurationMilliseconds <= 0)
            {
                throw new InvalidOperationException("Strategic route duration must be positive.");
            }

            EnsureFixedStep(route.DurationMilliseconds, "Strategic route duration");
            bool originFirst = string.CompareOrdinal(route.Origin, route.Destination) <= 0;
            string first = originFirst ? route.Origin : route.Destination;
            string second = originFirst ? route.Destination : route.Origin;
            if (!connections.Add($"{first}\0{second}"))
            {
                throw new InvalidOperationException("A strategic connection may be declared only once.");
            }
        }
    }

    private static void ValidateSchedulerCandidateV2(
        SchedulerSnapshotV2 scheduler,
        long currentTime,
        HashSet<long> shipIds,
        int sourceSchemaVersion
    )
    {
        if (scheduler.OutstandingWork is null)
        {
            throw new InvalidOperationException("Outstanding scheduler work is required.");
        }

        EnsureCount(scheduler.OutstandingWork.Length, SimulationScheduler.MaximumOutstandingWork, "scheduler work");
        if (!SimulationScheduler.AreCountersWithinPersistedRange(scheduler.NextWorkId, scheduler.NextSequence))
        {
            throw new InvalidOperationException("Scheduler counters are outside the persisted range.");
        }

        var identities = new HashSet<long>();
        var sequences = new HashSet<long>();
        long previousDue = -1;
        long previousSequence = -1;
        for (int index = 0; index < scheduler.OutstandingWork.Length; index++)
        {
            ScheduledWorkSnapshotV2 work = ValidateScheduledWorkCandidateV2(
                scheduler.OutstandingWork[index],
                scheduler,
                currentTime,
                shipIds,
                identities,
                sequences,
                sourceSchemaVersion
            );
            if (
                index > 0
                && (
                    work.DueTimeMilliseconds < previousDue
                    || (work.DueTimeMilliseconds == previousDue && work.Sequence <= previousSequence)
                )
            )
            {
                throw new InvalidOperationException(
                    "Outstanding scheduler work is not in stable due-time and sequence order."
                );
            }

            previousDue = work.DueTimeMilliseconds;
            previousSequence = work.Sequence;
        }
    }

    private static ScheduledWorkSnapshotV2 ValidateScheduledWorkCandidateV2(
        ScheduledWorkSnapshotV2? work,
        SchedulerSnapshotV2 scheduler,
        long currentTime,
        HashSet<long> shipIds,
        HashSet<long> identities,
        HashSet<long> sequences,
        int sourceSchemaVersion
    )
    {
        if (work is null)
        {
            throw new InvalidOperationException("Scheduler work items cannot be null.");
        }

        if (work.Id <= 0 || work.Id >= scheduler.NextWorkId || !identities.Add(work.Id))
        {
            throw new InvalidOperationException(
                "Scheduler work identities must be positive, unique, and below the next counter."
            );
        }

        if (work.Sequence < 0 || work.Sequence >= scheduler.NextSequence || !sequences.Add(work.Sequence))
        {
            throw new InvalidOperationException(
                "Scheduler work sequences must be nonnegative, unique, and below the next counter."
            );
        }

        EnsureFixedStep(work.DueTimeMilliseconds, "Scheduled due time");
        if (work.DueTimeMilliseconds < currentTime)
        {
            throw new InvalidOperationException("Scheduled work cannot be overdue.");
        }

        if (!shipIds.Contains(work.TargetShipId))
        {
            throw new InvalidOperationException("Scheduled work target ship does not exist.");
        }

        ParseWorkKind(work.Kind, sourceSchemaVersion);
        return work;
    }

    private static void ValidateSchedulerCandidateV7(SimulationSnapshotV7 snapshot)
    {
        SaveModelsV7.SchedulerSnapshotV7 scheduler = snapshot.Scheduler;
        if (scheduler.OutstandingWork is null)
        {
            throw new InvalidOperationException("Outstanding scheduler work is required.");
        }

        EnsureCount(scheduler.OutstandingWork.Length, SimulationScheduler.MaximumOutstandingWork, "scheduler work");
        if (!SimulationScheduler.AreCountersWithinPersistedRange(scheduler.NextWorkId, scheduler.NextSequence))
        {
            throw new InvalidOperationException("Scheduler counters are outside the persisted range.");
        }

        HashSet<long> shipIds = [.. snapshot.Ships.Select(ship => ship.InstanceId)];
        HashSet<long> factionIds = [.. snapshot.Factions.Select(faction => faction.Id)];
        var identities = new HashSet<long>();
        var sequences = new HashSet<long>();
        long previousDue = -1;
        long previousSequence = -1;
        foreach (SaveModelsV7.ScheduledWorkSnapshotV7? work in scheduler.OutstandingWork)
        {
            ValidateScheduledWorkCandidateV7(
                work,
                scheduler,
                snapshot.TimeMilliseconds,
                shipIds,
                factionIds,
                identities,
                sequences
            );
            if (
                work!.DueTimeMilliseconds < previousDue
                || (work.DueTimeMilliseconds == previousDue && work.Sequence <= previousSequence)
            )
            {
                throw new InvalidOperationException(
                    "Outstanding scheduler work is not in stable due-time and sequence order."
                );
            }

            previousDue = work.DueTimeMilliseconds;
            previousSequence = work.Sequence;
        }
    }

    private static void ValidateScheduledWorkCandidateV7(
        SaveModelsV7.ScheduledWorkSnapshotV7? work,
        SaveModelsV7.SchedulerSnapshotV7 scheduler,
        long currentTime,
        HashSet<long> shipIds,
        HashSet<long> factionIds,
        HashSet<long> identities,
        HashSet<long> sequences
    )
    {
        if (work is null)
        {
            throw new InvalidOperationException("Scheduler work items cannot be null.");
        }

        if (work.Id <= 0 || work.Id >= scheduler.NextWorkId || !identities.Add(work.Id))
        {
            throw new InvalidOperationException(
                "Scheduler work identities must be positive, unique, and below the next counter."
            );
        }

        if (work.Sequence < 0 || work.Sequence >= scheduler.NextSequence || !sequences.Add(work.Sequence))
        {
            throw new InvalidOperationException(
                "Scheduler work sequences must be nonnegative, unique, and below the next counter."
            );
        }

        EnsureFixedStep(work.DueTimeMilliseconds, "Scheduled due time");
        if (work.DueTimeMilliseconds < currentTime)
        {
            throw new InvalidOperationException("Scheduled work cannot be overdue.");
        }

        ScheduledWorkTarget target = ParseWorkTargetV7(work);
        if (
            (target.ShipId is { } shipId && !shipIds.Contains(shipId.Value))
            || (target.FactionId is { } factionId && !factionIds.Contains(factionId.Value))
        )
        {
            throw new InvalidOperationException("Scheduled work target does not exist in its declared domain.");
        }

        _ = new ScheduledWork(
            new ScheduledWorkId(work.Id),
            new SimulationTime(work.DueTimeMilliseconds),
            work.Sequence,
            target,
            ParseWorkKind(work.Kind, CurrentSchemaVersion)
        );
    }

    private static void ValidateFactionShapeV7(SimulationSnapshotV7 snapshot)
    {
        EnsureCount(snapshot.Factions.Length, SimulationState.MaximumFactions, "factions");
        var identities = new HashSet<long>();
        foreach (SaveModelsV7.FactionSnapshotV7? faction in snapshot.Factions)
        {
            if (faction is null || faction.Id <= 0 || !identities.Add(faction.Id))
            {
                throw new InvalidOperationException("Factions require positive unique identities.");
            }

            ValidateText(faction.DefinitionId, "Faction definition identity", FactionDefinitionId.MaximumLength);
            if (faction.PresenceObjective is { } objective)
            {
                ValidateText(objective.TargetLocationId, "Faction objective location", LocationId.MaximumLength);
                _ = ParseObjectiveStatus(objective.Status);
            }

            if (faction.PendingDecisionWake is { } wake)
            {
                EnsureFixedStep(wake.DueTimeMilliseconds, "Faction decision wake due time");
            }
        }
    }

    private static void ValidateShipCandidateV2(
        ShipSnapshotV2 ship,
        ShipDefinition definition,
        StrategicMapSnapshotV2 map,
        SchedulerSnapshotV2 scheduler,
        long currentTime
    )
    {
        if (ship.TacticalPosition is null || ship.TacticalMotion is null || ship.StrategicState is null)
        {
            throw new InvalidOperationException("Ship tactical and strategic state is required.");
        }

        EnsureFinite(ship.TacticalPosition.XKilometers, "Ship tactical X position");
        EnsureFinite(ship.TacticalPosition.YKilometers, "Ship tactical Y position");
        EnsureFinite(ship.TacticalMotion.HeadingDegrees, "Ship tactical heading");
        EnsureFinite(ship.TacticalMotion.SpeedKilometersPerSecond, "Ship tactical speed");
        if (
            ship.TacticalMotion.SpeedKilometersPerSecond < 0
            || ship.TacticalMotion.SpeedKilometersPerSecond > definition.MaximumTacticalSpeed.Value
        )
        {
            throw new InvalidOperationException("Ship tactical speed is outside its definition bounds.");
        }

        EnsureUnitInterval(ship.SensorIntegrity, "Ship sensor integrity");
        ValidateStrategicCandidateV2(ship, map, scheduler, currentTime);
        ValidateRepairCandidateV2(ship, definition, scheduler, currentTime);
    }

    private static void ValidateStrategicCandidateV2(
        ShipSnapshotV2 ship,
        StrategicMapSnapshotV2 map,
        SchedulerSnapshotV2 scheduler,
        long currentTime
    )
    {
        StrategicStateSnapshotV2 strategic = ship.StrategicState;
        if (string.Equals(strategic.Kind, AtLocationKind, StringComparison.Ordinal))
        {
            ValidateAtLocationCandidateV2(ship, map, scheduler);
            return;
        }

        if (!string.Equals(strategic.Kind, TravelingKind, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Ship strategic state kind is unknown.");
        }

        ValidateTravelingCandidateV2(ship, map, scheduler, currentTime);
    }

    private static void ValidateAtLocationCandidateV2(
        ShipSnapshotV2 ship,
        StrategicMapSnapshotV2 map,
        SchedulerSnapshotV2 scheduler
    )
    {
        StrategicStateSnapshotV2 strategic = ship.StrategicState;
        if (strategic.Travel is not null)
        {
            throw new InvalidOperationException("At-location state cannot contain active travel.");
        }

        ValidateText(strategic.LocationId, "Current location identity", LocationId.MaximumLength);
        if (!map.Locations.Any(location => string.Equals(location.Id, strategic.LocationId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Current location does not exist in the strategic map.");
        }

        if (
            scheduler.OutstandingWork.Any(work =>
                work.TargetShipId == ship.InstanceId
                && string.Equals(work.Kind, TravelArrivalKind, StringComparison.Ordinal)
            )
        )
        {
            throw new InvalidOperationException("Scheduled arrival work has no correlated active travel.");
        }
    }

    private static void ValidateTravelingCandidateV2(
        ShipSnapshotV2 ship,
        StrategicMapSnapshotV2 map,
        SchedulerSnapshotV2 scheduler,
        long currentTime
    )
    {
        StrategicStateSnapshotV2 strategic = ship.StrategicState;
        if (strategic.LocationId is not null || strategic.Travel is null)
        {
            throw new InvalidOperationException("Traveling state requires only the explicit active travel member.");
        }

        TravelSnapshotV2 travel = strategic.Travel;
        ValidateText(travel.Origin, "Travel origin", LocationId.MaximumLength);
        ValidateText(travel.Destination, "Travel destination", LocationId.MaximumLength);
        StrategicRouteSnapshotV2? route = map.Routes.FirstOrDefault(candidate =>
            (
                string.Equals(candidate.Origin, travel.Origin, StringComparison.Ordinal)
                && string.Equals(candidate.Destination, travel.Destination, StringComparison.Ordinal)
            )
            || (
                string.Equals(candidate.Origin, travel.Destination, StringComparison.Ordinal)
                && string.Equals(candidate.Destination, travel.Origin, StringComparison.Ordinal)
            )
        );
        if (route is null)
        {
            throw new InvalidOperationException("Active travel does not follow a strategic map route.");
        }

        EnsureFixedStep(travel.DepartureMilliseconds, "Travel departure");
        EnsureFixedStep(travel.ExpectedArrivalMilliseconds, "Travel arrival");
        if (
            currentTime < travel.DepartureMilliseconds
            || currentTime >= travel.ExpectedArrivalMilliseconds
            || travel.ExpectedArrivalMilliseconds - travel.DepartureMilliseconds != route.DurationMilliseconds
        )
        {
            throw new InvalidOperationException("Active travel time is outside its route and current-time contract.");
        }

        if (ship.TacticalMotion.HeadingDegrees != 0 || ship.TacticalMotion.SpeedKilometersPerSecond != 0)
        {
            throw new InvalidOperationException("Active strategic travel requires cleared local tactical motion.");
        }

        EnsureExactlyCorrelatedV2(
            scheduler,
            ship.InstanceId,
            travel.ScheduledArrivalId,
            travel.ExpectedArrivalMilliseconds,
            TravelArrivalKind,
            "travel"
        );
    }

    private static void ValidateRepairCandidateV2(
        ShipSnapshotV2 ship,
        ShipDefinition definition,
        SchedulerSnapshotV2 scheduler,
        long currentTime
    )
    {
        if (ship.SensorRepair is null)
        {
            if (
                scheduler.OutstandingWork.Any(work =>
                    work.TargetShipId == ship.InstanceId
                    && string.Equals(work.Kind, SensorRepairCompletionKind, StringComparison.Ordinal)
                )
            )
            {
                throw new InvalidOperationException("Scheduled sensor completion has no correlated active repair.");
            }

            return;
        }

        SensorRepairSnapshotV2 repair = ship.SensorRepair;
        EnsureUnitInterval(repair.StartingIntegrity, "Sensor repair starting integrity");
        EnsureUnitInterval(repair.TargetIntegrity, "Sensor repair target integrity");
        if (repair.TargetIntegrity <= repair.StartingIntegrity)
        {
            throw new InvalidOperationException("Sensor repair target must exceed its starting integrity.");
        }

        EnsureFixedStep(repair.StartedAtMilliseconds, "Sensor repair start");
        EnsureFixedStep(repair.ExpectedCompletionMilliseconds, "Sensor repair completion");
        if (currentTime < repair.StartedAtMilliseconds || currentTime >= repair.ExpectedCompletionMilliseconds)
        {
            throw new InvalidOperationException("Active sensor repair does not contain the current simulation time.");
        }

        if (
            repair.ExpectedCompletionMilliseconds - repair.StartedAtMilliseconds
            != definition.SensorRepairDuration.Milliseconds
        )
        {
            throw new InvalidOperationException("Sensor repair duration does not match its ship definition.");
        }

        double progress =
            (double)(currentTime - repair.StartedAtMilliseconds)
            / (repair.ExpectedCompletionMilliseconds - repair.StartedAtMilliseconds);
        double expectedIntegrity =
            repair.StartingIntegrity + ((repair.TargetIntegrity - repair.StartingIntegrity) * progress);
        if (ship.SensorIntegrity != expectedIntegrity)
        {
            throw new InvalidOperationException(
                "Sensor integrity does not match the active repair at the current time."
            );
        }

        EnsureExactlyCorrelatedV2(
            scheduler,
            ship.InstanceId,
            repair.ScheduledCompletionId,
            repair.ExpectedCompletionMilliseconds,
            SensorRepairCompletionKind,
            "sensor repair"
        );
    }

    private static void EnsureExactlyCorrelatedV2(
        SchedulerSnapshotV2 scheduler,
        long targetShipId,
        long workId,
        long dueTime,
        string kind,
        string operation
    )
    {
        int count = scheduler.OutstandingWork.Count(work =>
            work.TargetShipId == targetShipId
            && work.Id == workId
            && work.DueTimeMilliseconds == dueTime
            && string.Equals(work.Kind, kind, StringComparison.Ordinal)
        );
        if (count != 1)
        {
            throw new InvalidOperationException(
                $"Active {operation} must have exactly one same-target correlated scheduled work item."
            );
        }
    }

    private static StrategicMap RestoreMapV2(StrategicMapSnapshotV2 snapshot) =>
        new(
            snapshot.Locations.Select(location => new StrategicLocation(
                new LocationId(location.Id),
                location.DisplayName,
                new StrategicMapPosition(location.Position.XUnitless, location.Position.YUnitless)
            )),
            snapshot.Routes.Select(route => new StrategicRoute(
                new LocationId(route.Origin),
                new LocationId(route.Destination),
                new SimulationDuration(route.DurationMilliseconds)
            ))
        );

    private static SimulationScheduler RestoreSchedulerV2(SchedulerSnapshotV2 snapshot, int sourceSchemaVersion) =>
        SimulationScheduler.Restore(
            snapshot.NextWorkId,
            snapshot.NextSequence,
            snapshot.OutstandingWork.Select(work => new ScheduledWork(
                new ScheduledWorkId(work.Id),
                new SimulationTime(work.DueTimeMilliseconds),
                work.Sequence,
                new ShipInstanceId(work.TargetShipId),
                ParseWorkKind(work.Kind, sourceSchemaVersion)
            ))
        );

    private static SimulationScheduler RestoreSchedulerV7(SaveModelsV7.SchedulerSnapshotV7 snapshot) =>
        SimulationScheduler.Restore(
            snapshot.NextWorkId,
            snapshot.NextSequence,
            snapshot.OutstandingWork.Select(work => new ScheduledWork(
                new ScheduledWorkId(work.Id),
                new SimulationTime(work.DueTimeMilliseconds),
                work.Sequence,
                ParseWorkTargetV7(work),
                ParseWorkKind(work.Kind, CurrentSchemaVersion)
            ))
        );

    private static ShipState RestoreShipV6(ShipSnapshotV6 snapshot) =>
        new(
            new ShipInstanceId(snapshot.InstanceId),
            new ShipDefinitionId(snapshot.DefinitionId),
            snapshot.DisplayName,
            new TacticalPosition(snapshot.TacticalPosition.XKilometers, snapshot.TacticalPosition.YKilometers),
            new TacticalMotion(
                new HeadingDegrees(snapshot.TacticalMotion.HeadingDegrees),
                new SpeedKilometersPerSecond(snapshot.TacticalMotion.SpeedKilometersPerSecond)
            ),
            new ShipEngineeringState(
                new SystemCondition(snapshot.Engineering.GenerationCondition),
                new SystemCondition(snapshot.Engineering.SensorCondition),
                new SystemCondition(snapshot.Engineering.ImpulseCondition),
                new PowerAllocation(
                    new PowerUnits(snapshot.Engineering.SensorAllocation),
                    new PowerUnits(snapshot.Engineering.ImpulseAllocation)
                ),
                RestoreSystemRepairV5(snapshot.Engineering.ActiveRepair)
            ),
            RestoreStrategicStateV2(snapshot.StrategicState),
            RestoreOrderV3(snapshot.ActiveOrder),
            RestoreSensorKnowledgeV6(snapshot.SensorKnowledge),
            RestoreAutonomousStateV4(snapshot.AutonomousState)
        );

    private static ShipState RestoreShipV7(ShipSnapshotV7 snapshot) =>
        new(
            new ShipInstanceId(snapshot.InstanceId),
            new ShipDefinitionId(snapshot.DefinitionId),
            snapshot.DisplayName,
            new TacticalPosition(snapshot.TacticalPosition.XKilometers, snapshot.TacticalPosition.YKilometers),
            new TacticalMotion(
                new HeadingDegrees(snapshot.TacticalMotion.HeadingDegrees),
                new SpeedKilometersPerSecond(snapshot.TacticalMotion.SpeedKilometersPerSecond)
            ),
            new ShipEngineeringState(
                new SystemCondition(snapshot.Engineering.GenerationCondition),
                new SystemCondition(snapshot.Engineering.SensorCondition),
                new SystemCondition(snapshot.Engineering.ImpulseCondition),
                new PowerAllocation(
                    new PowerUnits(snapshot.Engineering.SensorAllocation),
                    new PowerUnits(snapshot.Engineering.ImpulseAllocation)
                ),
                RestoreSystemRepairV5(snapshot.Engineering.ActiveRepair)
            ),
            RestoreStrategicStateV2(snapshot.StrategicState),
            RestoreOrderV3(snapshot.ActiveOrder),
            RestoreSensorKnowledgeV6(snapshot.SensorKnowledge),
            RestoreAutonomousStateV4(snapshot.AutonomousState),
            snapshot.DirectControllerFactionId is null ? null : new FactionId(snapshot.DirectControllerFactionId.Value)
        );

    private static FactionState RestoreFactionV7(SaveModelsV7.FactionSnapshotV7 snapshot) =>
        new(
            new FactionId(snapshot.Id),
            new FactionDefinitionId(snapshot.DefinitionId),
            snapshot.PresenceObjective is null
                ? null
                : new EstablishPresenceObjectiveState(
                    new LocationId(snapshot.PresenceObjective.TargetLocationId),
                    ParseObjectiveStatus(snapshot.PresenceObjective.Status),
                    snapshot.PresenceObjective.AssignedShipId is null
                        ? null
                        : new ShipInstanceId(snapshot.PresenceObjective.AssignedShipId.Value),
                    snapshot.PresenceObjective.AssignedOrderId is null
                        ? null
                        : new ShipOrderId(snapshot.PresenceObjective.AssignedOrderId.Value)
                ),
            snapshot.PendingDecisionWake is null
                ? null
                : new PendingFactionDecisionWake(
                    new ScheduledWorkId(snapshot.PendingDecisionWake.WorkId),
                    new SimulationTime(snapshot.PendingDecisionWake.DueTimeMilliseconds)
                )
        );

    private static SensorKnowledge RestoreSensorKnowledgeV6(SaveModelsV6.SensorKnowledgeSnapshotV6 snapshot) =>
        new(
            snapshot.NextContactId,
            snapshot.Contacts.Select(contact => new SensorContactTrack(
                new SensorContactId(contact.Id),
                new ShipInstanceId(contact.TargetShipId),
                new TacticalPosition(
                    contact.LastObservedPosition.XKilometers,
                    contact.LastObservedPosition.YKilometers
                ),
                new SimulationTime(contact.LastObservedAtMilliseconds),
                // A null frame is restored as a null frame: the document records an observation that
                // predates the schema recording locations, and no location may be synthesized for it.
                // Candidate validation has already bounded and resolved every non-null identity, so
                // the aggregate's own observed-location rules are the only remaining check.
                contact.ObservedAtLocationId
                    is null
                    ? null
                    : new LocationId(contact.ObservedAtLocationId),
                ParseContactStatus(contact.Status),
                ParseContactIdentification(contact.Identification),
                contact.KnownVesselDisplayName,
                contact.KnownDesignDisplayName,
                contact.LossWorkId is null ? null : new ScheduledWorkId(contact.LossWorkId.Value),
                contact.LossDueTimeMilliseconds is null
                    ? null
                    : new SimulationTime(contact.LossDueTimeMilliseconds.Value)
            )),
            snapshot.ActiveScan is null
                ? null
                : new ActiveSensorScanState(
                    new SensorContactId(snapshot.ActiveScan.TargetContactId),
                    new SimulationTime(snapshot.ActiveScan.StartedAtMilliseconds),
                    new SimulationTime(snapshot.ActiveScan.ExpectedCompletionMilliseconds),
                    new ScheduledWorkId(snapshot.ActiveScan.ScheduledCompletionId)
                )
        );

    private static ShipAutonomousState RestoreAutonomousStateV4(SaveModelsV4.ShipAutonomousSnapshotV4 snapshot) =>
        new(
            snapshot.ContactPosture is null ? null : ParseContactPosture(snapshot.ContactPosture),
            snapshot.PendingContactDecisionWake is null
                ? null
                : new ShipContactDecisionWake(
                    new ScheduledWorkId(snapshot.PendingContactDecisionWake.ScheduledWorkId),
                    new SimulationTime(snapshot.PendingContactDecisionWake.DueTimeMilliseconds)
                )
        );

    private static ShipOrder? RestoreOrderV3(ShipOrderSnapshotV3? snapshot) =>
        snapshot switch
        {
            null => null,
            TravelToOrderSnapshotV3 travel => new TravelToOrder(
                new ShipOrderId(travel.Id),
                new LocationId(travel.Destination)
            ),
            PatrolRouteOrderSnapshotV3 patrol => new PatrolRouteOrder(
                new ShipOrderId(patrol.Id),
                patrol.Waypoints.Select(waypoint => new LocationId(waypoint)),
                patrol.NextWaypointIndex
            ),
            HoldUntilOrderSnapshotV3 hold => new HoldUntilOrder(
                new ShipOrderId(hold.Id),
                new SimulationTime(hold.UntilMilliseconds),
                new ScheduledWorkId(hold.ScheduledWakeId)
            ),
            _ => throw new InvalidOperationException("Active ship order kind is unknown."),
        };

    private static SystemRepairState? RestoreSystemRepairV5(SaveModelsV5.SystemRepairSnapshotV5? snapshot) =>
        snapshot is null
            ? null
            : new SystemRepairState(
                ShipSystemId.Parse(snapshot.TargetSystem),
                new SystemCondition(snapshot.StartingCondition),
                new SystemCondition(snapshot.TargetCondition),
                new SimulationTime(snapshot.StartedAtMilliseconds),
                new SimulationTime(snapshot.ExpectedCompletionMilliseconds),
                new ScheduledWorkId(snapshot.ScheduledCompletionId)
            );

    private static ShipStrategicState RestoreStrategicStateV2(StrategicStateSnapshotV2 snapshot) =>
        snapshot.Kind switch
        {
            AtLocationKind => new AtLocationState(new LocationId(snapshot.LocationId!)),
            TravelingKind => new TravelingState(
                new TravelState(
                    new LocationId(snapshot.Travel!.Origin),
                    new LocationId(snapshot.Travel.Destination),
                    new SimulationTime(snapshot.Travel.DepartureMilliseconds),
                    new SimulationTime(snapshot.Travel.ExpectedArrivalMilliseconds),
                    new ScheduledWorkId(snapshot.Travel.ScheduledArrivalId)
                )
            ),
            _ => throw new InvalidOperationException("Ship strategic state kind is unknown."),
        };

    private static ScheduledWorkTarget ParseWorkTargetV7(SaveModelsV7.ScheduledWorkSnapshotV7 work) =>
        work.TargetKind switch
        {
            ShipTargetKind when work.TargetShipId is { } shipId && work.TargetFactionId is null =>
                ScheduledWorkTarget.ForShip(new ShipInstanceId(shipId)),
            FactionTargetKind when work.TargetFactionId is { } factionId && work.TargetShipId is null =>
                ScheduledWorkTarget.ForFaction(new FactionId(factionId)),
            ShipTargetKind or FactionTargetKind => throw new InvalidOperationException(
                "Scheduled work requires exactly one identity matching its target kind."
            ),
            _ => throw new InvalidOperationException("Scheduled work target kind is unknown."),
        };

    private static FactionObjectiveStatus ParseObjectiveStatus(string? status) =>
        status switch
        {
            PendingObjectiveStatus => FactionObjectiveStatus.Pending,
            AssignedObjectiveStatus => FactionObjectiveStatus.Assigned,
            SatisfiedObjectiveStatus => FactionObjectiveStatus.Satisfied,
            _ => throw new InvalidOperationException("Faction objective status is unknown."),
        };

    private static ScheduledWorkKind ParseWorkKind(string? kind, int sourceSchemaVersion)
    {
        if (
            (
                sourceSchemaVersion <= V4SchemaVersion
                && string.Equals(kind, SystemRepairCompletionKind, StringComparison.Ordinal)
            )
            // The sensor-specific repair kind was replaced by the general system kind in V5, so every
            // schema from V5 onward rejects the legacy token rather than silently accepting both.
            || (
                sourceSchemaVersion >= V5SchemaVersion
                && string.Equals(kind, SensorRepairCompletionKind, StringComparison.Ordinal)
            )
        )
        {
            throw new InvalidOperationException("Scheduled repair work kind is unsupported by schema.");
        }

        ScheduledWorkKind parsed = kind switch
        {
            TravelArrivalKind => ScheduledWorkKind.TravelArrival,
            SensorRepairCompletionKind => ScheduledWorkKind.SystemRepairCompletion,
            SystemRepairCompletionKind => ScheduledWorkKind.SystemRepairCompletion,
            OrderWakeKind => ScheduledWorkKind.OrderWake,
            SensorContactLossKind => ScheduledWorkKind.SensorContactLoss,
            ActiveSensorScanCompletionKind => ScheduledWorkKind.ActiveSensorScanCompletion,
            ShipContactDecisionWakeKind => ScheduledWorkKind.ShipContactDecisionWake,
            FactionDecisionWakeKind => ScheduledWorkKind.FactionDecisionWake,
            _ => throw new InvalidOperationException("Scheduled work kind is unknown."),
        };

        bool allowed = sourceSchemaVersion switch
        {
            V1SchemaVersion or V2SchemaVersion => parsed
                is ScheduledWorkKind.TravelArrival
                    or ScheduledWorkKind.SystemRepairCompletion,
            V3SchemaVersion => parsed
                is ScheduledWorkKind.TravelArrival
                    or ScheduledWorkKind.SystemRepairCompletion
                    or ScheduledWorkKind.OrderWake,
            V4SchemaVersion or V5SchemaVersion or V6SchemaVersion => parsed
                is ScheduledWorkKind.TravelArrival
                    or ScheduledWorkKind.SystemRepairCompletion
                    or ScheduledWorkKind.OrderWake
                    or ScheduledWorkKind.SensorContactLoss
                    or ScheduledWorkKind.ActiveSensorScanCompletion
                    or ScheduledWorkKind.ShipContactDecisionWake,
            CurrentSchemaVersion => parsed
                is ScheduledWorkKind.TravelArrival
                    or ScheduledWorkKind.SystemRepairCompletion
                    or ScheduledWorkKind.OrderWake
                    or ScheduledWorkKind.SensorContactLoss
                    or ScheduledWorkKind.ActiveSensorScanCompletion
                    or ScheduledWorkKind.ShipContactDecisionWake
                    or ScheduledWorkKind.FactionDecisionWake,
            _ => false,
        };
        return allowed ? parsed : throw new InvalidOperationException("Scheduled work kind is unsupported by schema.");
    }

    private static SensorContactStatus ParseContactStatus(string? status) =>
        status switch
        {
            CurrentContactStatus => SensorContactStatus.Current,
            StaleContactStatus => SensorContactStatus.Stale,
            LostContactStatus => SensorContactStatus.Lost,
            _ => throw new InvalidOperationException("Sensor contact status is unknown."),
        };

    private static SensorContactIdentification ParseContactIdentification(string? identification) =>
        identification switch
        {
            DetectedContactIdentification => SensorContactIdentification.Detected,
            IdentifiedContactIdentification => SensorContactIdentification.Identified,
            _ => throw new InvalidOperationException("Sensor contact identification is unknown."),
        };

    private static ShipContactPosture ParseContactPosture(string? posture) =>
        posture switch
        {
            CautiousContactPosture => ShipContactPosture.CautiousContact,
            _ => throw new InvalidOperationException("Ship contact posture is unknown."),
        };

    private static void EnsureFinite(double value, string label)
    {
        if (!double.IsFinite(value))
        {
            throw new InvalidOperationException($"{label} must be finite.");
        }
    }

    private static void EnsureUnitInterval(double value, string label)
    {
        if (!double.IsFinite(value) || value is < 0 or > 1)
        {
            throw new InvalidOperationException($"{label} must be finite and between zero and one.");
        }
    }

    private static int ReadSchemaVersion(JsonElement root, string sourceIdentity)
    {
        if (
            root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("schemaVersion", out JsonElement versionElement)
            || versionElement.ValueKind != JsonValueKind.Number
            || !versionElement.TryGetInt32(out int version)
        )
        {
            throw Failure(
                GamePersistenceFailure.InvalidData,
                sourceIdentity,
                "requires one integer 'schemaVersion' member."
            );
        }

        return version;
    }

    private static void RejectDuplicateMembers(JsonElement element, string sourceIdentity, string path, int depth)
    {
        if (depth > MaximumJsonDepth)
        {
            throw Failure(
                GamePersistenceFailure.InvalidData,
                sourceIdentity,
                $"exceeds the JSON depth limit at '{path}'."
            );
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw Failure(
                        GamePersistenceFailure.InvalidData,
                        sourceIdentity,
                        $"contains duplicate JSON member '{property.Name}' at '{path}'."
                    );
                }

                RejectDuplicateMembers(property.Value, sourceIdentity, $"{path}.{property.Name}", depth + 1);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
            {
                RejectDuplicateMembers(item, sourceIdentity, $"{path}[{index}]", depth + 1);
                index++;
            }
        }
    }

    private static void ValidateMetadata(GameSaveMetadata metadata)
    {
        ValidateMetadataText(metadata.SaveId, "Save identity");
        ValidateMetadataText(metadata.DisplayName, "Save display name");
        if (metadata.CreatedAtUtc.Offset != TimeSpan.Zero || metadata.SavedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Save organization timestamps must use UTC offsets.", nameof(metadata));
        }

        if (metadata.SavedAtUtc < metadata.CreatedAtUtc)
        {
            throw new ArgumentException("Save timestamp cannot precede creation timestamp.", nameof(metadata));
        }
    }

    private static void ValidateMetadataText(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{label} is required.", nameof(value));
        }

        if (value.Length > MaximumMetadataTextLength)
        {
            throw new ArgumentException(
                $"{label} exceeds the {MaximumMetadataTextLength}-character limit.",
                nameof(value)
            );
        }
    }

    private static void ValidateText(string? value, string label, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{label} is required.");
        }

        if (value.Length > maximumLength)
        {
            throw new InvalidOperationException($"{label} exceeds the {maximumLength}-character limit.");
        }
    }

    private static void EnsureCount(int count, int maximum, string label)
    {
        if (count > maximum)
        {
            throw new InvalidOperationException($"The save contains more than {maximum} {label}.");
        }
    }

    private static void EnsureFixedStep(long milliseconds, string label)
    {
        if (milliseconds % SimulationFixedStep.Duration.Milliseconds != 0)
        {
            throw new InvalidOperationException($"{label} must be fixed-step aligned.");
        }

        if (milliseconds > long.MaxValue - SimulationFixedStep.Duration.Milliseconds)
        {
            throw new InvalidOperationException($"{label} must retain one fixed step of continuation headroom.");
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            AllowTrailingCommas = false,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = MaximumJsonDepth,
            WriteIndented = true,
        };
        options.Converters.Add(new FiniteDoubleJsonConverter());
        return options;
    }

    private static GamePersistenceException Failure(
        GamePersistenceFailure failure,
        string sourceIdentity,
        string message,
        Exception? innerException = null
    ) => new(failure, sourceIdentity, message, innerException);
}
