using System;
using System.Collections.Generic;
using HarmonyLib;

namespace SlimesSlimeFix
{
    /// <summary>
    /// Bounds how often a divided slime may split again.
    ///
    /// BattleFlow.DivideSlime creates the child as UNIT_TYPE.s_divided, so every generation shares one
    /// type and the data table cannot tell them apart; the generation is therefore tracked per battle
    /// offset. Splitting is also how a divided slime reacts to being hit - UnitData.IsAbleDivide is
    /// what the damage handling asks before it calls StartUnitDivide - so the limit cannot be a plain
    /// "you already split" flag, or the slime stops fighting back.
    ///
    /// Each divided unit therefore holds an allowance of splits: the deployed slime starts with one,
    /// a child gets the configured amount, and the generation after that gets none. Every split spends
    /// one. Nothing refills it, so a battle can only ever hold the deployed slime, its child and that
    /// child's child.
    ///
    /// The unit quad is tinted to match: a unit that can still split is drawn at full opacity and one
    /// that cannot is drawn faint, so the state is visible on the field.
    ///
    /// INFINITE_DIVIDE builds the variant with no allowance at all.
    /// </summary>
    internal static class Divide
    {
        /// <summary>Generation of each unit DivideSlime created this battle, by battle offset.</summary>
        private static readonly Dictionary<int, int> Generation = new Dictionary<int, int>();

        /// <summary>Splits each unit may still take, by battle offset.</summary>
        private static readonly Dictionary<int, int> Allowance = new Dictionary<int, int>();

        /// <summary>Whether the allowance rules are in effect.</summary>
        internal static bool Active =>
            SlimeFixPlugin.CfgEnabled != null && SlimeFixPlugin.CfgEnabled.Value
            && SlimeFixPlugin.CfgDivideEnabled != null && SlimeFixPlugin.CfgDivideEnabled.Value;

        /// <summary>Whether the allowance is bounded at all. False in the INFINITE_DIVIDE build.</summary>
        internal static bool LimitGenerations
        {
#if INFINITE_DIVIDE
            get { return false; }
#else
            get { return true; }
#endif
        }

        /// <summary>Splits a freshly divided child starts with.</summary>
        internal static int ChildSplits => SlimeFixPlugin.CfgDivideChildSplits?.Value ?? 1;

        /// <summary>HP percentage below which a divided unit refuses to split.</summary>
        internal static int MinHpPercent => SlimeFixPlugin.CfgDivideMinHpPercent?.Value ?? 20;

        /// <summary>Quad alpha of a divided unit that can still split.</summary>
        internal static float CanSplitAlpha =>
            Math.Max(0f, Math.Min(1f, SlimeFixPlugin.CfgDivideCanSplitAlpha?.Value ?? 1f));

        /// <summary>Quad alpha of a divided unit that cannot split any more.</summary>
        internal static float CannotSplitAlpha =>
            Math.Max(0f, Math.Min(1f, SlimeFixPlugin.CfgDivideCannotSplitAlpha?.Value ?? 0.8f));

        /// <summary>UNIT_TYPE of a divided child. Every generation carries it.</summary>
        internal static int ChildType => (int)UNIT_TYPE.s_divided;

        /// <summary>ABILITY_TYPE a child splits with, so its ability entry exists in the window.</summary>
        internal static ABILITY_TYPE ChildAbility => ABILITY_TYPE.divide;

        /// <summary>Forgets everything recorded for a battle.</summary>
        internal static void ResetBattle()
        {
            Generation.Clear();
            Allowance.Clear();
        }

        private static int GenerationOf(int offs)
        {
            int generation;
            return Generation.TryGetValue(offs, out generation) ? generation : 0;
        }

        /// <summary>Splits the unit may still take: none past the limited generations, else its allowance.</summary>
        internal static int AllowanceOf(UnitData unit)
        {
            if (unit == null) return 0;
            if (!LimitGenerations) return int.MaxValue;
            if (GenerationOf(unit.myOffs) >= 2) return 0;

            int allowance;
            return Allowance.TryGetValue(unit.myOffs, out allowance) ? Math.Max(0, allowance) : ChildSplits;
        }

        private static void SetAllowance(UnitData unit, int value)
        {
            if (unit == null) return;
            Allowance[unit.myOffs] = Math.Max(0, value);
        }

        /// <summary>Whether the unit may split right now.</summary>
        internal static bool CanSplit(UnitData unit)
        {
            if (unit == null) return false;
            if (!Active) return true;
            if ((int)unit.nType != ChildType) return true;
            if (!LimitGenerations) return true;
            if (AllowanceOf(unit) <= 0) return false;

            int minPercent = MinHpPercent;
            if (minPercent > 0 && unit.nMAXHP > 0)
            {
                int percent = (int)((long)unit.nHP * 100 / unit.nMAXHP);
                if (percent < minPercent) return false;
            }

            return true;
        }

        /// <summary>
        /// What the game's own divide validator answers. A divided slime spends this to react to being
        /// hit as well as to divide on its own turn, so refusing it also stops the slime fighting back.
        /// </summary>
        internal static bool IsAbleDivide(UnitData unit)
        {
            if (unit == null) return false;
            if (!Active) return true;
            if ((int)unit.nType != ChildType) return true;

            bool allowed = CanSplit(unit);
            if (!allowed)
            {
                SlimeFixPlugin.Verbose($"divide: {Name((UNIT_TYPE)unit.nType)} at cell"
                                     + $" {unit.nPosX},{unit.nPosZ} refused a split (HP"
                                     + $" {unit.nHP}/{unit.nMAXHP}, allowance {AllowanceOf(unit)},"
                                     + $" generation {GenerationOf(unit.myOffs)}).");
            }

            return allowed;
        }

        /// <summary>
        /// Refreshes a divided unit at the start of its turn. The allowance is fixed for the battle,
        /// so this only re-states the ability and the tint in case the game changed them.
        /// </summary>
        internal static void TurnStart(UnitData unit)
        {
            if (unit == null) return;
            if (!Active) return;
            if ((int)unit.nType != ChildType) return;

            Refresh(unit);
        }

        /// <summary>Makes the unit's ability and tint agree with what it may still do.</summary>
        internal static void Refresh(UnitData unit)
        {
            if (unit == null) return;
            if (!Active) return;
            if ((int)unit.nType != ChildType) return;

            bool canSplit = CanSplit(unit);

            if (canSplit)
            {
                if (unit.m_nAbility != ChildAbility) unit.m_nAbility = ChildAbility;
                int allowance = LimitGenerations ? AllowanceOf(unit) : ChildSplits;
                unit.m_nAbilityCount = allowance;
                unit.m_nAbilityCountMax = allowance;
            }
            else
            {
                unit.m_nAbility = 0;
                unit.m_nAbilityCount = 0;
                unit.m_nAbilityCountMax = 0;
            }

            unit.SetUnitQuadTransparent(canSplit ? CanSplitAlpha : CannotSplitAlpha);
        }

        /// <summary>
        /// Applies the allowance to a child BattleFlow.DivideSlime just created and spends the parent's.
        /// </summary>
        internal static void Apply(BattleFlow battle, int parentOffs, int childOffs)
        {
            var units = battle == null ? null : battle.UnitDataArray;
            if (units == null) return;
            if (parentOffs < 0 || parentOffs >= units.Length) return;
            if (childOffs < 0 || childOffs >= units.Length) return;

            var parent = units[parentOffs];
            var child = units[childOffs];
            if (parent == null || child == null) return;

            int generation = GenerationOf(parentOffs) + 1;
            Generation[childOffs] = generation;

            int left = AllowanceOf(parent);
            if (left != int.MaxValue) SetAllowance(parent, left - 1);

            if (LimitGenerations && generation >= 2) SetAllowance(child, 0);
            else SetAllowance(child, ChildSplits);

            Refresh(parent);
            Refresh(child);

            if (LimitGenerations && generation >= 2)
            {
                SlimeFixPlugin.Instance?.Log.LogInfo(
                    $"divide: parent slot {parentOffs} (generation {generation - 1}) at cell"
                  + $" {parent.nPosX},{parent.nPosZ} produced generation {generation}"
                  + $" {Name((UNIT_TYPE)child.nType)} Lv.{child.nLevel} at cell {child.nPosX},{child.nPosZ},"
                  + $" which cannot split again; {AllowanceOf(parent)} split(s) left for the parent.");
                return;
            }

            SlimeFixPlugin.Verbose($"divide: parent slot {parentOffs} (generation {generation - 1}) at"
                                 + $" cell {parent.nPosX},{parent.nPosZ} produced generation {generation}"
                                 + $" {Name((UNIT_TYPE)child.nType)} Lv.{child.nLevel}, which may split"
                                 + $" {AllowanceOf(child)} more time(s); parent has {AllowanceOf(parent)} left.");
        }

        private static string Name(UNIT_TYPE type)
        {
            try { return Strs.GetUnitTypeName(type); }
            catch (Exception) { return "?"; }
        }
    }

    /// <summary>
    /// Gives the game's own divide validator the allowance and health rules, so the command window,
    /// the AI and the reaction to being hit all agree with the tint on the field.
    /// </summary>
    [HarmonyPatch(typeof(UnitData), nameof(UnitData.IsAbleDivide))]
    internal static class Patch_UnitData_IsAbleDivide
    {
        private static void Postfix(UnitData __instance, ref bool __result)
        {
            if (!__result) return;
            if (!Divide.Active) return;

            try
            {
                if (!Divide.IsAbleDivide(__instance)) __result = false;
            }
            catch (Exception e) { SlimeFixPlugin.Warn("divide: validating a split failed: " + e.Message); }
        }
    }

    /// <summary>Refuses the split at the source as well, in case a path skips the validator.</summary>
    [HarmonyPatch(typeof(BattleFlow), nameof(BattleFlow.DivideSlime))]
    internal static class Patch_BattleFlow_DivideSlime
    {
        private static bool Prefix(BattleFlow __instance, int __0, ref int __result)
        {
            if (!Divide.Active) return true;

            try
            {
                var units = __instance == null ? null : __instance.UnitDataArray;
                if (units == null || __0 < 0 || __0 >= units.Length) return true;

                if (!Divide.IsAbleDivide(units[__0]))
                {
                    __result = -1;
                    return false;
                }
            }
            catch (Exception e) { SlimeFixPlugin.Warn("divide: refusing a split failed: " + e.Message); }

            return true;
        }

        private static void Postfix(BattleFlow __instance, int __0, int __result)
        {
            if (!Divide.Active) return;

            try { Divide.Apply(__instance, __0, __result); }
            catch (Exception e) { SlimeFixPlugin.Warn("divide: applying the allowance failed: " + e.Message); }
        }
    }

    /// <summary>Refreshes the ability and the tint whenever a turn starts.</summary>
    [HarmonyPatch(typeof(UnitData), nameof(UnitData.TurnStartCheck))]
    internal static class Patch_UnitData_TurnStartCheck
    {
        private static void Postfix(UnitData __instance)
        {
            if (!Divide.Active) return;

            try { Divide.TurnStart(__instance); }
            catch (Exception e) { SlimeFixPlugin.Warn("divide: the turn-start roll failed: " + e.Message); }
        }
    }

    /// <summary>
    /// Keeps the ability and the tint right when the game recomputes the unit's ability from the unit
    /// table or the unit-box record.
    /// </summary>
    [HarmonyPatch(typeof(UnitData), nameof(UnitData.SetAbilityParam))]
    internal static class Patch_UnitData_SetAbilityParam
    {
        private static void Postfix(UnitData __instance)
        {
            if (!Divide.Active) return;

            try { Divide.Refresh(__instance); }
            catch (Exception e) { SlimeFixPlugin.Warn("divide: refreshing a unit failed: " + e.Message); }
        }
    }
}
