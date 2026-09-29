// This plugin was written by an AI assistant (DeepSeek). The author set the requirements, made the
// design calls, tested every change in the game and owns the release. See AI-DISCLOSURE.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace SlimesItems
{
    /// <summary>
    /// Item tweaks for Ambition of the SLIMES 2: stage-clear item rewards and per-slime item restrictions.
    /// Split out of the combined SlimesTweaks plugin; this assembly carries the item patches only, so it can be
    /// installed without the occupation/display or mouse plugins. Rendering and occupation behaviour live in
    /// the sibling assemblies.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class ItemsPlugin : BasePlugin
    {
        public const string PluginGuid = "dsh.ambition.slimes2.items";
        public const string PluginName = "Ambition of the SLIMES 2 - Items Tweaks";
        public const string PluginVersion = "3.0.0";

        internal static ItemsPlugin Instance;

        internal static ConfigEntry<bool> CfgEnabled;
        internal static ConfigEntry<bool> CfgVerboseLog;

        internal static ConfigEntry<bool> CfgGrantAllItemsOnItemPrize;
        internal static ConfigEntry<bool> CfgGrantAllItemsOnUnitPrize;
        internal static ConfigEntry<bool> CfgSelfTest;
        internal static ConfigEntry<bool> CfgUnlimitedItemUse;
        internal static ConfigEntry<bool> CfgTransformAnySlime;

        internal static ConfigEntry<int> CfgUnitBoxMax;

        public override void Load()
        {
            Instance = this;

            CfgEnabled = Config.Bind("General", "Enabled", true,
                "Master switch. When false every patch defers to the original game logic.");
            CfgVerboseLog = Config.Bind("General", "VerboseLog", false,
                "Log the per-item-patch detail (debugging).");

            CfgGrantAllItemsOnItemPrize = Config.Bind("Items", "GrantAllItemsOnItemPrize", true,
                "On an item prize, grant one of every other item type as well, so a clear yields all 8.");
            CfgGrantAllItemsOnUnitPrize = Config.Bind("Items", "GrantAllItemsOnUnitPrize", true,
                "On a slime (unit) prize, grant one of every item type, since vanilla grants no item there.");
            CfgSelfTest = Config.Bind("Items", "SelfTestOnFirstMessage", false,
                "Once, after the save is loaded: grant one of every item type, log the counts, then restore them.");
            CfgUnlimitedItemUse = Config.Bind("Items", "UnlimitedItemUse", true,
                "Lift the per-slime item budget above the vanilla nRestItemTurn = 1 + level/10. UserUnitData."
              + "UseItemHistory has only Defs.MAX_ITEM_USE (10) slots, so UseItemTurn is pinned to the last slot "
              + "before each write; without that the 11th use throws inside the item button handler and the button "
              + "silently stops responding. Side effect: the reset item refunds at most 10 entries.");
            CfgTransformAnySlime = Config.Bind("Items", "TransformAnySlime", true,
                "Let the transform item work on any slime type (vanilla requires UNIT_TYPE.s_zako).");

            CfgUnitBoxMax = Config.Bind("Units", "MaxUnitCount", 32,
                "How many slimes the unit box holds. 32 is the game's own value, and this default changes nothing.\n"
              + "\n"
              + "READ THIS BEFORE RAISING IT. The count is NOT stored in the save file: SaveData/SDS_UserUnitTable."
              + "Write and .Read both loop to Defs.USER_UNIT_MAX and write that many unit records back to back, so "
              + "the reader learns the count from this setting rather than from the file. Raising it therefore does "
              + "not extend an existing save, it desynchronises it: the reader consumes more records than the file "
              + "holds and everything stored after the unit table loads as garbage. The same happens in reverse, so "
              + "a save written with a raised value cannot be loaded by the unmodded game, and changing this value "
              + "back and forth invalidates saves made with the other value.\n"
              + "\n"
              + "So: raise it only together with a NEW save, and keep the value fixed for as long as you play that "
              + "save. The unit box, the 目前单位数 / 最大单位数 counter and the save/load loops all follow this one "
              + "value. It raises storage only - how many units a battle can field is a separate, smaller limit.\n"
              + "Values below 1 leave the game's own limit alone.");

            var harmony = new Harmony(PluginGuid);
            harmony.PatchAll();

            var patched = harmony.GetPatchedMethods().ToList();
            Log.LogInfo($"Harmony patched method count: {patched.Count}");
            foreach (var method in patched)
                Log.LogInfo($"  patched -> {method.DeclaringType?.FullName}.{method.Name}");

            if (patched.Count == 0) Log.LogError("NO METHOD WAS PATCHED - the plugin has no effect.");
            else Log.LogInfo($"ItemsTweaks {PluginVersion} active: Enabled={CfgEnabled.Value}");
        }

        internal static void Warn(string message) => Instance?.Log.LogWarning(message);
        internal static void Verbose(string message)
        {
            if (CfgVerboseLog != null && CfgVerboseLog.Value) Instance?.Log.LogInfo(message);
        }
    }

    /// <summary>
    /// Reads and writes members of IL2CPP interop types without depending on whether Il2CppInterop emitted a
    /// field or a property, nor on the exact managed type of an enum-backed member. Il2CppInterop emits IL2CPP
    /// fields as properties whose accessors go through the native object, so these writes reach the game.
    /// </summary>
    internal static class NativeMember
    {
        private const BindingFlags AnyMember =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        private static readonly Dictionary<string, MemberInfo> Cache = new Dictionary<string, MemberInfo>();
        private static readonly HashSet<string> ReportedMissing = new HashSet<string>();

        private static MemberInfo Resolve(Type type, string name)
        {
            if (Cache.TryGetValue(name, out var cached)) return cached;

            MemberInfo member = type.GetField(name, AnyMember);
            if (member == null) member = type.GetProperty(name, AnyMember);

            if (member == null)
            {
                if (ReportedMissing.Add(name)) ItemsPlugin.Warn($"member not found on {type.Name}: {name}");
                return null;
            }

            Cache[name] = member;
            return member;
        }

        internal static object GetRaw(object target, string name)
        {
            try
            {
                var member = Resolve(target.GetType(), name);
                if (member == null) return null;
                return member is FieldInfo field ? field.GetValue(target) : ((PropertyInfo)member).GetValue(target, null);
            }
            catch (Exception e)
            {
                ItemsPlugin.Warn($"read {name} failed: {e.Message}");
                return null;
            }
        }

        private static void WriteRaw(object target, string name, object value)
        {
            var member = Resolve(target.GetType(), name);
            if (member == null) return;

            var memberType = member is FieldInfo field ? field.FieldType : ((PropertyInfo)member).PropertyType;
            if (memberType.IsEnum) value = Enum.ToObject(memberType, Convert.ToInt64(value));

            if (member is FieldInfo f) f.SetValue(target, value);
            else ((PropertyInfo)member).SetValue(target, value, null);
        }

        internal static int GetInt(object target, string name, int fallback)
        {
            try
            {
                var raw = GetRaw(target, name);
                return raw == null ? fallback : Convert.ToInt32(raw);
            }
            catch (Exception e)
            {
                ItemsPlugin.Warn($"read {name} failed: {e.Message}");
                return fallback;
            }
        }

        internal static bool GetBool(object target, string name, bool fallback)
        {
            try
            {
                var raw = GetRaw(target, name);
                return raw == null ? fallback : Convert.ToBoolean(raw);
            }
            catch (Exception e)
            {
                ItemsPlugin.Warn($"read {name} failed: {e.Message}");
                return fallback;
            }
        }

        internal static void SetInt(object target, string name, int value)
        {
            try { WriteRaw(target, name, value); }
            catch (Exception e) { ItemsPlugin.Warn($"write {name} failed: {e.Message}"); }
        }

        internal static void SetBool(object target, string name, bool value)
        {
            try { WriteRaw(target, name, value); }
            catch (Exception e) { ItemsPlugin.Warn($"write {name} failed: {e.Message}"); }
        }
    }
}
