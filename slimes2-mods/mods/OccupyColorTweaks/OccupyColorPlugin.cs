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

namespace SlimesOccupyColor
{
    /// <summary>
    /// Occupation tweaks for Ambition of the SLIMES 2 (occupation restrictions lifted, shield mechanic preserved)
    /// together with the rendering-only display fixes that make a forced possession visible again.
    /// Split out of the combined SlimesTweaks plugin; this assembly carries the occupation and display patches
    /// only, so it can be installed without the item or mouse plugins.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class OccupyColorPlugin : BasePlugin
    {
        public const string PluginGuid = "dsh.ambition.slimes2.occupycolor";
        public const string PluginName = "Ambition of the SLIMES 2 - Occupy Color Tweaks";
        public const string PluginVersion = "3.0.0";

        internal static OccupyColorPlugin Instance;

        internal static ConfigEntry<bool> CfgEnabled;
        internal static ConfigEntry<bool> CfgVerboseLog;

        internal static ConfigEntry<bool> CfgPreserveShield;
        internal static ConfigEntry<bool> CfgForceTargetable;

        internal static ConfigEntry<bool> CfgDisplayFallback;
        internal static ConfigEntry<bool> CfgFactionTint;
        internal static ConfigEntry<float> CfgTintRed;
        internal static ConfigEntry<float> CfgTintGreen;
        internal static ConfigEntry<float> CfgTintBlue;
        internal static ConfigEntry<float> CfgTintGlow;
        internal static ConfigEntry<float> CfgTintStrength;

        public override void Load()
        {
            Instance = this;

            CfgEnabled = Config.Bind("General", "Enabled", true,
                "Master switch. When false every patch defers to the original game logic.");
            CfgVerboseLog = Config.Bind("General", "VerboseLog", false,
                "Log the per-unit occupation verdicts (debugging).");

            CfgPreserveShield = Config.Bind("Occupy", "PreserveShield", true,
                "Keep the shield mechanic: a unit with m_bProtected stays unoccupiable (except by the eater ability).");
            CfgForceTargetable = Config.Bind("Occupy", "ForceTargetable", true,
                "Make UnitData.IsAblePossessionUnit() always return true so the game's other occupation gate is open.");

            CfgDisplayFallback = Config.Bind("Display", "ShowForcedPossession", true,
                "Make a forced possession visible again with the game's own art: the atlas lookup falls back to "
              + "the plain _m/_face sprite when the _mr/_b_face variant does not exist, and the unit quad is "
              + "repainted once. Rendering only - no occuptation state, side type, ability or stat is written.");

            CfgFactionTint = Config.Bind("Display", "FactionTint", false,
                "Recolour a forced possession towards the faction look. The game has no possessed art for a unit "
              + "it never lets you possess, so the unit's own sheet is copied and tinted. Rendering only.");
            CfgTintRed = Config.Bind("Display", "TintRed", 1.55f, "Red channel multiplier for the faction tint.");
            CfgTintGreen = Config.Bind("Display", "TintGreen", 0.55f, "Green channel multiplier for the faction tint.");
            CfgTintBlue = Config.Bind("Display", "TintBlue", 0.95f, "Blue channel multiplier for the faction tint.");
            CfgTintGlow = Config.Bind("Display", "TintGlow", 18f, "Added brightness (0-255) for the faction tint.");
            CfgTintStrength = Config.Bind("Display", "TintStrength", 0.45f,
                "How far the faction tint is blended in, 0 = original colours, 1 = fully tinted. Lower values keep "
              + "the original shading and look softer.");

            var harmony = new Harmony(PluginGuid);
            harmony.PatchAll();

            var patched = harmony.GetPatchedMethods().ToList();
            Log.LogInfo($"Harmony patched method count: {patched.Count}");
            foreach (var method in patched)
                Log.LogInfo($"  patched -> {method.DeclaringType?.FullName}.{method.Name}");

            if (patched.Count == 0) Log.LogError("NO METHOD WAS PATCHED - the plugin has no effect.");
            else Log.LogInfo($"OccupyColorTweaks {PluginVersion} active: Enabled={CfgEnabled.Value}");
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
                if (ReportedMissing.Add(name)) OccupyColorPlugin.Warn($"member not found on {type.Name}: {name}");
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
                OccupyColorPlugin.Warn($"read {name} failed: {e.Message}");
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
                OccupyColorPlugin.Warn($"read {name} failed: {e.Message}");
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
                OccupyColorPlugin.Warn($"read {name} failed: {e.Message}");
                return fallback;
            }
        }

        internal static void SetInt(object target, string name, int value)
        {
            try { WriteRaw(target, name, value); }
            catch (Exception e) { OccupyColorPlugin.Warn($"write {name} failed: {e.Message}"); }
        }

        internal static void SetBool(object target, string name, bool value)
        {
            try { WriteRaw(target, name, value); }
            catch (Exception e) { OccupyColorPlugin.Warn($"write {name} failed: {e.Message}"); }
        }
    }
}
