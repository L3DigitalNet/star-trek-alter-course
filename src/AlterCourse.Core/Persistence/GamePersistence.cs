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
using SaveMetadataV1 = AlterCourse.Core.Persistence.SaveModelsV1.SaveMetadataV1;
using ScheduledWorkSnapshotV1 = AlterCourse.Core.Persistence.SaveModelsV1.ScheduledWorkSnapshotV1;
using SchedulerSnapshotV1 = AlterCourse.Core.Persistence.SaveModelsV1.SchedulerSnapshotV1;
using SensorRepairSnapshotV1 = AlterCourse.Core.Persistence.SaveModelsV1.SensorRepairSnapshotV1;
using SimulationSnapshotV1 = AlterCourse.Core.Persistence.SaveModelsV1.SimulationSnapshotV1;
using StrategicLocationSnapshotV1 = AlterCourse.Core.Persistence.SaveModelsV1.StrategicLocationSnapshotV1;
using StrategicMapSnapshotV1 = AlterCourse.Core.Persistence.SaveModelsV1.StrategicMapSnapshotV1;
using StrategicPositionSnapshotV1 = AlterCourse.Core.Persistence.SaveModelsV1.StrategicPositionSnapshotV1;
using StrategicRouteSnapshotV1 = AlterCourse.Core.Persistence.SaveModelsV1.StrategicRouteSnapshotV1;
using StrategicStateSnapshotV1 = AlterCourse.Core.Persistence.SaveModelsV1.StrategicStateSnapshotV1;
using TacticalMotionSnapshotV1 = AlterCourse.Core.Persistence.SaveModelsV1.TacticalMotionSnapshotV1;
using TacticalPositionSnapshotV1 = AlterCourse.Core.Persistence.SaveModelsV1.TacticalPositionSnapshotV1;
using TravelSnapshotV1 = AlterCourse.Core.Persistence.SaveModelsV1.TravelSnapshotV1;

namespace AlterCourse.Core.Persistence;

/// <summary>Maps the authoritative simulation to and from the strict explicit JSON save contract.</summary>
public static class GamePersistence
{
    private const int CurrentSchemaVersion = 1;
    private const string CurrentSimulationRulesVersion = "first-playable-v1";
    private const string TravelArrivalKind = "travelArrival";
    private const string SensorRepairCompletionKind = "sensorRepairCompletion";
    private const string AtLocationKind = "atLocation";
    private const string TravelingKind = "traveling";
    private const int MaximumSaveBytes = 1024 * 1024;
    private const int MaximumJsonDepth = 32;
    private const int MaximumMetadataTextLength = 128;
    private const int MaximumIdentityLength = 128;
    private const int MaximumDisplayNameLength = 256;
    private const int MaximumLocations = 256;
    private const int MaximumRoutes = 1024;
    private const int MaximumOutstandingWork = 4096;

    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = MaximumJsonDepth,
    };

    /// <summary>Serializes a validated simulation and caller-supplied organization metadata as V1 UTF-8 JSON.</summary>
    public static byte[] Serialize(GameSimulation simulation, GameSaveMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(metadata);
        ValidateMetadata(metadata);

        SaveEnvelopeV1 envelope = CaptureV1(simulation.CaptureState(), metadata);
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(envelope, SerializerOptions);
        if (json.Length > MaximumSaveBytes)
        {
            throw new InvalidOperationException($"The V1 save exceeds the {MaximumSaveBytes}-byte contract limit.");
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

            // Each supported version enters through one explicit branch. V2 can add a new mapper or
            // a V1-to-V2 migration here without permitting version fallback or live-state mutation.
            return version switch
            {
                CurrentSchemaVersion => LoadV1(documentBytes, catalog, sourceIdentity),
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

    private static SaveEnvelopeV1 CaptureV1(SimulationState state, GameSaveMetadata metadata) =>
        new()
        {
            SchemaVersion = CurrentSchemaVersion,
            SimulationRulesVersion = CurrentSimulationRulesVersion,
            Metadata = new SaveMetadataV1
            {
                SaveId = metadata.SaveId,
                DisplayName = metadata.DisplayName,
                CreatedAtUtc = metadata.CreatedAtUtc,
                SavedAtUtc = metadata.SavedAtUtc,
            },
            Simulation = new SimulationSnapshotV1
            {
                TimeMilliseconds = state.Time.Milliseconds,
                ShipAllocatorNextId = state.ShipIdAllocator.NextId,
                Scheduler = CaptureScheduler(state.Scheduler),
                StrategicMap = CaptureStrategicMap(state.StrategicMap),
                StrategicState = CaptureStrategicState(state.StrategicState),
                PlayerShip = CapturePlayerShip(state.PlayerShip),
            },
        };

    private static SchedulerSnapshotV1 CaptureScheduler(SimulationScheduler scheduler) =>
        new()
        {
            NextWorkId = scheduler.NextWorkId,
            NextSequence = scheduler.NextSequence,
            OutstandingWork =
            [
                .. scheduler.OutstandingWork.Select(work => new ScheduledWorkSnapshotV1
                {
                    Id = work.Id.Value,
                    DueTimeMilliseconds = work.DueTime.Milliseconds,
                    Sequence = work.Sequence,
                    Kind = work.Kind switch
                    {
                        ScheduledWorkKind.TravelArrival => TravelArrivalKind,
                        ScheduledWorkKind.SensorRepairCompletion => SensorRepairCompletionKind,
                        _ => throw new InvalidOperationException("Cannot persist an unknown scheduled work kind."),
                    },
                }),
            ],
        };

    private static StrategicMapSnapshotV1 CaptureStrategicMap(StrategicMap map) =>
        new()
        {
            Locations =
            [
                .. map.Locations.Select(location => new StrategicLocationSnapshotV1
                {
                    Id = location.Id.Value,
                    DisplayName = location.DisplayName,
                    Position = new StrategicPositionSnapshotV1
                    {
                        XUnitless = location.Position.X,
                        YUnitless = location.Position.Y,
                    },
                }),
            ],
            Routes =
            [
                .. map.Routes.Select(route => new StrategicRouteSnapshotV1
                {
                    Origin = route.Origin.Value,
                    Destination = route.Destination.Value,
                    DurationMilliseconds = route.Duration.Milliseconds,
                }),
            ],
        };

    private static StrategicStateSnapshotV1 CaptureStrategicState(PlayerStrategicState state) =>
        state switch
        {
            AtLocationState atLocation => new StrategicStateSnapshotV1
            {
                Kind = AtLocationKind,
                LocationId = atLocation.LocationId.Value,
                Travel = null,
            },
            TravelingState traveling => new StrategicStateSnapshotV1
            {
                Kind = TravelingKind,
                LocationId = null,
                Travel = new TravelSnapshotV1
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

    private static PlayerShipSnapshotV1 CapturePlayerShip(PlayerShipState ship) =>
        new()
        {
            InstanceId = ship.InstanceId.Value,
            DefinitionId = ship.DefinitionId.Value,
            TacticalPosition = new TacticalPositionSnapshotV1
            {
                XKilometers = ship.TacticalPosition.XKilometers,
                YKilometers = ship.TacticalPosition.YKilometers,
            },
            TacticalMotion = new TacticalMotionSnapshotV1
            {
                HeadingDegrees = ship.TacticalMotion.Heading.Value,
                SpeedKilometersPerSecond = ship.TacticalMotion.Speed.Value,
            },
            SensorIntegrity = ship.SensorIntegrity.Value,
            SensorRepair = ship.SensorRepair is null
                ? null
                : new SensorRepairSnapshotV1
                {
                    StartingIntegrity = ship.SensorRepair.StartingIntegrity.Value,
                    TargetIntegrity = ship.SensorRepair.TargetIntegrity.Value,
                    StartedAtMilliseconds = ship.SensorRepair.StartedAt.Milliseconds,
                    ExpectedCompletionMilliseconds = ship.SensorRepair.ExpectedCompletion.Milliseconds,
                    ScheduledCompletionId = ship.SensorRepair.ScheduledCompletionId.Value,
                },
        };

    private static LoadedGameSave LoadV1(byte[] json, ShipDefinitionCatalog catalog, string sourceIdentity)
    {
        try
        {
            SaveEnvelopeV1 envelope =
                JsonSerializer.Deserialize<SaveEnvelopeV1>(json, SerializerOptions)
                ?? throw new JsonException("The save root must be an object.");
            ValidateEnvelopeV1(envelope);
            GameSaveMetadata metadata = RestoreMetadata(envelope.Metadata);
            GameSimulation simulation = RestoreSimulation(envelope.Simulation, catalog);
            return new LoadedGameSave(metadata, simulation);
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

    private static void ValidateEnvelopeV1(SaveEnvelopeV1 envelope)
    {
        if (envelope.SchemaVersion != CurrentSchemaVersion)
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

    private static GameSaveMetadata RestoreMetadata(SaveMetadataV1 metadata)
    {
        var restored = new GameSaveMetadata(
            metadata.SaveId,
            metadata.DisplayName,
            metadata.CreatedAtUtc,
            metadata.SavedAtUtc
        );
        ValidateMetadata(restored);
        return restored;
    }

    private static GameSimulation RestoreSimulation(SimulationSnapshotV1 snapshot, ShipDefinitionCatalog catalog)
    {
        if (
            snapshot.Scheduler is null
            || snapshot.StrategicMap is null
            || snapshot.StrategicState is null
            || snapshot.PlayerShip is null
        )
        {
            throw new InvalidOperationException("Required simulation members cannot be null.");
        }

        var time = new SimulationTime(snapshot.TimeMilliseconds);
        EnsureFixedStep(time.Milliseconds, "Current simulation time");
        var allocator = ShipInstanceIdAllocator.Restore(snapshot.ShipAllocatorNextId);
        SimulationScheduler scheduler = RestoreScheduler(snapshot.Scheduler);
        StrategicMap map = RestoreMap(snapshot.StrategicMap);
        PlayerStrategicState strategicState = RestoreStrategicState(snapshot.StrategicState, map, time);
        PlayerShipState playerShip = RestorePlayerShip(snapshot.PlayerShip, time);
        ShipDefinition definition = catalog.GetRequired(playerShip.DefinitionId);
        if (playerShip.TacticalMotion.Speed.Value > definition.MaximumTacticalSpeed.Value)
        {
            throw new InvalidOperationException("Player tactical speed exceeds its authored definition maximum.");
        }

        if (strategicState is TravelingState && playerShip.TacticalMotion != default)
        {
            throw new InvalidOperationException("Active strategic travel requires cleared local tactical motion.");
        }

        ValidateOutstandingTravel(scheduler, strategicState);
        ValidateOutstandingRepair(scheduler, playerShip.SensorRepair);
        var state = new SimulationState(time, scheduler, allocator, map, strategicState, definition, playerShip);
        return GameSimulation.RestoreState(state);
    }

    private static SimulationScheduler RestoreScheduler(SchedulerSnapshotV1 snapshot)
    {
        if (snapshot.OutstandingWork is null)
        {
            throw new InvalidOperationException("Outstanding scheduler work is required.");
        }

        EnsureCount(snapshot.OutstandingWork.Length, MaximumOutstandingWork, "scheduler work");
        if (snapshot.NextWorkId == long.MaxValue || snapshot.NextSequence == long.MaxValue)
        {
            throw new InvalidOperationException(
                "Scheduler counters must retain headroom for deterministic continuation."
            );
        }

        var identities = new HashSet<long>();
        var sequences = new HashSet<long>();
        var work = new ScheduledWork[snapshot.OutstandingWork.Length];
        long previousDue = -1;
        long previousSequence = -1;
        for (int index = 0; index < snapshot.OutstandingWork.Length; index++)
        {
            ScheduledWorkSnapshotV1 item =
                snapshot.OutstandingWork[index]
                ?? throw new InvalidOperationException("Scheduler work items cannot be null.");
            work[index] = RestoreScheduledWork(item, index, previousDue, previousSequence, identities, sequences);
            previousDue = item.DueTimeMilliseconds;
            previousSequence = item.Sequence;
        }

        return SimulationScheduler.Restore(snapshot.NextWorkId, snapshot.NextSequence, work);
    }

    private static ScheduledWork RestoreScheduledWork(
        ScheduledWorkSnapshotV1 item,
        int index,
        long previousDue,
        long previousSequence,
        HashSet<long> identities,
        HashSet<long> sequences
    )
    {
        EnsureFixedStep(item.DueTimeMilliseconds, "Scheduled due time");
        if (!identities.Add(item.Id))
        {
            throw new InvalidOperationException("Scheduler contains a duplicate work identity.");
        }

        if (!sequences.Add(item.Sequence))
        {
            throw new InvalidOperationException("Scheduler contains a duplicate work sequence.");
        }

        if (
            index > 0
            && (
                item.DueTimeMilliseconds < previousDue
                || (item.DueTimeMilliseconds == previousDue && item.Sequence <= previousSequence)
            )
        )
        {
            throw new InvalidOperationException(
                "Outstanding scheduler work is not in stable due-time and sequence order."
            );
        }

        return new ScheduledWork(
            new ScheduledWorkId(item.Id),
            new SimulationTime(item.DueTimeMilliseconds),
            item.Sequence,
            item.Kind switch
            {
                TravelArrivalKind => ScheduledWorkKind.TravelArrival,
                SensorRepairCompletionKind => ScheduledWorkKind.SensorRepairCompletion,
                _ => throw new InvalidOperationException("Scheduled work kind is unknown."),
            }
        );
    }

    private static StrategicMap RestoreMap(StrategicMapSnapshotV1 snapshot)
    {
        if (snapshot.Locations is null || snapshot.Routes is null)
        {
            throw new InvalidOperationException("Strategic map collections are required.");
        }

        EnsureCount(snapshot.Locations.Length, MaximumLocations, "strategic locations");
        EnsureCount(snapshot.Routes.Length, MaximumRoutes, "strategic routes");
        StrategicLocation[] locations =
        [
            .. snapshot.Locations.Select(location =>
            {
                if (location is null || location.Position is null)
                {
                    throw new InvalidOperationException("Strategic location data cannot be null.");
                }

                ValidateText(location.Id, "Location identity", MaximumIdentityLength);
                ValidateText(location.DisplayName, "Location display name", MaximumDisplayNameLength);
                return new StrategicLocation(
                    new LocationId(location.Id),
                    location.DisplayName,
                    new StrategicMapPosition(location.Position.XUnitless, location.Position.YUnitless)
                );
            }),
        ];
        StrategicRoute[] routes =
        [
            .. snapshot.Routes.Select(route =>
            {
                if (route is null)
                {
                    throw new InvalidOperationException("Strategic route data cannot be null.");
                }

                ValidateText(route.Origin, "Route origin", MaximumIdentityLength);
                ValidateText(route.Destination, "Route destination", MaximumIdentityLength);
                return new StrategicRoute(
                    new LocationId(route.Origin),
                    new LocationId(route.Destination),
                    new SimulationDuration(route.DurationMilliseconds)
                );
            }),
        ];
        return new StrategicMap(locations, routes);
    }

    private static PlayerStrategicState RestoreStrategicState(
        StrategicStateSnapshotV1 snapshot,
        StrategicMap map,
        SimulationTime currentTime
    ) =>
        snapshot.Kind switch
        {
            AtLocationKind => RestoreAtLocation(snapshot, map),
            TravelingKind => RestoreTraveling(snapshot, map, currentTime),
            _ => throw new InvalidOperationException("Strategic state kind is unknown."),
        };

    private static AtLocationState RestoreAtLocation(StrategicStateSnapshotV1 snapshot, StrategicMap map)
    {
        if (snapshot.Travel is not null)
        {
            throw new InvalidOperationException("At-location state cannot contain active travel.");
        }

        ValidateText(snapshot.LocationId, "Current location identity", MaximumIdentityLength);
        var locationId = new LocationId(snapshot.LocationId!);
        map.GetLocation(locationId);
        return new AtLocationState(locationId);
    }

    private static TravelingState RestoreTraveling(
        StrategicStateSnapshotV1 snapshot,
        StrategicMap map,
        SimulationTime currentTime
    )
    {
        if (snapshot.LocationId is not null || snapshot.Travel is null)
        {
            throw new InvalidOperationException("Traveling state requires only the explicit active travel member.");
        }

        TravelSnapshotV1 persisted = snapshot.Travel;
        ValidateText(persisted.Origin, "Travel origin", MaximumIdentityLength);
        ValidateText(persisted.Destination, "Travel destination", MaximumIdentityLength);
        var origin = new LocationId(persisted.Origin);
        var destination = new LocationId(persisted.Destination);
        SimulationTime departure = new(persisted.DepartureMilliseconds);
        SimulationTime arrival = new(persisted.ExpectedArrivalMilliseconds);
        EnsureFixedStep(departure.Milliseconds, "Travel departure");
        EnsureFixedStep(arrival.Milliseconds, "Travel arrival");
        if (currentTime.Milliseconds < departure.Milliseconds || currentTime.Milliseconds >= arrival.Milliseconds)
        {
            throw new InvalidOperationException("Active travel does not contain the current simulation time.");
        }

        StrategicRoute route =
            map.FindRoute(origin, destination)
            ?? throw new InvalidOperationException("Active travel does not follow a map route.");
        if (arrival.Milliseconds - departure.Milliseconds != route.Duration.Milliseconds)
        {
            throw new InvalidOperationException("Active travel duration does not match its strategic route.");
        }

        return new TravelingState(
            new TravelState(origin, destination, departure, arrival, new ScheduledWorkId(persisted.ScheduledArrivalId))
        );
    }

    private static PlayerShipState RestorePlayerShip(PlayerShipSnapshotV1 snapshot, SimulationTime currentTime)
    {
        if (snapshot.TacticalPosition is null || snapshot.TacticalMotion is null)
        {
            throw new InvalidOperationException("Player tactical state is required.");
        }

        ValidateText(snapshot.DefinitionId, "Ship definition identity", MaximumIdentityLength);
        SensorRepairState? repair = RestoreSensorRepair(snapshot.SensorRepair, currentTime);
        var integrity = new SensorIntegrity(snapshot.SensorIntegrity);
        if (repair is not null && integrity != repair.IntegrityAt(currentTime))
        {
            throw new InvalidOperationException(
                "Sensor integrity does not match the active repair at the current time."
            );
        }

        return new PlayerShipState(
            new ShipInstanceId(snapshot.InstanceId),
            new ShipDefinitionId(snapshot.DefinitionId),
            new TacticalPosition(snapshot.TacticalPosition.XKilometers, snapshot.TacticalPosition.YKilometers),
            new TacticalMotion(
                new HeadingDegrees(snapshot.TacticalMotion.HeadingDegrees),
                new SpeedKilometersPerSecond(snapshot.TacticalMotion.SpeedKilometersPerSecond)
            ),
            integrity,
            repair
        );
    }

    private static SensorRepairState? RestoreSensorRepair(SensorRepairSnapshotV1? snapshot, SimulationTime currentTime)
    {
        if (snapshot is null)
        {
            return null;
        }

        SimulationTime startedAt = new(snapshot.StartedAtMilliseconds);
        SimulationTime completion = new(snapshot.ExpectedCompletionMilliseconds);
        EnsureFixedStep(startedAt.Milliseconds, "Sensor repair start");
        EnsureFixedStep(completion.Milliseconds, "Sensor repair completion");
        if (currentTime.Milliseconds < startedAt.Milliseconds || currentTime.Milliseconds >= completion.Milliseconds)
        {
            throw new InvalidOperationException("Active sensor repair does not contain the current simulation time.");
        }

        return new SensorRepairState(
            new SensorIntegrity(snapshot.StartingIntegrity),
            new SensorIntegrity(snapshot.TargetIntegrity),
            startedAt,
            completion,
            new ScheduledWorkId(snapshot.ScheduledCompletionId)
        );
    }

    private static void ValidateOutstandingTravel(SimulationScheduler scheduler, PlayerStrategicState strategicState)
    {
        ScheduledWork[] arrivals =
        [
            .. scheduler.OutstandingWork.Where(work => work.Kind == ScheduledWorkKind.TravelArrival),
        ];
        if (strategicState is TravelingState traveling)
        {
            if (
                arrivals.Length != 1
                || arrivals[0].Id != traveling.Travel.ScheduledArrivalId
                || arrivals[0].DueTime != traveling.Travel.ExpectedArrival
            )
            {
                throw new InvalidOperationException(
                    "Active travel must have exactly one correlated scheduled arrival."
                );
            }
        }
        else if (arrivals.Length != 0)
        {
            throw new InvalidOperationException("Scheduled arrival work has no correlated active travel.");
        }
    }

    private static void ValidateOutstandingRepair(SimulationScheduler scheduler, SensorRepairState? repair)
    {
        ScheduledWork[] repairs =
        [
            .. scheduler.OutstandingWork.Where(work => work.Kind == ScheduledWorkKind.SensorRepairCompletion),
        ];
        if (repair is not null)
        {
            if (
                repairs.Length != 1
                || repairs[0].Id != repair.ScheduledCompletionId
                || repairs[0].DueTime != repair.ExpectedCompletion
            )
            {
                throw new InvalidOperationException(
                    "Active sensor repair must have exactly one correlated scheduled completion."
                );
            }
        }
        else if (repairs.Length != 0)
        {
            throw new InvalidOperationException("Scheduled sensor completion has no correlated active repair.");
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
