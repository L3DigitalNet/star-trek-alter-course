using System.Text.Json;
using AlterCourse.Core.Factions;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Orders;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Persistence;

public static partial class GamePersistence
{
    /// <summary>Checks existing collection ceilings without materializing untrusted JSON arrays or strings.</summary>
    internal static void ValidateCollectionBounds(ReadOnlySpan<byte> json, string sourceIdentity)
    {
        var reader = new Utf8JsonReader(json, new JsonReaderOptions { MaxDepth = MaximumJsonDepth });
        if (!reader.Read())
            throw new JsonException("The save root must be an object.");
        ReadBoundedValue(ref reader, int.MaxValue, "array", sourceIdentity);
        if (reader.Read())
            throw new JsonException("The save contains data after its root value.");
    }

    private static void ReadBoundedValue(ref Utf8JsonReader reader, int maximum, string label, string sourceIdentity)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                (int childMaximum, string childLabel) = CollectionBound(ref reader);
                if (!reader.Read())
                    throw new JsonException("A save member has no value.");
                ReadBoundedValue(ref reader, childMaximum, childLabel, sourceIdentity);
            }
            if (reader.TokenType != JsonTokenType.EndObject)
                throw new JsonException("A save object is incomplete.");
        }
        else if (reader.TokenType == JsonTokenType.StartArray)
        {
            int count = 0;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                if (++count > maximum)
                    throw Failure(
                        GamePersistenceFailure.InvalidData,
                        sourceIdentity,
                        $"{label} exceeds the {maximum}-entry collection limit."
                    );
                ReadBoundedValue(ref reader, int.MaxValue, "array", sourceIdentity);
            }
            if (reader.TokenType != JsonTokenType.EndArray)
                throw new JsonException("A save array is incomplete.");
        }
    }

    // Property tokens are compared in place: allocating a property-name string here would itself
    // allow an untrusted name to allocate memory before the DTO's closed shape can reject it.
    private static (int Maximum, string Label) CollectionBound(ref Utf8JsonReader reader)
    {
        if (reader.ValueTextEquals("ships"u8))
            return (SimulationState.MaximumShips, "ships");
        if (reader.ValueTextEquals("factions"u8))
            return (SimulationState.MaximumFactions, "factions");
        if (reader.ValueTextEquals("locations"u8))
            return (StrategicMap.MaximumLocations, "strategic locations");
        if (reader.ValueTextEquals("routes"u8))
            return (StrategicMap.MaximumRoutes, "strategic routes");
        if (reader.ValueTextEquals("outstandingWork"u8))
            return (SimulationScheduler.MaximumOutstandingWork, "scheduler work");
        if (reader.ValueTextEquals("contacts"u8))
            return (SensorKnowledge.MaximumContactsPerObserver, "contacts");
        if (reader.ValueTextEquals("waypoints"u8))
            return (PatrolRouteOrder.MaximumWaypointCount, "patrol waypoints");
        if (reader.ValueTextEquals("inFlightReports"u8))
            return (FactionObservationState.MaximumInFlightReports, "in-flight observation reports");
        if (reader.ValueTextEquals("receivedReports"u8))
            return (FactionObservationState.MaximumReceivedReports, "received observation reports");
        if (reader.ValueTextEquals("completionWatermarks"u8))
            return (FactionObservationState.MaximumCompletionWatermarks, "observation completion watermarks");
        if (reader.ValueTextEquals("systemDefinitions"u8))
            return (MaximumSystemDefinitionReferences, "system definition references");
        if (reader.ValueTextEquals("installedSystems"u8))
            return (ShipSystemLimits.MaximumInstalledSystemsPerShip, "installed systems per ship");
        if (reader.ValueTextEquals("directedEnergyReadiness"u8))
            return (ShipSystemLimits.MaximumInstalledSystemsPerShip, "weapon readiness entries per ship");
        return (int.MaxValue, "array");
    }
}
