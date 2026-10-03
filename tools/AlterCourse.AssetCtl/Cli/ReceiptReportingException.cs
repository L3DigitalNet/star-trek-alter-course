namespace AlterCourse.AssetCtl.Cli;

/// <summary>Identifies an independent receipt sink failure after authoritative publication returned.</summary>
internal sealed class ReceiptReportingException() : Exception("Receipt output is unavailable after publication.");
