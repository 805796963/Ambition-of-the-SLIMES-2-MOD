using System;
using HarmonyLib;

namespace SlimesBattle
{
    /// <summary>Scales the damage an attack resolves to.</summary>
    [HarmonyPatch(typeof(UnitData), nameof(UnitData.CalcAttackResult))]
    internal static class Patch_UnitData_CalcAttackResult_Damage
    {
        private static void Postfix(UnitData __instance, ref int __result)
        {
            int original = __result;
            __result = Damage.Scale(original);

            try
            {
                if (__instance != null && __result != original)
                    BattleTweaksPlugin.Verbose($"damage: attack {original} -> {__result} on"
                                             + $" {Strs.GetUnitTypeName(__instance.nType)}.");
            }
            catch (Exception) { /* logging only */ }
        }
    }

    /// <summary>Scales the damage a counter resolves to, so counterattacks match.</summary>
    [HarmonyPatch(typeof(UnitData), nameof(UnitData.CalcCounterResult))]
    internal static class Patch_UnitData_CalcCounterResult_Damage
    {
        private static void Postfix(UnitData __instance, ref int __result)
        {
            int original = __result;
            __result = Damage.Scale(original);

            try
            {
                if (__instance != null && __result != original)
                    BattleTweaksPlugin.Verbose($"damage: counter {original} -> {__result} on"
                                             + $" {Strs.GetUnitTypeName(__instance.nType)}.");
            }
            catch (Exception) { /* logging only */ }
        }
    }

    /// <summary>
    /// The damage multiplier. Both directions go through the same calculation, so doubling here doubles
    /// what a unit deals and what it takes.
    /// </summary>
    internal static class Damage
    {
        internal static float Multiplier =>
            Math.Max(0f, BattleTweaksPlugin.CfgMultiplier?.Value ?? 2f);

        internal static int Scale(int damage)
        {
            if (BattleTweaksPlugin.CfgEnabled == null || !BattleTweaksPlugin.CfgEnabled.Value) return damage;
            if (damage == 0) return damage;

            float multiplier = Multiplier;
            if (multiplier == 1f) return damage;

            return (int)Math.Round(damage * multiplier, MidpointRounding.AwayFromZero);
        }
    }
}
