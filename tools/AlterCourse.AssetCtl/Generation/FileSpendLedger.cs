using System.Globalization;
using System.Text.Json;

namespace AlterCourse.AssetCtl.Generation;

internal sealed class FileSpendLedger : ISpendLedger
{
    private readonly string ledgerPath;
    private readonly string lockPath;

    public FileSpendLedger(EffectiveConfiguration configuration)
    {
        string stateRoot = PathPolicy.ResolveUnder(
            configuration.RepositoryRoot,
            configuration.Paths.StateRoot,
            "state_root",
            allowMissing: true
        );
        Directory.CreateDirectory(stateRoot);
        ledgerPath = Path.Combine(stateRoot, "daily-spend.json");
        lockPath = Path.Combine(stateRoot, "daily-spend.lock");
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
        string stage = ledgerPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(stage, JsonSerializer.Serialize(totals, JsonOptions.Stable));
            File.Move(stage, ledgerPath, overwrite: true);
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
            return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            throw new AssetCtlException("daily spending ledger is locked by another process.", 7);
        }
    }

    private Dictionary<string, decimal> ReadTotals()
    {
        if (!File.Exists(ledgerPath))
        {
            return new Dictionary<string, decimal>(StringComparer.Ordinal);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, decimal>>(File.ReadAllText(ledgerPath))
                ?? throw new JsonException("ledger root was null");
        }
        catch (JsonException exception)
        {
            throw new AssetCtlException($"daily spending ledger is invalid: {exception.Message}", 7);
        }
    }
}
