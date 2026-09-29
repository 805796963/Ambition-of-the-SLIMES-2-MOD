using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;

namespace SlimesSlimeFix
{
    /// <summary>
    /// Resolves which slime owns the inherit ability and holds the use count the patches write for it.
    ///
    /// The unit table (ExcelUnitData) is loaded lazily by the game, so every lookup here can run
    /// before the table exists. Resolution therefore retries on the next patch invocation instead of
    /// caching a failure, and every re-entrant call made while resolving is served the game's own
    /// value.
    /// </summary>
    internal static class Inherit
    {
        private static int sourceType = -1;
        private static int useCount = 999;
        private static bool resolved;
        private static bool suppress;
        private static bool dumpDone;
        private static bool selfTestDone;
        private static bool warnedNoSource;

        /// <summary>Whether the inherit patches may change anything at all.</summary>
        internal static bool Active =>
            SlimeFixPlugin.CfgEnabled != null && SlimeFixPlugin.CfgEnabled.Value
            && SlimeFixPlugin.CfgInheritEnabled != null && SlimeFixPlugin.CfgInheritEnabled.Value;

        /// <summary>True while this class is reading the game's own values instead of overriding them.</summary>
        internal static bool Suppress => suppress;

        /// <summary>The use count written for the inherit slime.</summary>
        internal static int UseCount => useCount;

        /// <summary>Whether <paramref name="type"/> is the slime that owns the inherit ability.</summary>
        internal static bool IsSource(UNIT_TYPE type) => resolved && (int)type == sourceType;

        /// <summary>Logs one override so [General] VerboseLog shows what changed.</summary>
        internal static void NoteOverride(UNIT_TYPE type, string what, object before, object after) =>
            SlimeFixPlugin.Verbose($"inherit: {what} of type {type} '{Name(type)}' {before} -> {after}");

        /// <summary>Reads the configuration into this class. Called once from plugin load.</summary>
        internal static void Configure()
        {
            resolved = false;
            sourceType = SlimeFixPlugin.CfgInheritSourceType.Value;
            useCount = Math.Max(1, SlimeFixPlugin.CfgInheritUseCount.Value);
            warnedNoSource = false;
        }

        /// <summary>
        /// Resolves the inherit slime. Returns false while the unit table is still unreadable, so the
        /// caller leaves the game's own value alone and tries again later.
        /// </summary>
        internal static bool Resolve()
        {
            if (resolved) return true;
            if (suppress) return false;

            suppress = true;
            try
            {
                if (sourceType <= 0)
                {
                    // The slime that already owns the ability is the one whose count is raised.
                    int max = (int)UNIT_TYPE.max;
                    for (int t = 1; t < max; t++)
                    {
                        if (!UnitTypeTable.IsSlimeUnit((UNIT_TYPE)t)) continue;
                        if ((int)UnitTypeTable.GetAbilityType((UNIT_TYPE)t) == (int)ABILITY_TYPE.ocuupy_keep)
                        {
                            sourceType = t;
                            break;
                        }
                    }

                    if (sourceType <= 0)
                    {
                        if (!warnedNoSource)
                        {
                            warnedNoSource = true;
                            SlimeFixPlugin.Warn("Inherit: no slime type reports ABILITY_TYPE.ocuupy_keep; set"
                                              + " [Inherit] SourceType explicitly. Nothing is being changed.");
                        }
                        return false;
                    }
                }

                resolved = true;
                SlimeFixPlugin.Instance?.Log.LogInfo(
                    $"Inherit ready: type {sourceType} '{Name((UNIT_TYPE)sourceType)}'"
                  + $" ability '{AbilityName(UnitTypeTable.GetAbilityType((UNIT_TYPE)sourceType))}',"
                  + $" ability count raised to {useCount}; no other slime is touched.");
                return true;
            }
            catch (Exception)
            {
                // The unit table is not loaded yet; the next call retries.
                return false;
            }
            finally
            {
                suppress = false;
            }
        }

        /// <summary>
        /// Writes the one-shot unit-table dump requested by [Diagnostics] DumpUnitTable, while
        /// <see cref="suppress"/> is set so the dump shows the game's shipped values.
        /// </summary>
        internal static void DumpIfRequested()
        {
            if (dumpDone) return;
            if (SlimeFixPlugin.CfgDumpUnitTable == null || !SlimeFixPlugin.CfgDumpUnitTable.Value)
            {
                dumpDone = true;
                return;
            }

            bool outer = suppress;
            suppress = true;
            try
            {
                var text = new StringBuilder();
                text.AppendLine("UNIT_TYPE dump (number / name / slime flag / shipped ability / shipped count):");
                int max = (int)UNIT_TYPE.max;
                for (int t = 1; t < max; t++)
                {
                    var type = (UNIT_TYPE)t;
                    bool slime = UnitTypeTable.IsSlimeUnit(type);
                    var ability = UnitTypeTable.GetAbilityType(type);
                    text.AppendLine($"  {t,2}  {Strs.GetUnitTypeName(type),-14} slime={slime,-5}"
                                  + $" attr={DescribeAttribute(UnitTypeTable.GetAttrType(type))} "
                                  + $" ability={Strs.GetAbilityName(ability),-16} count={UnitTypeTable.GetAbilityNum(type)}");
                }

                SlimeFixPlugin.Instance?.Log.LogInfo(text.ToString());
                dumpDone = true;
            }
            catch (Exception)
            {
                // Unit table not readable yet; try again on the next call.
            }
            finally
            {
                suppress = outer;
            }
        }

        /// <summary>
        /// One-shot proof, requested by [Diagnostics] SelfTest, that exactly one slime changed: it
        /// prints the shipped and the patched ability count of every slime type, and runs the real
        /// battle-result step on a detached unit-box record to show the carrying count each stores.
        /// </summary>
        internal static void SelfTestIfRequested()
        {
            if (selfTestDone) return;
            if (SlimeFixPlugin.CfgSelfTest == null || !SlimeFixPlugin.CfgSelfTest.Value)
            {
                selfTestDone = true;
                return;
            }

            selfTestDone = true;
            bool outer = suppress;
            try
            {
                var text = new StringBuilder();
                text.AppendLine("self-test - ability count of every slime type (shipped -> patched):");

                int max = (int)UNIT_TYPE.max;
                for (int t = 1; t < max; t++)
                {
                    var type = (UNIT_TYPE)t;
                    if (!UnitTypeTable.IsSlimeUnit(type)) continue;

                    suppress = true;
                    int shipped = UnitTypeTable.GetAbilityNum(type);
                    var ability = UnitTypeTable.GetAbilityType(type);
                    suppress = false;
                    int patched = UnitTypeTable.GetAbilityNum(type);

                    text.AppendLine($"  {t,2} {Name(type),-14} {shipped,4} -> {patched,-4}"
                                  + $" ability={AbilityName(ability),-12}"
                                  + $" attr={DescribeAttribute(UnitTypeTable.GetAttrType(type))}"
                                  + (shipped != patched ? "   <-- changed" : string.Empty));
                }

                text.AppendLine("self-test - unit box record after one possession, detached from the game:");
                foreach (var type in TypesOfInterest())
                    text.AppendLine("  " + ProbeCarrying((UNIT_TYPE)type));

                SlimeFixPlugin.Instance?.Log.LogInfo(text.ToString());
            }
            catch (Exception e)
            {
                SlimeFixPlugin.Warn("self-test failed: " + e.Message);
            }
            finally
            {
                suppress = outer;
            }
        }

        /// <summary>The inherit slime plus three slimes the patch must leave alone.</summary>
        private static List<int> TypesOfInterest() => new List<int> { sourceType, 1, 17, 23 };

        private static string ProbeCarrying(UNIT_TYPE type)
        {
            var record = new UserUnitData();
            record.nType = type;
            record.nAdd_abilityNum = 0;
            record.nLevel = 5;

            suppress = true;
            int shipped = record.GetAbilityNum();
            suppress = false;
            int patched = record.GetAbilityNum();

            record.SetBattleResult_occupying((UNIT_TYPE)24, 5, ATTR_TYPE.none, (UNIT_TYPE)24, false);

            return $"{type,2} {Name(type),-14} box count {shipped,3} -> {patched,-3}"
                 + $" after one possession nOccupyingTurn={record.nOccupyingTurn,-3}"
                 + $", kept {record.nOccupyUnitType}";
        }

        private static string Name(UNIT_TYPE type)
        {
            try { return Strs.GetUnitTypeName(type); }
            catch (Exception) { return "?"; }
        }

        private static string AbilityName(ABILITY_TYPE ability)
        {
            try { return Strs.GetAbilityName(ability); }
            catch (Exception) { return "?"; }
        }

        private static string DescribeAttribute(ATTR_TYPE attribute)
        {
            try { return $"{(int)attribute}:{EtcFunc.GetAttrSpriteName(attribute)}"; }
            catch (Exception) { return ((int)attribute).ToString(); }
        }
    }

    /// <summary>
    /// Raises the ability count the unit table reports for the inherit slime. Only that one type is
    /// affected; every other type keeps the value the game ships.
    /// </summary>
    [HarmonyPatch(typeof(UnitTypeTable), nameof(UnitTypeTable.GetAbilityNum))]
    internal static class Patch_UnitTypeTable_GetAbilityNum
    {
        private static void Postfix(UNIT_TYPE type, ref int __result)
        {
            // A divided child needs a split use of its own, otherwise the second generation is the
            // last one for a reason the player never sees.
            if (Divide.Active && (int)type == Divide.ChildType)
            {
                __result = Divide.ChildSplits;
                return;
            }

            if (!Inherit.Active || Inherit.Suppress) return;

            Inherit.DumpIfRequested();
            if (!Inherit.Resolve()) return;
            Inherit.SelfTestIfRequested();
            if (!Inherit.IsSource(type)) return;

            Inherit.NoteOverride(type, "ability count", __result, Inherit.UseCount);
            __result = Inherit.UseCount;
        }
    }

    /// <summary>
    /// Gives a divided child the split ability. Every generation carries UNIT_TYPE.s_divided, so
    /// without this the child's data still describes a unit that has no ability at all and its split
    /// use would have no entry in the command window.
    /// </summary>
    [HarmonyPatch(typeof(UnitTypeTable), nameof(UnitTypeTable.GetAbilityType))]
    internal static class Patch_UnitTypeTable_GetAbilityType
    {
        private static void Postfix(UNIT_TYPE type, ref ABILITY_TYPE __result)
        {
            if (!Divide.Active) return;
            if ((int)type != Divide.ChildType) return;

            __result = Divide.ChildAbility;
        }
    }

    /// <summary>
    /// Reports the raised count for the inherit slime's unit-box record, which is what the unit list,
    /// the unit-info panel and the battle unit's ability count are read from.
    /// </summary>
    [HarmonyPatch(typeof(UserUnitData), nameof(UserUnitData.GetAbilityNum))]
    internal static class Patch_UserUnitData_GetAbilityNum
    {
        private static void Postfix(UserUnitData __instance, ref int __result)
        {
            if (!Inherit.Active || Inherit.Suppress) return;
            if (!Inherit.Resolve() || !Inherit.IsSource(__instance.nType)) return;

            Inherit.NoteOverride(__instance.nType, "unit box ability count", __result, Inherit.UseCount);
            __result = Inherit.UseCount;
        }
    }

    /// <summary>
    /// Applies the inherit step to the inherit slime when a battle ends.
    ///
    /// The game stores a hardcoded 1 + nAdd_abilityNum here, which is the count that decides for how
    /// many battles the possessed unit is carried. Re-running the same store with the configured count
    /// raises that carrying count too, so the ability count shown in the unit list and the number of
    /// battles it actually lasts agree. Slimes without the inherit ability never reach this step and
    /// are left alone.
    /// </summary>
    [HarmonyPatch(typeof(UserUnitData), nameof(UserUnitData.SetBattleResult_occupying))]
    internal static class Patch_UserUnitData_SetBattleResult_occupying
    {
        private static void Postfix(UserUnitData __instance, UNIT_TYPE __0, int __1, ATTR_TYPE __2, UNIT_TYPE __3)
        {
            if (!Inherit.Active || Inherit.Suppress) return;
            if (!Inherit.Resolve() || !Inherit.IsSource(__instance.nType)) return;

            Inherit.NoteOverride(__instance.nType, $"keeps {__0}", __instance.nOccupyingTurn, Inherit.UseCount);
            __instance.SetOccupying(Inherit.UseCount, __0, __1, __2, __3);
        }
    }
}
