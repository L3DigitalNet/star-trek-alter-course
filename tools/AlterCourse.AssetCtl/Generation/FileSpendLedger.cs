using System.Globalization;
using System.Text.Json;
using StateFile = AlterCourse.AssetCtl.Publishing.PublishingTypes.StateFile;

namespace AlterCourse.AssetCtl.Generation;

internal sealed class FileSpendLedger : ISpendLedger
{
    private readonly string _ledgerPath;
    private readonly string _lockPath;

    public FileSpendLedger(EffectiveConfiguration configuration)
    {
        string stateRoot = PathPolicy.ResolveUnder(
            configuration.RepositoryRoot,
            configuration.Paths.StateRoot,
            "state_root",
            allowMissing: true
        );
        Directory.CreateDirectory(stateRoot);
        _ledgerPath = Path.Combine(stateRoot, "daily-spend.json");
        _lockPath = Path.Combine(stateRoot, "daily-spend.lock");
    }

    public void Reserve(DateOnly date, decimal amount, decimal dailyLimit)
    {
        using FileStream heldLock = AcquireLock();
        Dictionary<string, decimal> totals = ReadTotals();
        string key = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        decimal next = totals.GetValueOrDefault(key) + amount;
        if (next > dailyLimit)
        {
            throw new AssetCtlException("local daily spending limit would be exceeded.", 6);
        }

        totals[key] = next;
        string stage = _ledgerPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(stage, JsonSerializer.Serialize(totals, JsonOptions.Stable));
            File.Move(stage, _ledgerPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(stage))
            {
                File.Delete(stage);
            }
        }
    }

    private FileStream AcquireLock()
    {
        try
        {
            string directory =
                Path.GetDirectoryName(_lockPath)
                ?? throw new AssetCtlException("daily spending ledger lock directory is invalid.", 7);
            return StateFile.OpenLockedLeaf(directory, Path.GetFileName(_lockPath), "daily spending ledger lock");
        }
        catch (IOException)
        {
            throw new AssetCtlException("daily spending ledger is locked by another process.", 7);
        }
    }

    private Dictionary<string, decimal> ReadTotals()
    {
        if (!File.Exists(_ledgerPath))
        {
            return new Dictionary<string, decimal>(StringComparer.Ordinal);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, decimal>>(File.ReadAllText(_ledgerPath))
                ?? throw new JsonException("ledger root was null");
        }
        catch (JsonException exception)
        {
            throw new AssetCtlException($"daily spending ledger is invalid: {exception.Message}", 7);
        }
    }
}
