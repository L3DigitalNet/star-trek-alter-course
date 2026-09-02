namespace AlterCourse.AssetCtl.Generation;

internal sealed class SpendGuard
{
    private readonly SpendingLimits _limits;
    private readonly string _unknownPricePolicy;
    private readonly ISpendLedger _ledger;
    private readonly Func<DateOnly> _today;

    public SpendGuard(EffectiveConfiguration configuration)
        : this(
            configuration.Spending,
            configuration.Policy.UnknownPricePolicy,
            new FileSpendLedger(configuration),
            () => DateOnly.FromDateTime(DateTime.UtcNow)
        ) { }

    internal SpendGuard(SpendingLimits limits, string unknownPricePolicy, ISpendLedger ledger, Func<DateOnly> today)
    {
        _limits = limits;
        _unknownPricePolicy = unknownPricePolicy;
        _ledger = ledger;
        _today = today;
    }

    public decimal TotalReservedUsd { get; private set; }

    public void Reserve(decimal? estimatedCostPerOutput, int outputs, string operation)
    {
        if (estimatedCostPerOutput is null)
        {
            if (string.Equals(_unknownPricePolicy, "reject", StringComparison.Ordinal))
            {
                throw new AssetCtlException($"{operation}: cost is unknown and policy rejects unbounded spend.", 6);
            }

            return;
        }

        if (estimatedCostPerOutput < 0 || outputs < 1)
        {
            throw new AssetCtlException($"{operation}: invalid spend estimate.", 6);
        }

        decimal amount = estimatedCostPerOutput.Value * outputs;
        decimal next = TotalReservedUsd + amount;
        if (amount > _limits.PerAssetUsd || next > _limits.PerAssetUsd || next > _limits.PerRunUsd)
        {
            throw new AssetCtlException($"{operation}: per-asset or per-run spending limit would be exceeded.", 6);
        }

        // Reserve before the request: a timeout or malformed response can still represent a provider-side billable event.
        _ledger.Reserve(_today(), amount, _limits.PerDayUsd);
        TotalReservedUsd = next;
    }
}
