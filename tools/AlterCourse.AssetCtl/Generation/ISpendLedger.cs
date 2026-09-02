namespace AlterCourse.AssetCtl.Generation;

internal interface ISpendLedger
{
    public void Reserve(DateOnly date, decimal amount, decimal dailyLimit);
}
