// This plugin was written by an AI assistant (DeepSeek). The author set the requirements, made the
// design calls, tested every change in the game and owns the release. See AI-DISCLOSURE.md.
using System;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace SlimesMimic
{
    /// <summary>
    /// 百变史莱姆 / Mimic Slime for Ambition of the SLIMES 2.
    ///
    /// Gives the zako slime a mimicry ability: it copies any unit on the field and keeps that shape for
    /// the rest of the battle. Split out of the SlimeFix plugin so the shape-shifting can be installed
    /// on its own; SlimeFix's own copy of this feature is switched off in its config.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class MimicSlimePlugin : BasePlugin
    {
        public const string PluginGuid = "dsh.ambition.slimes2.mimicslime";
        public const string PluginName = "Ambition of the SLIMES 2 - Mimic Slime (百变史莱姆)";
        public const string PluginVersion = "1.0.0";

        internal static MimicSlimePlugin Instance;

        internal static ConfigEntry<bool> CfgEnabled;
        internal static ConfigEntry<bool> CfgVerboseLog;

        internal static ConfigEntry<int> CfgSlimeType;
        internal static ConfigEntry<int> CfgShellType;
        internal static ConfigEntry<int> CfgLevelPercent;
        internal static ConfigEntry<int> CfgShieldBreak;
        internal static ConfigEntry<bool> CfgReplaceCaster;
        internal static ConfigEntry<string> CfgForbidTypes;
        internal static ConfigEntry<float> CfgTintR;
        internal static ConfigEntry<float> CfgTintG;
        internal static ConfigEntry<float> CfgTintB;

        public override void Load()
        {
            Instance = this;

            CfgEnabled = Config.Bind("General", "Enabled", true,
                "Master switch. When false every patch defers to the original game logic.");
            CfgVerboseLog = Config.Bind("General", "VerboseLog", false,
                "Log the per-use detail of the mimic (debugging).");

            CfgSlimeType = Config.Bind("Mimic", "SlimeType", 1,
                "UNIT_TYPE that may mimic. 1 is the zako slime.");
            CfgShellType = Config.Bind("Mimic", "ShellType", 15,
                "UNIT_TYPE whose ability is borrowed as the ability shell. 15 is the anti-magic slime,"
              + " whose shield breaker is what the mimic entry shows.");
            CfgLevelPercent = Config.Bind("Mimic", "LevelPercent", 70,
                "Percent of the copied unit's level the shape ends up at, so 70 means 30 percent lower."
              + " HP, attack and defence follow from that level.");
            CfgShieldBreak = Config.Bind("Mimic", "ShieldBreakPercent", 5,
                "Percent chance that using the mimic also breaks the target's shield, which is what the"
              + " borrowed shell normally does.");
            CfgReplaceCaster = Config.Bind("Mimic", "ReplaceCaster", true,
                "true removes the slime's own body once the shape is in place, so the shape replaces it;"
              + " false keeps both on the field.");
            CfgForbidTypes = Config.Bind("Mimic", "ForbidTypes", "21,22",
                "Comma separated UNIT_TYPE values the mimic refuses to copy. 21 and 22 are the split"
              + " slimes, whose copies would be able to split as well.");
            CfgTintR = Config.Bind("Mimic", "TintR", 0.25f, "Red of the tint drawn over a copied shape.");
            CfgTintG = Config.Bind("Mimic", "TintG", 1f, "Green of the tint drawn over a copied shape.");
            CfgTintB = Config.Bind("Mimic", "TintB", 0.25f, "Blue of the tint drawn over a copied shape.");

            var harmony = new Harmony(PluginGuid);
            harmony.PatchAll();

            var patched = harmony.GetPatchedMethods().ToList();
            Log.LogInfo($"Harmony patched method count: {patched.Count}");
            foreach (var method in patched)
                Log.LogInfo($"  patched -> {method.DeclaringType?.FullName}.{method.Name}");

            if (patched.Count == 0) Log.LogError("NO METHOD WAS PATCHED - the plugin has no effect.");
            else Log.LogInfo($"MimicSlime {PluginVersion} active: Enabled={CfgEnabled.Value}");
        }

        internal static void Warn(string message) => Instance?.Log.LogWarning(message);

        internal static void Verbose(string message)
        {
            if (CfgVerboseLog != null && CfgVerboseLog.Value) Instance?.Log.LogInfo(message);
        }
    }
}
