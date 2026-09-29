// This plugin was written by an AI assistant (DeepSeek). The author set the requirements, made the
// design calls, tested every change in the game and owns the release. See AI-DISCLOSURE.md.
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace SlimesMouse
{
    /// <summary>
    /// Mouse input for Ambition of the SLIMES 2, whose PC release only maps a keyboard: a click on a UI entry
    /// activates it, a click on the battlefield moves the tile cursor, a double click confirms, a drag sweeps the
    /// cursor, the right button cancels and the wheel zooms.
    /// Split out of the combined SlimesTweaks plugin; this assembly carries the mouse module only, so it can be
    /// installed without the item or occupation/display plugins.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class MousePlugin : BasePlugin
    {
        public const string PluginGuid = "dsh.ambition.slimes2.mouse";
        public const string PluginName = "Ambition of the SLIMES 2 - Mouse Tweaks";
        public const string PluginVersion = "3.0.0";

        internal static MousePlugin Instance;

        internal static ConfigEntry<bool> CfgEnabled;
        internal static ConfigEntry<bool> CfgVerboseLog;

        internal static ConfigEntry<bool> CfgMouse;
        internal static ConfigEntry<bool> CfgMouseDragCursor;
        internal static ConfigEntry<bool> CfgMouseWheelNavigate;
        internal static ConfigEntry<bool> CfgMouseWheelMapSelect;
        internal static ConfigEntry<bool> CfgMouseRightCancels;
        internal static ConfigEntry<float> CfgMouseDoubleClickMs;
        internal static ConfigEntry<float> CfgMouseDragThresholdPixels;
        internal static ConfigEntry<float> CfgMouseDragDelayMs;

        public override void Load()
        {
            Instance = this;

            CfgEnabled = Config.Bind("General", "Enabled", true,
                "Master switch for this plugin. When false the mouse patches stay idle and the game keeps its "
              + "keyboard-only input. The [Mouse] section below still decides what the pointer does.");
            CfgVerboseLog = Config.Bind("General", "VerboseLog", false,
                "Log every cursor move, window click and injected button press (debugging).");

            CfgMouse = Config.Bind("Mouse", "Enabled", true,
                "Let the mouse drive the game, which otherwise only accepts keyboard input. A click on a UI entry "
              + "activates it, a click on the battlefield moves the tile cursor, and a double click confirms.");
            CfgMouseDragCursor = Config.Bind("Mouse", "DragMovesCursor", true,
                "Holding the left button and dragging sweeps the tile cursor along the drag direction.");
            CfgMouseWheelNavigate = Config.Bind("Mouse", "WheelNavigates", true,
                "Mouse wheel moves the selection up and down, the same as the up and down keys: the tile cursor in "
              + "a battle, the highlighted row in a menu. Camera zoom stays on the V key, because its levels are "
              + "fixed and a wheel cannot fine-tune them.");
            CfgMouseWheelMapSelect = Config.Bind("Mouse", "WheelMovesStageSelect", false,
                "Also let the wheel walk the stage select's world map east and west. Off by default: every focus "
              + "step there reloads the area's models, so scrolling quickly can stall or take the game down. "
              + "Turn it on to try it, and scroll one notch at a time.");
            CfgMouseRightCancels = Config.Bind("Mouse", "RightClickCancels", true,
                "Right click sends cancel, the same action as the X key.");
            CfgMouseDoubleClickMs = Config.Bind("Mouse", "DoubleClickMs", 350f,
                "Maximum gap between two left clicks on the battlefield for them to count as a confirm.");
            CfgMouseDragThresholdPixels = Config.Bind("Mouse", "DragThresholdPixels", 14f,
                "How far the pointer must move while the left button is held before the drag starts sweeping the "
              + "tile cursor. Keeps a slightly long click from dragging the cursor along with the mouse.");
            CfgMouseDragDelayMs = Config.Bind("Mouse", "DragDelayMs", 220f,
                "How long the left button must be held before the drag starts. Together with DragThresholdPixels "
              + "this separates a click (move the cursor once) from a deliberate drag.");

            var harmony = new Harmony(PluginGuid);
            harmony.PatchAll();

            var patched = harmony.GetPatchedMethods().ToList();
            Log.LogInfo($"Harmony patched method count: {patched.Count}");
            foreach (var method in patched)
                Log.LogInfo($"  patched -> {method.DeclaringType?.FullName}.{method.Name}");

            if (patched.Count == 0) Log.LogError("NO METHOD WAS PATCHED - the plugin has no effect.");
            else Log.LogInfo($"MouseTweaks {PluginVersion} active: Enabled={CfgEnabled.Value}");
        }

        internal static void Warn(string message) => Instance?.Log.LogWarning(message);
        internal static void Verbose(string message)
        {
            if (CfgVerboseLog != null && CfgVerboseLog.Value) Instance?.Log.LogInfo(message);
        }
    }
}
