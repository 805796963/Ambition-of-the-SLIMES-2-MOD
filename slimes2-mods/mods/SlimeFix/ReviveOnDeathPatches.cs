using System;
using System.Collections.Generic;
using HarmonyLib;

namespace SlimesSlimeFix
{
    /// <summary>
    /// Lets a slime drop its own body instead of dying inside the host it possessed.
    ///
    /// A possessed unit is a player unit that carries the slime's unit-box offset. When it takes a
    /// lethal hit the game runs UnitData.StartUnitDead, which ends the unit and, at the end of the
    /// battle, removes its unit-box record. This patch remembers the slime types that took a host
    /// over, and when one of those hosts reaches StartUnitDead with no health left it turns the unit
    /// back into the slime on the same cell instead: the unit keeps its side and its unit-box
    /// offset, so the player can possess again with it.
    ///
    /// It deliberately does NOT touch the zombie or skeleton conversion. The game reaches those
    /// through their own door (m_bReserveZombie and Change2Zombie), which this patch neither reads
    /// nor writes, so a possessed host whose slime has the zombie ability still becomes a zombie, on
    /// either side, exactly as before.
    ///
    /// One drop per slime per battle; the next lethal hit kills it normally. Only the player's own
    /// slimes are affected - a unit without a unit-box offset (the enemy side) is never armed.
    /// </summary>
    internal static class ReviveOnDeath
    {
        /// <summary>Unit-box offsets whose slime may drop its body on a lethal hit.</summary>
        private static readonly HashSet<int> Armed = new HashSet<int>();

        /// <summary>Unit-box offsets that already dropped their body in this battle.</summary>
        private static readonly HashSet<int> Revived = new HashSet<int>();

        /// <summary>Whether the drop-on-death patch may change anything at all.</summary>
        internal static bool Active =>
            SlimeFixPlugin.CfgEnabled != null && SlimeFixPlugin.CfgEnabled.Value
            && SlimeFixPlugin.CfgReviveEnabled != null && SlimeFixPlugin.CfgReviveEnabled.Value;

        /// <summary>
        /// UNIT_TYPE that drops its body instead of dying inside a host, or a negative value for any
        /// slime type; the body that drops out is then the slime that went in.
        /// </summary>
        internal static int SlimeType => SlimeFixPlugin.CfgReviveSlimeType?.Value ?? (int)UNIT_TYPE.s_zako;

        /// <summary>Forgets the per-battle bookkeeping. Called when a battle's results are computed.</summary>
        internal static void ResetBattle()
        {
            if (Armed.Count == 0 && Revived.Count == 0) return;
            Armed.Clear();
            Revived.Clear();
        }

        /// <summary>
        /// Remembers the host a slime just took over, unless that slime already used its one drop in
        /// this battle. Called after SetOccupied so the unit box offset has already been copied over.
        /// </summary>
        internal static void ArmIfEligible(UnitData occupied, int slimeUnitOffs)
        {
            if (slimeUnitOffs < 0) return;

            var slime = BattleUnit(occupied, slimeUnitOffs);
            if (slime == null) return;
            if (SlimeType >= 0 && (int)slime.nType != SlimeType) return;

            int boxOffset = slime.m_nUserUnitOffs;
            if (boxOffset < 0) return;                 // enemy slimes keep the plain death
            if (Revived.Contains(boxOffset)) return;   // one drop per battle

            Armed.Add(boxOffset);
            SlimeFixPlugin.Verbose($"drop on death: unit box {boxOffset} ({Name(slime.nType)}) took a host;"
                                 + " it will drop its body instead of dying with it (once this battle).");
        }

        /// <summary>
        /// Turns a unit that just took a lethal hit back into the slime that possessed it, on the
        /// same cell. Returns false for everything this patch does not own, so the game's own death
        /// handling runs untouched.
        /// </summary>
        internal static bool TryDropBody(UnitData unit)
        {
            int boxOffset = unit.m_nUserUnitOffs;
            if (!IsArmed(boxOffset)) return false;
            if (unit.nHP > 0) return false;            // scripted deaths keep working

            Armed.Remove(boxOffset);
            Revived.Add(boxOffset);

            var slimeType = (UNIT_TYPE)HostType(unit);

            // Everything the game derives from the unit's own state has to be the slime's own data
            // BEFORE ChangeUnitType runs: that method picks the sprite from the occupation state and
            // computes attack and defence from nLevel, so leaving the host's values there drops out a
            // possessed-looking slime wearing the host's level.
            unit.m_nOccupyState = OCCUPY_STATE.normal;
            unit.m_nSuckUnitOffs = -1;
            unit.m_bHyperMode = false;

            // bSlime decides which animation set the unit uses (QUAD_TYPE.slime versus
            // QUAD_TYPE.enemy, and with it the idle frame numbers). Only UnitData.Create sets it,
            // ChangeUnitType does not, so a host has it false; left alone, the sprite sheet is the
            // slime's while the frame numbers are the enemy's, and the unit draws torn.
            unit.bSlime = UnitTypeTable.IsSlimeUnit(slimeType);

            var record = BoxRecord(boxOffset);
            if (record != null)
            {
                unit.nLevel = record.nLevel;
                unit.nAttr = record.nAttr;
            }

            unit.ChangeUnitType(slimeType, 0);      // zeroes the possession stat bonuses itself
            unit.nMAXHP = UnitTypeTable.GetMaxHP(slimeType, unit.nLevel);
            unit.nHP = unit.nMAXHP;
            unit.SetUnitQuadTexture(true);          // the plain slime art, not the possessed variant
            unit.SetUnitAction(UNIT_ACTION.wait);
            unit.m_fUnitActionCounter = 0f;
            unit.SetUnitAnime(unit.GetUnitSpriteDir(), true);

            SlimeFixPlugin.Instance?.Log.LogInfo(
                $"drop on death: unit box {boxOffset} lost its host and is back to {Name(slimeType)}"
              + $" Lv.{unit.nLevel} at cell {unit.nPosX},{unit.nPosZ} with {unit.nHP} HP"
              + $" (quad {unit.m_nQuadType}, idle frame {unit.m_nAnimWaitF})"
              + " - it may possess once more this battle, but only once.");
            return true;
        }

        private static bool IsArmed(int boxOffset) => boxOffset >= 0 && Armed.Contains(boxOffset);

        /// <summary>
        /// Slime type to turn back into. With SlimeType negative (any) it is read from the unit box
        /// record the host belongs to, so the body that drops out is the slime that went in.
        /// </summary>
        private static int HostType(UnitData unit)
        {
            if (SlimeType >= 0) return SlimeType;

            var box = UserUnitTable.DataArray;
            int offs = unit.m_nUserUnitOffs;
            if (box != null && offs >= 0 && offs < box.Length)
            {
                var record = box[offs];
                if (record != null) return (int)record.nType;
            }
            return (int)UNIT_TYPE.s_zako;
        }

        private static UserUnitData BoxRecord(int boxOffset)
        {
            var box = UserUnitTable.DataArray;
            if (box == null || boxOffset < 0 || boxOffset >= box.Length) return null;
            return box[boxOffset];
        }

        private static UnitData BattleUnit(UnitData reference, int offs)
        {
            var battle = reference.BattleFlowComponent;
            if (battle == null) return null;

            var units = battle.UnitDataArray;
            if (units == null || offs >= units.Length) return null;

            return units[offs];
        }

        private static string Name(UNIT_TYPE type)
        {
            try { return Strs.GetUnitTypeName(type); }
            catch (Exception) { return "?"; }
        }
    }

    /// <summary>Remembers the host a slime takes over, so a lethal hit can drop the body instead.</summary>
    [HarmonyPatch(typeof(UnitData), nameof(UnitData.SetOccupied))]
    internal static class Patch_UnitData_SetOccupied_Arm
    {
        private static void Postfix(UnitData __instance, int slimeUnitOffs)
        {
            if (!ReviveOnDeath.Active) return;

            try { ReviveOnDeath.ArmIfEligible(__instance, slimeUnitOffs); }
            catch (Exception e) { SlimeFixPlugin.Warn("drop on death: arming failed: " + e.Message); }
        }
    }

    /// <summary>
    /// Replaces the death of a host this patch armed with the drop back into the slime. Everything
    /// else - including the zombie and skeleton conversions, which never come through here - keeps
    /// the game's own handling.
    /// </summary>
    [HarmonyPatch(typeof(UnitData), nameof(UnitData.StartUnitDead))]
    internal static class Patch_UnitData_StartUnitDead
    {
        private static bool Prefix(UnitData __instance)
        {
            if (!ReviveOnDeath.Active) return true;

            try
            {
                if (!ReviveOnDeath.TryDropBody(__instance))
                {
                    SlimeFixPlugin.Verbose($"drop on death: unit type {__instance.nType} box"
                                         + $" {__instance.m_nUserUnitOffs} died normally.");
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                SlimeFixPlugin.Warn("drop on death: dropping failed: " + e.Message);
                return true;
            }
        }
    }

    /// <summary>
    /// Starts a fresh battle for the once-per-battle bookkeeping, and keeps the battle-end result
    /// pass away from units that have no unit-box record. The pass is entered for every unit whose
    /// side is the slime side, and it indexes UserUnitTable.DataArray[m_nUserUnitOffs]; a unit this
    /// plugin spawned on the player's side without a box record would be an out-of-range index.
    /// </summary>
    [HarmonyPatch(typeof(UnitData), nameof(UnitData.SetBattleResult))]
    internal static class Patch_UnitData_SetBattleResult_Reset
    {
        private static bool Prefix(UnitData __instance)
        {
            if (!ReviveOnDeath.Active) return true;

            ReviveOnDeath.ResetBattle();
            Divide.ResetBattle();
            MimicBattleReset.Reset();
            return __instance.m_nUserUnitOffs >= 0;
        }
    }
}
