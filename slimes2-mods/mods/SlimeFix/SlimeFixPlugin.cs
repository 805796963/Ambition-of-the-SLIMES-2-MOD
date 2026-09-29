// This plugin was written by an AI assistant (DeepSeek). The author set the requirements, made the
// design calls, tested every change in the game and owns the release. See AI-DISCLOSURE.md.
using System;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace SlimesSlimeFix
{
    /// <summary>
    /// Ability-count patch for Ambition of the SLIMES 2.
    ///
    /// The slime that owns the "inherit" passive (ABILITY_TYPE.ocuupy_keep) may carry the unit it
    /// possessed into the next battle, for 1 + nAdd_abilityNum (1/2/3) battles: that value is
    /// hardcoded in UserUnitData.SetBattleResult_occupying, and the unit list and unit-info panel
    /// show UnitTypeTable.GetAbilityNum / UserUnitData.GetAbilityNum. This plugin raises that one
    /// slime's ability count to the configured value, and nothing else: no other slime type changes
    /// its ability or its count.
    ///
    /// Own GUID, own assembly; installs next to the item, occupation-display and mouse plugins.
    /// </summary>
    [BepInPlugin(SlimeFixPlugin.PluginGuid, SlimeFixPlugin.PluginName, SlimeFixPlugin.PluginVersion)]
    public class SlimeFixPlugin : BasePlugin
    {
#if INFINITE_DIVIDE
        public const string PluginGuid = "dsh.ambition.slimes2.slimefix.infinite";
        public const string PluginName = "Ambition of the SLIMES 2 - Slime Fix (Infinite Split)";
#else
        public const string PluginGuid = "dsh.ambition.slimes2.slimefix";
        public const string PluginName = "Ambition of the SLIMES 2 - Slime Fix";
#endif
        public const string PluginVersion = "1.1.0";

        internal static SlimeFixPlugin Instance;

        internal static ConfigEntry<bool> CfgEnabled;
        internal static ConfigEntry<bool> CfgVerboseLog;

        internal static ConfigEntry<bool> CfgInheritEnabled;
        internal static ConfigEntry<int> CfgInheritUseCount;
        internal static ConfigEntry<int> CfgInheritSourceType;

        internal static ConfigEntry<bool> CfgPossessEnabled;
        internal static ConfigEntry<int> CfgPossessSlimeAttr;
        internal static ConfigEntry<int> CfgPossessGrantAttr;

        internal static ConfigEntry<bool> CfgMimicEnabled;
        internal static ConfigEntry<int> CfgMimicSlimeType;
        internal static ConfigEntry<int> CfgMimicShellType;
        internal static ConfigEntry<int> CfgMimicLevelPercent;
        internal static ConfigEntry<int> CfgMimicShieldBreak;
        internal static ConfigEntry<bool> CfgMimicReplaceCaster;
        internal static ConfigEntry<string> CfgMimicForbidTypes;
        internal static ConfigEntry<float> CfgMimicTintR;
        internal static ConfigEntry<float> CfgMimicTintG;
        internal static ConfigEntry<float> CfgMimicTintB;

        internal static ConfigEntry<bool> CfgAmmoEnabled;
        internal static ConfigEntry<int> CfgAmmoUnlimitedValue;

        internal static ConfigEntry<bool> CfgDivideEnabled;
        internal static ConfigEntry<int> CfgDivideChildSplits;
        internal static ConfigEntry<int> CfgDivideMinHpPercent;
        internal static ConfigEntry<float> CfgDivideCanSplitAlpha;
        internal static ConfigEntry<float> CfgDivideCannotSplitAlpha;

        internal static ConfigEntry<bool> CfgDigestEnabled;
        internal static ConfigEntry<int> CfgDigestSlimeType;
        internal static ConfigEntry<int> CfgDigestSkeletonType;
        internal static ConfigEntry<bool> CfgDigestCopyAttribute;

        internal static ConfigEntry<bool> CfgReviveEnabled;
        internal static ConfigEntry<int> CfgReviveSlimeType;

        internal static ConfigEntry<bool> CfgDumpUnitTable;
        internal static ConfigEntry<bool> CfgSelfTest;

        public override void Load()
        {
            Instance = this;

            CfgEnabled = Config.Bind("General", "Enabled", true,
                "Master switch. When false every patch defers to the original game logic.");
            CfgVerboseLog = Config.Bind("General", "VerboseLog", false,
                "Log every value the patches change (debugging). Leave false for normal play: the BepInEx"
              + " console window blocks on heavy log traffic and freezes the game with it.");

            CfgInheritEnabled = Config.Bind("Inherit", "Enabled", true,
                "Raise the ability count of the slime that owns the inherit ability. When false the"
              + " game's own unit table is left untouched.");
            CfgInheritUseCount = Config.Bind("Inherit", "UseCount", 999,
                "Ability count written for that slime, both for the number the screens show and for the"
              + " number of battles a possessed unit is carried. The game's own value is 1 plus 0/1/2"
              + " from the ability items.");
            CfgInheritSourceType = Config.Bind("Inherit", "SourceType", 0,
                "UNIT_TYPE number of the slime whose count is raised. 0 means auto-detect: the plugin"
              + " picks the slime that already owns the inherit ability and logs which one it found."
              + " No other slime type is touched either way.");

            CfgPossessEnabled = Config.Bind("PossessAttr", "Enabled", true,
                "Give a possessed unit the attribute of the slime that possessed it, when that slime's"
              + " attribute is SlimeAttr. Applied on the field when the possession happens, and carried"
              + " into the next battle with the possessed unit. Nothing else about the unit changes.");
            CfgPossessSlimeAttr = Config.Bind("PossessAttr", "SlimeAttr", 4,
                "ATTR_TYPE the possessing slime must have for the swap to happen."
              + " 0 none, 1 fire, 2 water, 3 grass, 4 sun, -1 = any attribute.");
            CfgPossessGrantAttr = Config.Bind("PossessAttr", "GrantAttr", 4,
                "ATTR_TYPE written into the possessed unit. Same numbering as SlimeAttr.");

            CfgMimicEnabled = Config.Bind("Mimic", "Enabled", false,
                "Gives the zako slime a mimicry ability that copies any unit on the field and keeps"
              + " that shape for the rest of the battle.");
            CfgMimicSlimeType = Config.Bind("Mimic", "SlimeType", 1,
                "UNIT_TYPE that may mimic. 1 is the zako slime.");
            CfgMimicShellType = Config.Bind("Mimic", "ShellType", 15,
                "UNIT_TYPE whose ability is borrowed as the ability shell. 15 is the anti-magic"
              + " slime, whose shield breaker is what the mimic entry shows.");
            CfgMimicLevelPercent = Config.Bind("Mimic", "LevelPercent", 70,
                "Percent of the copied unit's level the mimic ends up at, so 70 means 30 percent"
              + " lower. HP, attack and defence follow from that level.");
            CfgMimicShieldBreak = Config.Bind("Mimic", "ShieldBreakPercent", 5,
                "Percent chance that using the mimic also breaks the target's shield, which is what"
              + " the borrowed shell normally does.");
            CfgMimicForbidTypes = Config.Bind("Mimic", "ForbidTypes", "21,22",
                "Comma separated UNIT_TYPE values the mimic refuses to copy. 21 and 22 are the split"
              + " slimes, whose copies would be able to split as well.");
            CfgMimicReplaceCaster = Config.Bind("Mimic", "ReplaceCaster", true,
                "true removes the zako's own body once the copied shape is in place, so the shape"
              + " replaces it; false keeps both on the field.");
            CfgMimicTintR = Config.Bind("Mimic", "TintR", 0.25f, "Red of the tint drawn over a mimicked unit.");
            CfgMimicTintG = Config.Bind("Mimic", "TintG", 1f, "Green of the tint drawn over a mimicked unit.");
            CfgMimicTintB = Config.Bind("Mimic", "TintB", 0.25f, "Blue of the tint drawn over a mimicked unit.");

            CfgAmmoEnabled = Config.Bind("Ammo", "Enabled", true,
                "Turns the possession ability that raises the host's ammo limit by one into"
              + " unlimited ammo.");
            CfgAmmoUnlimitedValue = Config.Bind("Ammo", "UnlimitedValue", 0,
                "Attack count written to the host. 0 is the game's own unlimited marker: the unit"
              + " panel shows 00_mugen and the attack command is never refused.");

            CfgDivideEnabled = Config.Bind("Divide", "Enabled", true,
                "Lets a divided slime split once more. The child of that second split gets no further"
              + " split and is drawn fainter.");
            CfgDivideChildSplits = Config.Bind("Divide", "ChildSplits", 1,
                "Ability uses a freshly divided child is given, so 1 means the chain is two splits deep.");
            CfgDivideCanSplitAlpha = Config.Bind("Divide", "CanSplitAlpha", 1f,
                "Quad alpha for a divided slime that can still split, 0 to 1. 1 is full opacity.");
            CfgDivideCannotSplitAlpha = Config.Bind("Divide", "CannotSplitAlpha", 0.8f,
                "Alpha of a divided slime that cannot split any more, 0 to 1. 0.8 is 20 percent"
              + " transparent; lower is fainter.");
            CfgDivideMinHpPercent = Config.Bind("Divide", "MinHpPercent", 25,
                "A divided slime whose HP percentage is below this cannot split. 0 disables the rule.");

            CfgDigestEnabled = Config.Bind("Digest", "Enabled", true,
                "While this slime is holding a swallowed unit, its ability button digests instead of"
              + " starting the ability: one ability use is spent, the swallowed unit is destroyed, and"
              + " a skeleton of that unit's level appears on the slime's own cell on the player's side."
              + " The slime then goes back to its normal state.");
            CfgDigestSlimeType = Config.Bind("Digest", "SlimeType", 10,
                "UNIT_TYPE whose ability button digests its swallowed unit. 10 is the melty slime.");
            CfgDigestSkeletonType = Config.Bind("Digest", "SkeletonType", 62,
                "UNIT_TYPE created by the digest. 62 is the skeleton the game's own skeleton ability"
              + " turns units into; 20 would be the skeleton slime.");
            CfgDigestCopyAttribute = Config.Bind("Digest", "CopyAttribute", true,
                "true copies the swallowed unit's attribute onto the skeleton, false uses the slime's.");

            CfgReviveEnabled = Config.Bind("ReviveOnDeath", "Enabled", true,
                "When a slime of SlimeType possesses a unit and that possessed unit is killed, the slime"
              + " drops its own body on the same cell instead of dying with the host, so it can possess"
              + " again. Once per battle: the next lethal hit kills it normally. Only the player's own"
              + " slimes are affected, and the zombie and skeleton abilities are untouched on both"
              + " sides.");
            CfgReviveSlimeType = Config.Bind("ReviveOnDeath", "SlimeType", 1,
                "UNIT_TYPE that drops its body instead of dying inside a host. 1 is the zako slime,"
              + " or -1 for any slime type (the body that drops out is then the slime that went in).");

            CfgDumpUnitTable = Config.Bind("Diagnostics", "DumpUnitTable", false,
                "Once, as soon as the unit table is readable, log every slime type with its name, its"
              + " shipped ability and its shipped ability count. Turn it off again afterwards.");
            CfgSelfTest = Config.Bind("Diagnostics", "SelfTest", false,
                "Once, as soon as the unit table is readable, log the shipped and the patched ability"
              + " count of every slime type and run the battle-result step on a detached unit-box"
              + " record. Nothing that the game holds is modified. Turn it off again afterwards.");

            Inherit.Configure();

            var harmony = new Harmony(PluginGuid);
            harmony.PatchAll();

            var patched = harmony.GetPatchedMethods().ToList();
            Log.LogInfo($"Harmony patched method count: {patched.Count}");
            foreach (var method in patched)
                Log.LogInfo($"  patched -> {method.DeclaringType?.FullName}.{method.Name}");

            if (patched.Count == 0) Log.LogError("NO METHOD WAS PATCHED - the plugin has no effect.");
            else Log.LogInfo($"SlimeFix {PluginVersion} active: Enabled={CfgEnabled.Value}, Inherit={CfgInheritEnabled.Value}");

        }

        internal static void Warn(string message) => Instance?.Log.LogWarning(message);

        internal static void Verbose(string message)
        {
            if (CfgVerboseLog != null && CfgVerboseLog.Value) Instance?.Log.LogInfo(message);
        }
    }
}
