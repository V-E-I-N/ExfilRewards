using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Utils;

namespace ExfilRewards.Server;

/// <summary>
///     Two client-facing routes:
///     - /exfilrewards/calculate: preview only, no grant, used to populate the popup.
///     - /exfilrewards/claim: recomputes independently from the same raw facts and
///       actually mails the currency. Only called when the player clicks Confirm.
///     Registered the same way core routers are (verified against MatchStaticRouter /
///     BuildStaticRouter in the dump): a StaticRouter subclass passing RouteAction[] to
///     the base constructor, auto-discovered via [Injectable].
/// </summary>
[Injectable(InjectionType.Transient, int.MaxValue)]
public class ExfilRewardsRouter : StaticRouter
{
    public ExfilRewardsRouter(JsonUtil jsonUtil, HttpResponseUtil httpResponseUtil, RewardGranter rewardGranter)
        : base(jsonUtil, new List<RouteAction>
        {
            new RouteAction<CalculateRewardRequest>(
                "/exfilrewards/calculate",
                async (url, request, sessionId, output, cancellationToken) =>
                    await HandleCalculateAsync(request, httpResponseUtil)),

            new RouteAction<CalculateRewardRequest>(
                "/exfilrewards/claim",
                async (url, request, sessionId, output, cancellationToken) =>
                    await HandleClaimAsync(request, sessionId, httpResponseUtil, rewardGranter))
        })
    {
    }

    private static Task<string> HandleCalculateAsync(CalculateRewardRequest request, HttpResponseUtil httpResponseUtil)
    {
        var breakdown = CalculateFromRequest(request);
        var response = RewardBreakdownResponse.From(breakdown, granted: false);
        return Task.FromResult(httpResponseUtil.GetBody(response));
    }

    private static Task<string> HandleClaimAsync(
        CalculateRewardRequest request,
        MongoId sessionId,
        HttpResponseUtil httpResponseUtil,
        RewardGranter rewardGranter)
    {
        var breakdown = CalculateFromRequest(request);
        rewardGranter.Grant(sessionId, breakdown);
        var response = RewardBreakdownResponse.From(breakdown, granted: true);
        return Task.FromResult(httpResponseUtil.GetBody(response));
    }

    private static RewardBreakdown CalculateFromRequest(CalculateRewardRequest request)
    {
        var side = string.Equals(request.Side, "scav", StringComparison.OrdinalIgnoreCase)
            ? RaidSide.Scav
            : RaidSide.Pmc;

        return RewardCalculator.Calculate(side, request.KillCount, request.SurvivedSeconds, request.IsRunThrough);
    }
}
