using System.Collections.Immutable;

namespace AlterCourse.Core.Factions;

/// <summary>Contains one faction's bounded authoritative observation-response state.</summary>
internal sealed record FactionObservationState
{
    internal const int MaximumInFlightReports = 8;
    internal const int MaximumReceivedReports = 16;
    internal const int MaximumActiveInvestigations = 1;
    internal const int MaximumCompletionWatermarks = MaximumInFlightReports + MaximumReceivedReports;
    internal const long ReportFreshnessMilliseconds = 60_000;
    internal const long ReportDeliveryDelayMilliseconds = 2_000;

    internal FactionObservationState(
        ObservationResponsePosture posture = ObservationResponsePosture.Disabled,
        IEnumerable<ObservationReportInFlight>? inFlightReports = null,
        IEnumerable<ReceivedObservationReport>? receivedReports = null,
        ActiveFactionInvestigation? activeInvestigation = null,
        IEnumerable<ObservationLocationCompletionWatermark>? completionWatermarks = null
    )
    {
        if (!Enum.IsDefined(posture))
        {
            throw new ArgumentOutOfRangeException(nameof(posture), "Observation-response posture is unsupported.");
        }

        InFlightReports =
        [
            .. Materialize(
                    inFlightReports,
                    MaximumInFlightReports,
                    nameof(inFlightReports),
                    item => item.Report.ReportId
                )
                .OrderBy(item => item.Report.ReportId.Value),
        ];
        ReceivedReports =
        [
            .. Materialize(
                    receivedReports,
                    MaximumReceivedReports,
                    nameof(receivedReports),
                    item => item.Report.ReportId
                )
                .OrderByDescending(item => item.Report.ObservedAt.Milliseconds)
                .ThenBy(item => item.Report.ReportId.Value),
        ];
        CompletionWatermarks = MaterializeWatermarks(completionWatermarks);
        if (
            InFlightReports
                .Select(item => item.Report.ReportId)
                .Intersect(ReceivedReports.Select(item => item.Report.ReportId))
                .Any()
        )
        {
            throw new ArgumentException("A report cannot be both in flight and received.", nameof(receivedReports));
        }

        Posture = posture;
        ActiveInvestigation = activeInvestigation;
    }

    internal ObservationResponsePosture Posture { get; }
    internal ImmutableArray<ObservationReportInFlight> InFlightReports { get; }
    internal ImmutableArray<ReceivedObservationReport> ReceivedReports { get; }
    internal ActiveFactionInvestigation? ActiveInvestigation { get; }
    internal ImmutableArray<ObservationLocationCompletionWatermark> CompletionWatermarks { get; }

    private static ImmutableArray<T> Materialize<T>(
        IEnumerable<T>? values,
        int maximum,
        string parameterName,
        Func<T, object> identity
    )
        where T : class
    {
        T[] materialized = (values ?? []).Take(maximum + 1).ToArray();
        if (materialized.Length > maximum || materialized.Any(value => value is null))
        {
            throw new ArgumentException(
                $"Observation state supports at most {maximum} nonnull entries.",
                parameterName
            );
        }

        if (materialized.Select(identity).Distinct().Count() != materialized.Length)
        {
            throw new ArgumentException("Observation state entries require unique report identities.", parameterName);
        }

        return [.. materialized];
    }

    private static ImmutableArray<ObservationLocationCompletionWatermark> MaterializeWatermarks(
        IEnumerable<ObservationLocationCompletionWatermark>? watermarks
    )
    {
        ObservationLocationCompletionWatermark[] materialized = (watermarks ?? [])
            .Take(MaximumCompletionWatermarks + 1)
            .ToArray();
        if (materialized.Length > MaximumCompletionWatermarks || materialized.Any(watermark => watermark is null))
        {
            throw new ArgumentException(
                $"Observation state supports at most {MaximumCompletionWatermarks} nonnull completion watermarks.",
                nameof(watermarks)
            );
        }

        if (materialized.Select(watermark => watermark.LocationId).Distinct().Count() != materialized.Length)
        {
            throw new ArgumentException("Completion watermarks require unique locations.", nameof(watermarks));
        }

        return [.. materialized.OrderBy(watermark => watermark.LocationId.Value, StringComparer.Ordinal)];
    }
}
