using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SlimesOccupyColor
{
    // ---------------------------------------------------------------- occupation

    /// <summary>
    /// Original: returns false when the unit is hidden, is not on the human side, or is not in the normal
    /// occupation state; any of those makes it an invalid target.
    /// </summary>
    [HarmonyPatch(typeof(UnitData), "IsAblePossessionUnit")]
    internal static class Patch_IsAblePossessionUnit
    {
        [HarmonyPrefix]
        private static bool Prefix(ref bool __result)
        {
            if (!OccupyColorPlugin.CfgEnabled.Value || !OccupyColorPlugin.CfgForceTargetable.Value) return true;

            __result = true;
            return false;
        }
    }

    /// <summary>
    /// Original verdict (OCCUPY_RESULT_TYPE: match=0, ok=1, ng=2):
    ///   * demon side and not yet possessed         -> ng
    ///   * ability 25 while not stopped, ability 31 -> ng
    ///   * m_bProtected and ability != eater(1)     -> ng
    ///   * otherwise, matching slime attribute      -> match, else ok
    ///
    /// The patch neutralises the side-type and ability gates for the duration of the call so the original body
    /// still decides the shield case and the match case, and restores them afterwards. It never writes the
    /// unit's side outside this call.
    /// </summary>
    [HarmonyPatch(typeof(UnitData), "CheckOccupyResult")]
    internal static class Patch_CheckOccupyResult
    {
        private const int SideHuman = 1;
        private const int SideDemon = 2;
        private const int AbilityNinjya = 25;
        private const int AbilityAntiOccupy = 31;

        private static int _savedSide;
        private static int _savedAbility;
        private static bool _savedProtected;
        private static bool _saved;

        [HarmonyPrefix]
        private static void Prefix(UnitData __instance)
        {
            if (!OccupyColorPlugin.CfgEnabled.Value) return;

            _savedSide = NativeMember.GetInt(__instance, "m_nSideType", SideHuman);
            _savedAbility = NativeMember.GetInt(__instance, "m_nAbility", 0);
            _savedProtected = NativeMember.GetBool(__instance, "m_bProtected", false);
            _saved = true;

            if (_savedSide == SideDemon) NativeMember.SetInt(__instance, "m_nSideType", SideHuman);
            if (_savedAbility == AbilityNinjya || _savedAbility == AbilityAntiOccupy)
                NativeMember.SetInt(__instance, "m_nAbility", 0);
            if (!OccupyColorPlugin.CfgPreserveShield.Value && _savedProtected)
                NativeMember.SetBool(__instance, "m_bProtected", false);

            OccupyColorPlugin.Verbose($"CheckOccupyResult in : side={_savedSide} ability={_savedAbility} protected={_savedProtected}");
        }

        [HarmonyPostfix]
        private static void Postfix(UnitData __instance)
        {
            if (!_saved) return;
            _saved = false;

            NativeMember.SetInt(__instance, "m_nSideType", _savedSide);
            NativeMember.SetInt(__instance, "m_nAbility", _savedAbility);
            if (!OccupyColorPlugin.CfgPreserveShield.Value && _savedProtected)
                NativeMember.SetBool(__instance, "m_bProtected", true);

            OccupyColorPlugin.Verbose($"CheckOccupyResult out: result={NativeMember.GetInt(__instance, "m_nOccupyResultType", -1)}");
        }
    }

    /// <summary>
    /// The occupation confirm handler checks the target directly, bypassing m_nOccupyResultType:
    ///   bSlime                                      -> error_occupy_myside (kept)
    ///   m_nAbility == antioccupy(31)                -> occupy_ng_emperor   (removed)
    ///   m_nSideType == demon &amp;&amp; !IsPossessioned()  -> occupy_ng_demon    (removed)
    ///   IsOccupied()                                -> kept
    ///   m_bProtected &amp;&amp; result == ng               -> occupy_ng_protection (kept)
    ///   result == ng                                -> occupy_ng_ninjya / ng_occupy (already neutralised)
    ///
    /// Only ACTION_STEP 5 (the occupation confirm) is touched, and the side/ability members of every affected
    /// unit are restored as soon as the handler returns.
    /// </summary>
    [HarmonyPatch(typeof(BattleFlow), "PushActionOKButton")]
    internal static class Patch_PushActionOKButton
    {
        private const int ActionStepOccupyConfirm = 5;

        private static readonly List<object> TouchedUnits = new List<object>();
        private static readonly List<int> TouchedSides = new List<int>();
        private static readonly List<int> TouchedAbilities = new List<int>();
        private static bool _active;

        [HarmonyPrefix]
        private static void Prefix(BattleFlow __instance)
        {
            if (!OccupyColorPlugin.CfgEnabled.Value) return;
            if (NativeMember.GetInt(__instance, "nActionStep", -1) != ActionStepOccupyConfirm) return;

            var units = NativeMember.GetRaw(__instance, "UnitDataArray") as IEnumerable;
            if (units == null)
            {
                OccupyColorPlugin.Warn("UnitDataArray not enumerable; the occupation confirm gate is not neutralised.");
                return;
            }

            _active = true;
            foreach (var unit in units)
            {
                if (unit == null) continue;

                int side = NativeMember.GetInt(unit, "m_nSideType", 1);
                int ability = NativeMember.GetInt(unit, "m_nAbility", 0);
                bool changed = false;

                if (side == 2) { NativeMember.SetInt(unit, "m_nSideType", 1); changed = true; }
                if (ability == 31) { NativeMember.SetInt(unit, "m_nAbility", 0); changed = true; }

                if (changed)
                {
                    TouchedUnits.Add(unit);
                    TouchedSides.Add(side);
                    TouchedAbilities.Add(ability);
                }
            }

            OccupyColorPlugin.Verbose($"PushActionOKButton: neutralised {TouchedUnits.Count} unit(s) for the confirm gate");
        }

        [HarmonyPostfix]
        private static void Postfix(BattleFlow __instance)
        {
            if (!_active) return;
            _active = false;

            for (int i = 0; i < TouchedUnits.Count; i++)
            {
                NativeMember.SetInt(TouchedUnits[i], "m_nSideType", TouchedSides[i]);
                NativeMember.SetInt(TouchedUnits[i], "m_nAbility", TouchedAbilities[i]);
            }

            OccupyColorPlugin.Verbose($"PushActionOKButton: restored {TouchedUnits.Count} unit(s)");
            TouchedUnits.Clear();
            TouchedSides.Clear();
            TouchedAbilities.Clear();
        }
    }
}
