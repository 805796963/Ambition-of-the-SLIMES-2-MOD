using System;
using HarmonyLib;

namespace SlimesSlimeFix
{
    /// <summary>
    /// Turns the "ammo limit +1" possession ability into unlimited ammo.
    ///
    /// The ability is applied inside UnitData.SetOccupiedUnitParam, which UnitData.SetOccupied calls
    /// to copy the possessing slime's ability onto the body it just took over: for the ammo ability
    /// it runs nAttackCountMax++ and nAttackCount++, guarded by nAttackCountMax > 0. That guard is
    /// also the detector - comparing the value across the possession identifies the ability without
    /// depending on its ABILITY_TYPE value, which is not known here.
    ///
    /// Unlimited ammo is expressed the way the game already expresses it: with nAttackCountMax at 0,
    /// UnitData.GetAttackCountRest returns -1, BattleFlow.PushActionAttackButton only refuses when
    /// that value is exactly 0, and the unit panel draws "00_mugen" instead of a number.
    ///
    /// Only a possession by one of the player's own slimes counts, so an enemy slime that takes one
    /// of the player's units over does not hand that unit unlimited attacks.
    /// </summary>
    internal static class Ammo
    {
        /// <summary>Whether the possessing slime is the player's, recorded by the SetOccupied prefix.</summary>
        private static bool possessorIsOurs;

        /// <summary>Whether the ammo ability is turned into unlimited ammo.</summary>
        internal static bool Active =>
            SlimeFixPlugin.CfgEnabled != null && SlimeFixPlugin.CfgEnabled.Value
            && SlimeFixPlugin.CfgAmmoEnabled != null && SlimeFixPlugin.CfgAmmoEnabled.Value;

        /// <summary>Value written to the attack counts. 0 is the game's own unlimited marker.</summary>
        internal static int UnlimitedValue => SlimeFixPlugin.CfgAmmoUnlimitedValue?.Value ?? 0;

        /// <summary>Records whose slime is taking the body over, for <see cref="AfterPossession"/>.</summary>
        internal static void NotePossession(UnitData host, int slimeUnitOffs)
        {
            possessorIsOurs = false;
            if (!Active || host == null) return;

            var battle = host.BattleFlowComponent;
            var units = battle == null ? null : battle.UnitDataArray;
            if (units == null) return;
            if (slimeUnitOffs < 0 || slimeUnitOffs >= units.Length) return;

            var slime = units[slimeUnitOffs];
            if (slime == null) return;

            possessorIsOurs = slime.m_nUserUnitOffs >= 0;
        }

        /// <summary>
        /// Rewrites the attack counts when the possession that just ran was the ammo ability, which
        /// is recognised by the +1 the ability itself applied.
        /// </summary>
        internal static void AfterPossession(UnitData host, int maxBefore)
        {
            if (!Active || host == null) return;
            if (!possessorIsOurs) return;
            if (host.nAttackCountMax != maxBefore + 1) return;

            host.nAttackCountMax = UnlimitedValue;
            host.nAttackCount = UnlimitedValue;

            SlimeFixPlugin.Instance?.Log.LogInfo(
                $"ammo: the possessed body has unlimited attacks now (nAttackCountMax"
              + $" {maxBefore + 1} -> {UnlimitedValue}, which the panel shows as 00_mugen).");
        }
    }

    /// <summary>
    /// Wraps the possession so the ammo the ability granted can be rewritten, and so the side of the
    /// possessing slime is known.
    /// </summary>
    [HarmonyPatch(typeof(UnitData), nameof(UnitData.SetOccupied))]
    internal static class Patch_UnitData_SetOccupied_Ammo
    {
        private static void Prefix(UnitData __instance, int slimeUnitOffs, out int __state)
        {
            __state = __instance == null ? 0 : __instance.nAttackCountMax;

            try { Ammo.NotePossession(__instance, slimeUnitOffs); }
            catch (Exception e) { SlimeFixPlugin.Warn("ammo: reading the possessing slime failed: " + e.Message); }
        }

        private static void Postfix(UnitData __instance, int __state)
        {
            if (!Ammo.Active) return;

            try { Ammo.AfterPossession(__instance, __state); }
            catch (Exception e) { SlimeFixPlugin.Warn("ammo: rewriting the attack counts failed: " + e.Message); }
        }
    }
}
