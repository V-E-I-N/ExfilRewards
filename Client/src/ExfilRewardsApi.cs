using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ExfilRewards.Client;

/// <summary>
///     Talks to the server-side ExfilRewardsRouter routes.
///
///     HISTORY (kept for context, not because it's still relevant code):
///     v1.0.1-1.0.4 hand-rolled a raw HttpClient because of an unverified
///     claim that SPT's server auto-zlib-decompresses POST/PUT bodies unless
///     a "requestcompressed: 0" header is set. That claim was never actually
///     confirmed against the server (SptHttpListener.HandleAsync was never
///     decompiled in this session). Meanwhile the raw-HttpClient approach had
///     a real, confirmed bug: it resolved the backend URL from
///     AppEnvironment.BackendUrl / reflection into RequestHandler, both of
///     which returned "https://prod.escapefromtarkov.com" instead of the
///     local server - confirmed via in-game diagnostic logging showing every
///     /calculate and /claim call hitting real Tarkov prod and getting back
///     an HTTP 200 with {"http code":"403","message":"Access denied"}.
///
///     GROUND TRUTH (ILSpy decompile of the actual loaded spt-common.dll,
///     Z:\SPT\BepInEx\plugins\spt\spt-common.dll, pasted by the user):
///     RequestHandler.Host is parsed directly from this process's own
///     command-line args (a "-config=...BackendUrl..." argument) in
///     RequestHandler's static constructor - not from AppEnvironment or
///     EFTBackendSettings, and not cacheable-stale the way our old field was,
///     since it's SPT's own client launcher that supplies it. This is the
///     same Host/HttpClient every other working SPT client mod uses.
///     RequestHandler.PostJsonAsync(path, json) is a thin wrapper: UTF8-
///     encodes the JSON, calls HttpClient.PostAsync(path, bytes), UTF8-
///     decodes the response. No special headers, no visible compression step
///     at this layer. Since every other mod's plain PostJsonAsync call works
///     against the real server, the original "must avoid RequestHandler"
///     premise is very likely wrong - if the server truly auto-decompressed
///     bodies, no mod using this exact method would work.
///
///     FINAL CONFIRMED BUG (v1.0.6 diagnostic log + in-game test): Host
///     resolution was correct (https://127.0.0.1:6969), /calculate and
///     /claim both reached the server and it granted the reward correctly -
///     confirmed via real mail delivery in-game. The remaining bug was
///     purely client-side: the server wraps responses in SPT's standard
///     {"err":0,"errmsg":null,"data":{...}} envelope, and the old code
///     deserialized RewardBreakdownDto directly against that envelope
///     instead of unwrapping .data first, so every field (Granted included)
///     silently defaulted. Fixed via SptResponseEnvelope<T> below - see
///     Dtos.cs for full detail.
///
///     Diagnostic logging from v1.0.4-1.0.6 removed now that root cause is
///     confirmed and fixed (its job is done, per project diagnostic-logging
///     policy).
/// </summary>
public static class ExfilRewardsApi
{
    // Server route paths - unchanged. These must match the routes the
    // server-side mod actually registers; renaming the client class doesn't
    // rename the server's HTTP endpoints.
    private const string CalculateUrl = "/exfilrewards/calculate";
    private const string ClaimUrl = "/exfilrewards/claim";

    public static async Task<RewardBreakdownDto?> CalculateAsync(CalculateRewardRequestDto request)
    {
        string responseJson = await PostAsync(CalculateUrl, request);
        var envelope = JsonConvert.DeserializeObject<SptResponseEnvelope<RewardBreakdownDto>>(responseJson);
        return envelope?.Data;
    }

    public static async Task<RewardBreakdownDto?> ClaimAsync(CalculateRewardRequestDto request)
    {
        string responseJson = await PostAsync(ClaimUrl, request);
        var envelope = JsonConvert.DeserializeObject<SptResponseEnvelope<RewardBreakdownDto>>(responseJson);
        return envelope?.Data;
    }

    private static async Task<string> PostAsync(string path, CalculateRewardRequestDto request)
    {
        string json = JsonConvert.SerializeObject(request);
        return await SPT.Common.Http.RequestHandler.PostJsonAsync(path, json);
    }
}
