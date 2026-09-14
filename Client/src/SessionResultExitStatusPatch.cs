using EFT;
using EFT.UI.SessionEnd;
using HarmonyLib;

namespace ExfilRewards.Client;

/// <summary>
///     Postfix on SessionResultExitStatus.Show(ExitStatusScreenController), the real
///     end-of-raid results screen (verified in SPT-4.1.4-AssemblyCSharp-Dump). Fires
///     once the game has already decided and is displaying the exit status, so
///     controller.ExitStatus / PlayerSide / RaidTime / ActiveProfile are all populated.
/// </summary>
[HarmonyPatch(typeof(SessionResultExitStatus), nameof(SessionResultExitStatus.Show), typeof(SessionResultExitStatus.ExitStatusScreenController))]
public static class SessionResultExitStatusPatch
{
    [HarmonyPostfix]
    public static void Postfix(SessionResultExitStatus.ExitStatusScreenController controller)
    {
        try
        {
            bool isRunThrough = controller.ExitStatus == ExitStatus.Runner;

            if (controller.ExitStatus != ExitStatus.Survived && !isRunThrough)
            {
                // Per spec: only a confirmed successful extraction (Survived) or a
                // run-through (Runner, at half extraction base) grants a reward.
                // Left / Killed / MissingInAction / Transit all get nothing.
                return;
            }

            var activeProfile = controller.ActiveProfile;

            // Scav run detection verified against SessionResultExitStatus.Show:
            // it maps side to ESideType.Savage exactly when activeProfile.Side == EPlayerSide.Savage.
            bool isScav = activeProfile.Side == EPlayerSide.Savage;

            int killCount = activeProfile.EftStats?.Victims?.Count ?? 0;
            long survivedSeconds = (long)controller.RaidTime.TotalSeconds;

            RaidRewardPopup.ShowFor(isScav ? "scav" : "pmc", killCount, survivedSeconds, isRunThrough);
        }
        catch (Exception ex)
        {
            BepInEx.Logging.Logger.CreateLogSource(ExfilRewardsPlugin.PluginName)
                .LogError($"Failed to process raid-end reward check: {ex}");
        }
    }
}
