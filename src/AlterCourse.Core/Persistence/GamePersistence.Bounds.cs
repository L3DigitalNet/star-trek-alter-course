using System.Buffers;
using System.Text;
using System.Text.Json;
using AlterCourse.Core.Content;
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
    /// <summary>Checks existing collection and string ceilings before materializing untrusted JSON values.</summary>
    internal static void ValidateInputBounds(ReadOnlySpan<byte> json, string sourceIdentity)
    {
        var reader = new Utf8JsonReader(json, new JsonReaderOptions { MaxDepth = MaximumJsonDepth });
        if (!reader.Read())
            throw new JsonException("The save root must be an object.");
        ReadBoundedValue(ref reader, int.MaxValue, int.MaxValue, "array", sourceIdentity);
        if (reader.Read())
            throw new JsonException("The save contains data after its root value.");
    }

    private static void ReadBoundedValue(
        ref Utf8JsonReader reader,
        int maximum,
        int maximumText,
        string label,
        string sourceIdentity
    )
    {
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                (int childMaximum, string childLabel) = CollectionBound(ref reader);
                (int childTextMaximum, string textLabel) = StringBound(ref reader);
                if (!reader.Read())
                    throw new JsonException("A save member has no value.");
                ReadBoundedValue(
                    ref reader,
                    childMaximum,
                    childTextMaximum,
                    childTextMaximum == int.MaxValue ? childLabel : textLabel,
                    sourceIdentity
                );
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
                ReadBoundedValue(ref reader, int.MaxValue, maximumText, label, sourceIdentity);
            }
            if (reader.TokenType != JsonTokenType.EndArray)
                throw new JsonException("A save array is incomplete.");
        }
        else if (reader.TokenType == JsonTokenType.String && maximumText != int.MaxValue)
        {
            ValidateStringToken(ref reader, maximumText, label, sourceIdentity);
        }
    }

    private static void ValidateStringToken(ref Utf8JsonReader reader, int maximum, string label, string sourceIdentity)
    {
        ReadOnlySpan<byte> encoded = reader.ValueSpan;
        int length = 0;
        for (int offset = 0; offset < encoded.Length; )
        {
            if (encoded[offset] == (byte)'\\')
            {
                // A Unicode escape denotes one UTF-16 code unit, including each half of a
                // surrogate pair. This matches the existing string.Length validators exactly.
                offset += encoded[offset + 1] == (byte)'u' ? 6 : 2;
                length++;
            }
            else if (encoded[offset] < 0x80)
            {
                offset++;
                length++;
            }
            else
            {
                if (Rune.DecodeFromUtf8(encoded[offset..], out Rune rune, out int consumed) != OperationStatus.Done)
                    throw new JsonException("A save string contains invalid UTF-8.");
                offset += consumed;
                length += rune.Utf16SequenceLength;
            }
            if (length > maximum)
                throw Failure(
                    GamePersistenceFailure.InvalidData,
                    sourceIdentity,
                    $"{label} exceeds the {maximum}-character string limit."
                );
        }
    }

    // Shared wire names use the largest existing ceiling of their supported contexts. Exact
    // ship/system/metadata limits remain in semantic validation; admission must not narrow saves.
    private static (int Maximum, string Label) StringBound(ref Utf8JsonReader reader)
    {
        if (reader.ValueTextEquals("saveId"u8))
            return (MaximumMetadataTextLength, "saveId");
        if (reader.ValueTextEquals("displayName"u8))
            return (
                Math.Max(
                    MaximumMetadataTextLength,
                    Math.Max(ShipState.MaximumVesselDisplayNameLength, StrategicLocation.MaximumDisplayNameLength)
                ),
                "displayName"
            );
        if (reader.ValueTextEquals("definitionId"u8))
            return (
                Math.Max(
                    ShipDefinitionId.MaximumLength,
                    Math.Max(FactionDefinitionId.MaximumLength, SystemDefinitionId.MaximumLength)
                ),
                "definitionId"
            );
        if (reader.ValueTextEquals("id"u8))
            return (LocationId.MaximumLength, "id");
        if (reader.ValueTextEquals("locationId"u8))
            return (LocationId.MaximumLength, "locationId");
        if (reader.ValueTextEquals("observedAtLocationId"u8))
            return (LocationId.MaximumLength, "observedAtLocationId");
        if (reader.ValueTextEquals("targetLocationId"u8))
            return (LocationId.MaximumLength, "targetLocationId");
        if (reader.ValueTextEquals("origin"u8))
            return (LocationId.MaximumLength, "origin");
        if (reader.ValueTextEquals("destination"u8))
            return (LocationId.MaximumLength, "destination");
        if (reader.ValueTextEquals("originLocationId"u8))
            return (LocationId.MaximumLength, "originLocationId");
        if (reader.ValueTextEquals("destinationLocationId"u8))
            return (LocationId.MaximumLength, "destinationLocationId");
        if (reader.ValueTextEquals("waypoints"u8))
            return (LocationId.MaximumLength, "waypoints");
        if (reader.ValueTextEquals("knownVesselDisplayName"u8))
            return (ShipState.MaximumVesselDisplayNameLength, "knownVesselDisplayName");
        if (reader.ValueTextEquals("knownDesignDisplayName"u8))
            return (ShipDefinition.MaximumDesignDisplayNameLength, "knownDesignDisplayName");
        if (reader.ValueTextEquals("semantics"u8))
            return (SystemDefinitionSemantics.MaximumLength, "semantics");
        if (reader.ValueTextEquals("aimVocabulary"u8))
            return (AimVocabularySemantics.MaximumLength, "aimVocabulary");
        return (int.MaxValue, "string");
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
