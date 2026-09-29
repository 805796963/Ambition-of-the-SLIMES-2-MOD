// This plugin was written by an AI assistant (DeepSeek). The author set the requirements, made the
// design calls, tested every change in the game and owns the release. See AI-DISCLOSURE.md.
using System;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace SlimesBattle
{
    /// <summary>
    /// 老牌战斗 / Old School Battle for Ambition of the SLIMES 2.
    ///
    /// Multiplies the damage every unit deals and takes. Both sides use the same calculation, so one
    /// multiplier covers damage given and damage received; the game's flat defence subtraction is not
    /// scaled, which is what makes fights resolve faster and hit harder.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class BattleTweaksPlugin : BasePlugin
    {
        public const string PluginGuid = "dsh.ambition.slimes2.battletweaks";
        public const string PluginName = "Ambition of the SLIMES 2 - Old School Battle (老牌战斗)";
        public const string PluginVersion = "1.0.0";

        internal static BattleTweaksPlugin Instance;

        internal static ConfigEntry<bool> CfgEnabled;
        internal static ConfigEntry<bool> CfgVerboseLog;
        internal static ConfigEntry<float> CfgMultiplier;

        public override void Load()
        {
            Instance = this;

            CfgEnabled = Config.Bind("General", "Enabled", true,
                "Master switch. When false every patch defers to the original game logic.");
            CfgVerboseLog = Config.Bind("General", "VerboseLog", false,
                "Log every damage value the patch changes (debugging).");
            CfgMultiplier = Config.Bind("Damage", "Multiplier", 2f,
                "Damage is multiplied by this. 2 doubles both the damage a unit deals and the damage it"
              + " takes, since the game uses one calculation for both.");

            var harmony = new Harmony(PluginGuid);
            harmony.PatchAll();

            var patched = harmony.GetPatchedMethods().ToList();
            Log.LogInfo($"Harmony patched method count: {patched.Count}");
            foreach (var method in patched)
                Log.LogInfo($"  patched -> {method.DeclaringType?.FullName}.{method.Name}");

            if (patched.Count == 0) Log.LogError("NO METHOD WAS PATCHED - the plugin has no effect.");
            else Log.LogInfo($"BattleTweaks {PluginVersion} active: Enabled={CfgEnabled.Value},"
                           + $" Multiplier={CfgMultiplier.Value}");
        }

        internal static void Warn(string message) => Instance?.Log.LogWarning(message);

        internal static void Verbose(string message)
        {
            if (CfgVerboseLog != null && CfgVerboseLog.Value) Instance?.Log.LogInfo(message);
        }
    }
}
