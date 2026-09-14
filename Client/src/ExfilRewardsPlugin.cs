using BepInEx;
using HarmonyLib;

namespace ExfilRewards.Client;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class ExfilRewardsPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.froze.exfilrewards";
    public const string PluginName = "Exfil Rewards";
    public const string PluginVersion = "1.0.0";

    private Harmony? _harmony;

    private void Awake()
    {
        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll(typeof(ExfilRewardsPlugin).Assembly);

        Logger.LogInfo($"{PluginName} v{PluginVersion} loaded.");
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
    }
}
