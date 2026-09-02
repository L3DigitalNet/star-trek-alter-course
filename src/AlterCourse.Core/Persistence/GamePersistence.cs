using System.Text.Json;
using System.Text.Json.Serialization;
using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;
using FiniteDoubleJsonConverter = AlterCourse.Core.Persistence.SaveModelsV1.FiniteDoubleJsonConverter;
using PlayerShipSnapshotV1 = AlterCourse.Core.Persistence.SaveModelsV1.PlayerShipSnapshotV1;
using SaveEnvelopeV1 = AlterCourse.Core.Persistence.SaveModelsV1.SaveEnvelopeV1;
using SaveEnvelopeV2 = AlterCourse.Core.Persistence.SaveModelsV2.SaveEnvelopeV2;
using SaveMetadataV2 = AlterCourse.Core.Persistence.SaveModelsV2.SaveMetadataV2;
using ScheduledWorkSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.ScheduledWorkSnapshotV2;
using SchedulerSnapshotV1 = AlterCourse.Core.Persistence.SaveModelsV1.SchedulerSnapshotV1;
using SchedulerSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.SchedulerSnapshotV2;
using SensorRepairSnapshotV1 = AlterCourse.Core.Persistence.SaveModelsV1.SensorRepairSnapshotV1;
using SensorRepairSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.SensorRepairSnapshotV2;
using ShipSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.ShipSnapshotV2;
using SimulationSnapshotV1 = AlterCourse.Core.Persistence.SaveModelsV1.SimulationSnapshotV1;
using SimulationSnapshotV2 = AlterCourse.Core.Persistence.SaveModelsV2.SimulationSnapshotV2;
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

namespace AlterCourse.Core.Persistence;

/// <summary>Maps the authoritative simulation to and from the strict explicit JSON save contract.</summary>
public static class GamePersistence
{
    private const int V1SchemaVersion = 1;
    private const int CurrentSchemaVersion = 2;
    private const string CurrentSimulationRulesVersion = "first-playable-v1";
    private const string TravelArrivalKind = "travelArrival";
    private const string SensorRepairCompletionKind = "sensorRepairCompletion";
    private const string AtLocationKind = "atLocation";
    private const string TravelingKind = "traveling";
    private const int MaximumSaveBytes = 1024 * 1024;
    private const int MaximumJsonDepth = 32;
    private const int MaximumMetadataTextLength = 128;

    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = MaximumJsonDepth,
    };

    /// <summary>Serializes a validated simulation and caller-supplied organization metadata as V2 UTF-8 JSON.</summary>
    public static byte[] Serialize(GameSimulation simulation, GameSaveMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(metadata);
        ValidateMetadata(metadata);

        SaveEnvelopeV2 envelope = CaptureV2(simulation.CaptureState(), metadata);
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(envelope, SerializerOptions);
        if (json.Length > MaximumSaveBytes)
        {
            throw new InvalidOperationException($"The V2 save exceeds the {MaximumSaveBytes}-byte contract limit.");
        }

        return json;
    }

    /// <summary>Loads bounded untrusted UTF-8 JSON into a new simulation without mutating another aggregate.</summary>
    public static LoadedGameSave Deserialize(
        ReadOnlySpan<byte> utf8Json,
        ShipDefinitionCatalog catalog,
        string sourceIdentity
    )
    {
        ArgumentNullException.ThrowIfNull(catalog);
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
                V1SchemaVersion => LoadV1(documentBytes, catalog, sourceIdentity),
                CurrentSchemaVersion => LoadV2(documentBytes, catalog, sourceIdentity),
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
    public static LoadedGameSave Load(string path, ShipDefinitionCatalog catalog)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(catalog);
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

            return Deserialize(json, catalog, sourceIdentity);
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

    private static SaveEnvelopeV2 CaptureV2(SimulationState state, GameSaveMetadata metadata)
    {
        if (state.Ships.Length > SimulationState.MaximumShips)
        {
            throw new InvalidOperationException(
                $"V2 persistence supports at most {SimulationState.MaximumShips} ships."
            );
        }

        return new SaveEnvelopeV2
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
            Simulation = new SimulationSnapshotV2
            {
                TimeMilliseconds = state.Time.Milliseconds,
                ShipAllocatorNextId = state.ShipIdAllocator.NextId,
                PlayerShipId = state.PlayerShipId.Value,
                Scheduler = CaptureSchedulerV2(state.Scheduler),
                StrategicMap = CaptureStrategicMapV2(state.StrategicMap),
                Ships = [.. state.Ships.OrderBy(ship => ship.InstanceId.Value).Select(CaptureShipV2)],
            },
        };
    }

    private static SchedulerSnapshotV2 CaptureSchedulerV2(SimulationScheduler scheduler) =>
        new()
        {
            NextWorkId = scheduler.NextWorkId,
            NextSequence = scheduler.NextSequence,
            OutstandingWork =
            [
                .. scheduler.OutstandingWork.Select(work => new ScheduledWorkSnapshotV2
                {
                    Id = work.Id.Value,
                    DueTimeMilliseconds = work.DueTime.Milliseconds,
                    Sequence = work.Sequence,
                    Kind = CaptureWorkKind(work.Kind),
                    TargetShipId = work.TargetShipId.Value,
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

    private static ShipSnapshotV2 CaptureShipV2(ShipState ship) =>
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
            SensorIntegrity = ship.SensorIntegrity.Value,
            SensorRepair = ship.SensorRepair is null
                ? null
                : new SensorRepairSnapshotV2
                {
                    StartingIntegrity = ship.SensorRepair.StartingIntegrity.Value,
                    TargetIntegrity = ship.SensorRepair.TargetIntegrity.Value,
                    StartedAtMilliseconds = ship.SensorRepair.StartedAt.Milliseconds,
                    ExpectedCompletionMilliseconds = ship.SensorRepair.ExpectedCompletion.Milliseconds,
                    ScheduledCompletionId = ship.SensorRepair.ScheduledCompletionId.Value,
                },
            StrategicState = CaptureStrategicStateV2(ship.StrategicState),
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
            ScheduledWorkKind.SensorRepairCompletion => SensorRepairCompletionKind,
            _ => throw new InvalidOperationException("Cannot persist an unknown scheduled work kind."),
        };

    private static LoadedGameSave LoadV1(byte[] json, ShipDefinitionCatalog catalog, string sourceIdentity)
    {
        try
        {
            SaveEnvelopeV1 envelope =
                JsonSerializer.Deserialize<SaveEnvelopeV1>(json, SerializerOptions)
                ?? throw new JsonException("The save root must be an object.");
            ValidateEnvelopeV1(envelope);
            SaveEnvelopeV2 migrated = MigrateV1ToV2(envelope, catalog);
            return RestoreV2(migrated, catalog);
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

    private static LoadedGameSave LoadV2(byte[] json, ShipDefinitionCatalog catalog, string sourceIdentity)
    {
        try
        {
            SaveEnvelopeV2 envelope =
                JsonSerializer.Deserialize<SaveEnvelopeV2>(json, SerializerOptions)
                ?? throw new JsonException("The save root must be an object.");
            return RestoreV2(envelope, catalog);
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
            SchemaVersion = CurrentSchemaVersion,
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

        if (!string.Equals(envelope.SimulationRulesVersion, CurrentSimulationRulesVersion, StringComparison.Ordinal))
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

    private static LoadedGameSave RestoreV2(SaveEnvelopeV2 envelope, ShipDefinitionCatalog catalog)
    {
        ValidateCandidateV2(envelope, catalog);
        SimulationSnapshotV2 snapshot = envelope.Simulation;
        var time = new SimulationTime(snapshot.TimeMilliseconds);
        StrategicMap map = RestoreMapV2(snapshot.StrategicMap);
        ShipState[] ships = [.. snapshot.Ships.Select(RestoreShipV2)];
        SimulationScheduler scheduler = RestoreSchedulerV2(snapshot.Scheduler);
        var state = new SimulationState(
            time,
            scheduler,
            ShipInstanceIdAllocator.Restore(snapshot.ShipAllocatorNextId),
            map,
            new ShipInstanceId(snapshot.PlayerShipId),
            ships
        );
        var metadata = new GameSaveMetadata(
            envelope.Metadata.SaveId,
            envelope.Metadata.DisplayName,
            envelope.Metadata.CreatedAtUtc,
            envelope.Metadata.SavedAtUtc
        );
        return new LoadedGameSave(metadata, GameSimulation.RestoreState(state, catalog));
    }

    private static void ValidateCandidateV2(SaveEnvelopeV2 envelope, ShipDefinitionCatalog catalog)
    {
        if (envelope.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidOperationException("The V2 mapper received a different schema version.");
        }

        if (!string.Equals(envelope.SimulationRulesVersion, CurrentSimulationRulesVersion, StringComparison.Ordinal))
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

        ValidateSimulationCandidateV2(envelope.Simulation, catalog);
    }

    private static void ValidateSimulationCandidateV2(SimulationSnapshotV2 snapshot, ShipDefinitionCatalog catalog)
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

        ValidateSchedulerCandidateV2(snapshot.Scheduler, snapshot.TimeMilliseconds, shipIds);
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
        HashSet<long> shipIds
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
                sequences
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

        if (!shipIds.Contains(work.TargetShipId))
        {
            throw new InvalidOperationException("Scheduled work target ship does not exist.");
        }

        ParseWorkKind(work.Kind);
        return work;
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

    private static SimulationScheduler RestoreSchedulerV2(SchedulerSnapshotV2 snapshot) =>
        SimulationScheduler.Restore(
            snapshot.NextWorkId,
            snapshot.NextSequence,
            snapshot.OutstandingWork.Select(work => new ScheduledWork(
                new ScheduledWorkId(work.Id),
                new SimulationTime(work.DueTimeMilliseconds),
                work.Sequence,
                new ShipInstanceId(work.TargetShipId),
                ParseWorkKind(work.Kind)
            ))
        );

    private static ShipState RestoreShipV2(ShipSnapshotV2 snapshot) =>
        new(
            new ShipInstanceId(snapshot.InstanceId),
            new ShipDefinitionId(snapshot.DefinitionId),
            snapshot.DisplayName,
            new TacticalPosition(snapshot.TacticalPosition.XKilometers, snapshot.TacticalPosition.YKilometers),
            new TacticalMotion(
                new HeadingDegrees(snapshot.TacticalMotion.HeadingDegrees),
                new SpeedKilometersPerSecond(snapshot.TacticalMotion.SpeedKilometersPerSecond)
            ),
            new SensorIntegrity(snapshot.SensorIntegrity),
            RestoreSensorRepairV2(snapshot.SensorRepair),
            RestoreStrategicStateV2(snapshot.StrategicState)
        );

    private static SensorRepairState? RestoreSensorRepairV2(SensorRepairSnapshotV2? snapshot) =>
        snapshot is null
            ? null
            : new SensorRepairState(
                new SensorIntegrity(snapshot.StartingIntegrity),
                new SensorIntegrity(snapshot.TargetIntegrity),
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

    private static ScheduledWorkKind ParseWorkKind(string? kind) =>
        kind switch
        {
            TravelArrivalKind => ScheduledWorkKind.TravelArrival,
            SensorRepairCompletionKind => ScheduledWorkKind.SensorRepairCompletion,
            _ => throw new InvalidOperationException("Scheduled work kind is unknown."),
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
