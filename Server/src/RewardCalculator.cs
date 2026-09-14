namespace ExfilRewards.Server;

public enum RaidSide
{
    Pmc,
    Scav
}

public readonly record struct CurrencyAmounts(long Roubles, long Dollars, long Euros)
{
    public static CurrencyAmounts operator +(CurrencyAmounts a, CurrencyAmounts b) =>
        new(a.Roubles + b.Roubles, a.Dollars + b.Dollars, a.Euros + b.Euros);
}

public readonly record struct RewardBreakdown(
    CurrencyAmounts ExtractionBonus,
    CurrencyAmounts KillBonus,
    CurrencyAmounts Total,
    int KillCount,
    long SurvivedSeconds
);

/// <summary>
///     Pure reward math. No persistence, no mail, no profile access -
///     just numbers in, numbers out, so it can be called identically
///     by both /calculate (preview) and /claim (real grant).
/// </summary>
public static class RewardCalculator
{
    // PMC base extraction reward per currency.
    private static readonly CurrencyAmounts PmcExtractBase = new(100_000, 800, 600);

    // Scav base extraction reward per currency.
    private static readonly CurrencyAmounts ScavExtractBase = new(55_000, 400, 275);

    // PMC flat per-kill reward per currency.
    private static readonly CurrencyAmounts PmcKillBonus = new(25_000, 200, 150);

    // Scav flat per-kill reward per currency.
    private static readonly CurrencyAmounts ScavKillBonus = new(12_000, 150, 100);

    // +0.5% of the base extraction reward per minute survived, linear, uncapped.
    private const double PercentPerMinute = 0.005;

    // Run-through (Runner exit) gets half the extraction base, before time scaling.
    // Kill bonus and the time-scaling multiplier are unaffected.
    private const double RunThroughBaseMultiplier = 0.5;

    public static RewardBreakdown Calculate(RaidSide side, int killCount, long survivedSeconds, bool isRunThrough = false)
    {
        if (killCount < 0)
        {
            killCount = 0;
        }
        if (survivedSeconds < 0)
        {
            survivedSeconds = 0;
        }

        var extractBase = side == RaidSide.Pmc ? PmcExtractBase : ScavExtractBase;
        var perKill = side == RaidSide.Pmc ? PmcKillBonus : ScavKillBonus;

        if (isRunThrough)
        {
            extractBase = new CurrencyAmounts(
                (long)Math.Round(extractBase.Roubles * RunThroughBaseMultiplier),
                (long)Math.Round(extractBase.Dollars * RunThroughBaseMultiplier),
                (long)Math.Round(extractBase.Euros * RunThroughBaseMultiplier)
            );
        }

        double survivedMinutes = survivedSeconds / 60.0;
        double multiplier = 1.0 + (PercentPerMinute * survivedMinutes);

        var extractionBonus = new CurrencyAmounts(
            (long)Math.Round(extractBase.Roubles * multiplier),
            (long)Math.Round(extractBase.Dollars * multiplier),
            (long)Math.Round(extractBase.Euros * multiplier)
        );

        var killBonus = new CurrencyAmounts(
            perKill.Roubles * killCount,
            perKill.Dollars * killCount,
            perKill.Euros * killCount
        );

        var total = extractionBonus + killBonus;

        return new RewardBreakdown(extractionBonus, killBonus, total, killCount, survivedSeconds);
    }
}
