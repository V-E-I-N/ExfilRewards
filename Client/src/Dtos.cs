using Newtonsoft.Json;

namespace ExfilRewards.Client;

public class CalculateRewardRequestDto
{
    [JsonProperty("side")]
    public string Side = "pmc";

    [JsonProperty("killCount")]
    public int KillCount;

    [JsonProperty("survivedSeconds")]
    public long SurvivedSeconds;

    [JsonProperty("isRunThrough")]
    public bool IsRunThrough;
}

public class CurrencyAmountsDto
{
    [JsonProperty("roubles")]
    public long Roubles;

    [JsonProperty("dollars")]
    public long Dollars;

    [JsonProperty("euros")]
    public long Euros;
}

public class RewardBreakdownDto
{
    [JsonProperty("extractionBonus")]
    public CurrencyAmountsDto ExtractionBonus = new();

    [JsonProperty("killBonus")]
    public CurrencyAmountsDto KillBonus = new();

    [JsonProperty("total")]
    public CurrencyAmountsDto Total = new();

    [JsonProperty("killCount")]
    public int KillCount;

    [JsonProperty("survivedSeconds")]
    public long SurvivedSeconds;

    [JsonProperty("granted")]
    public bool Granted;
}

/// <summary>
///     The server wraps every route response in the standard SPT envelope:
///     {"err":0,"errmsg":null,"data":{...actual payload...}}. Confirmed
///     against the real LogOutput.log response bodies for both /calculate
///     and /claim - the previous code deserialized RewardBreakdownDto
///     directly against this envelope, so every field silently defaulted
///     to zero/false (Granted included), which is why the popup always
///     showed ₽0 and treated successful claims as errors even though the
///     server was granting the reward correctly the whole time.
/// </summary>
public class SptResponseEnvelope<T>
{
    [JsonProperty("err")]
    public int Err;

    [JsonProperty("errmsg")]
    public string? ErrMsg;

    [JsonProperty("data")]
    public T? Data;
}
