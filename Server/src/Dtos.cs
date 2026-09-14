using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Utils;

namespace ExfilRewards.Server;

/// <summary>
///     Sent by the client mod after a confirmed-survived raid end.
///     Side/kills/time come from the client, since the client is the
///     only side that unambiguously knows what raid it just played.
///     The server independently recomputes the reward from these raw
///     facts - it never trusts a client-supplied currency amount.
/// </summary>
public record CalculateRewardRequest : IRequestData
{
    [JsonPropertyName("side")]
    public string? Side { get; set; }

    [JsonPropertyName("killCount")]
    public int KillCount { get; set; }

    [JsonPropertyName("survivedSeconds")]
    public long SurvivedSeconds { get; set; }

    /// <summary>
    ///     True when the client's exit status was Runner (run-through), not Survived.
    ///     A raw fact from the client, same trust level as side/killCount/survivedSeconds -
    ///     the server still independently computes the currency amount from it, it never
    ///     accepts a client-supplied total.
    /// </summary>
    [JsonPropertyName("isRunThrough")]
    public bool IsRunThrough { get; set; }
}

public record CurrencyAmountsDto
{
    [JsonPropertyName("roubles")]
    public long Roubles { get; set; }

    [JsonPropertyName("dollars")]
    public long Dollars { get; set; }

    [JsonPropertyName("euros")]
    public long Euros { get; set; }

    public static CurrencyAmountsDto From(CurrencyAmounts amounts) => new()
    {
        Roubles = amounts.Roubles,
        Dollars = amounts.Dollars,
        Euros = amounts.Euros
    };
}

public record RewardBreakdownResponse
{
    [JsonPropertyName("extractionBonus")]
    public CurrencyAmountsDto ExtractionBonus { get; set; } = new();

    [JsonPropertyName("killBonus")]
    public CurrencyAmountsDto KillBonus { get; set; } = new();

    [JsonPropertyName("total")]
    public CurrencyAmountsDto Total { get; set; } = new();

    [JsonPropertyName("killCount")]
    public int KillCount { get; set; }

    [JsonPropertyName("survivedSeconds")]
    public long SurvivedSeconds { get; set; }

    [JsonPropertyName("granted")]
    public bool Granted { get; set; }

    public static RewardBreakdownResponse From(RewardBreakdown breakdown, bool granted) => new()
    {
        ExtractionBonus = CurrencyAmountsDto.From(breakdown.ExtractionBonus),
        KillBonus = CurrencyAmountsDto.From(breakdown.KillBonus),
        Total = CurrencyAmountsDto.From(breakdown.Total),
        KillCount = breakdown.KillCount,
        SurvivedSeconds = breakdown.SurvivedSeconds,
        Granted = granted
    };
}
