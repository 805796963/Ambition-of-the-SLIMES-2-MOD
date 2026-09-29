using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SlimesSlimeFix
{
    /// <summary>
    /// Gives the zako slime a mimicry ability: it copies any unit on the field, keeps that unit's
    /// skills, and lives with it for the rest of the battle.
    ///
    /// ABILITY_TYPE has no free member to add, so the ability is the anti-magic slime's shield
    /// breaker (ABILITY_TYPE.c_antiprotection) borrowed as a shell: the zako's ability entry shows
    /// that skill, and BattleFlow.UseAbility is taken over so using it mimics instead. The shield
    /// break is kept as a chance, which is what the shell is for.
    ///
    /// The copy is not a half-strength stat block but a level: BattleFlow.UseAbility changes the unit
    /// type and the level is set to a configured percentage of the copied unit's, so HP, attack and
    /// defence follow from the game's own level tables, exactly as they do for any other unit.
    ///
    /// UnitData.Create and UnitData.ChangeUnitType do not touch the animation group, so bSlime is set
    /// from the copied type before the switch or the sprite samples the wrong animation frames.
    /// </summary>
    internal static class Mimic
    {
        /// <summary>
        /// The battle seen by the last ability hook, so the static ability-name lookup can tell which
        /// unit is acting.
        /// </summary>
        internal static BattleFlow LastBattle;

        /// <summary>
        /// The unit-box record as it was before the copy, keyed by the box offset. The game writes the
        /// battle outcome straight into that record, and a shape that is not the slime must not survive
        /// there: the next battle would deploy the shape instead of the slime.
        /// </summary>
        private struct BoxSnapshot
        {
            internal int Type;
            internal int Level;
            internal int Attr;
            internal int OccupyingTurn;
            internal int OccupyUnitType;
            internal int OccupyUnitLevel;
            internal int OccupyUnitAttr;
            internal int OccupyUnitOriginalType;
        }

        private static readonly Dictionary<int, BoxSnapshot> SavedBoxes = new Dictionary<int, BoxSnapshot>();

        /// <summary>Whether this battle unit is a shape this plugin created.</summary>
        internal static bool IsMimicUnit(UnitData unit)
        {
            return unit != null && Mimicked.Contains(unit.myOffs);
        }

        /// <summary>Remembers the unit box a copy is about to be built for.</summary>
        private static void SaveBox(UnitData caster)
        {
            try
            {
                if (caster == null || caster.m_nUserUnitOffs < 0) return;

                var records = UserUnitTable.DataArray;
                if (records == null) return;
                if (caster.m_nUserUnitOffs >= records.Length) return;

                var record = records[caster.m_nUserUnitOffs];
                if (record == null) return;

                SavedBoxes[caster.m_nUserUnitOffs] = new BoxSnapshot
                {
                    Type = (int)record.nType,
                    Level = record.nLevel,
                    Attr = (int)record.nAttr,
                    OccupyingTurn = record.nOccupyingTurn,
                    OccupyUnitType = (int)record.nOccupyUnitType,
                    OccupyUnitLevel = record.nOccupyUnitLevel,
                    OccupyUnitAttr = (int)record.nOccupyUnitAttr,
                    OccupyUnitOriginalType = (int)record.nOccupyUnitOriginalType,
                };
            }
            catch (Exception e) { SlimeFixPlugin.Warn("mimic: reading the unit box failed: " + e.Message); }
        }

        /// <summary>Puts the slime's own record back, undoing whatever the battle wrote into it.</summary>
        internal static void RestoreBox(UnitData unit)
        {
            if (unit == null) return;
            if (unit.m_nUserUnitOffs < 0) return;

            BoxSnapshot snapshot;
            if (!SavedBoxes.TryGetValue(unit.m_nUserUnitOffs, out snapshot)) return;

            try
            {
                var records = UserUnitTable.DataArray;
                if (records == null || unit.m_nUserUnitOffs >= records.Length) return;

                var record = records[unit.m_nUserUnitOffs];
                if (record == null) return;

                record.nType = (UNIT_TYPE)snapshot.Type;
                record.nLevel = snapshot.Level;
                record.nAttr = (ATTR_TYPE)snapshot.Attr;
                record.nOccupyingTurn = snapshot.OccupyingTurn;
                record.nOccupyUnitType = (UNIT_TYPE)snapshot.OccupyUnitType;
                record.nOccupyUnitLevel = snapshot.OccupyUnitLevel;
                record.nOccupyUnitAttr = (ATTR_TYPE)snapshot.OccupyUnitAttr;
                record.nOccupyUnitOriginalType = (UNIT_TYPE)snapshot.OccupyUnitOriginalType;

                SavedBoxes.Remove(unit.m_nUserUnitOffs);
                SlimeFixPlugin.Instance?.Log.LogInfo(
                    $"mimic: unit box {unit.m_nUserUnitOffs} restored to {Name((UNIT_TYPE)snapshot.Type)}"
                  + $" Lv.{snapshot.Level}, so the next battle deploys the slime and not the shape.");
            }
            catch (Exception e) { SlimeFixPlugin.Warn("mimic: restoring the unit box failed: " + e.Message); }
        }

        /// <summary>Units that already mimicked this battle, by battle offset.</summary>
        private static readonly HashSet<int> Mimicked = new HashSet<int>();

        /// <summary>
        /// Copy asked for by an ability use and not applied yet, caster offset to target offset. The
        /// type change waits for the caster's next turn because doing it while the ability's own
        /// animation is playing makes the animation sample the new sheet with the old frame numbers.
        /// </summary>
        private static readonly Dictionary<int, int> Pending = new Dictionary<int, int>();

        internal static bool Active =>
            SlimeFixPlugin.CfgEnabled != null && SlimeFixPlugin.CfgEnabled.Value
            && SlimeFixPlugin.CfgMimicEnabled != null && SlimeFixPlugin.CfgMimicEnabled.Value;

        /// <summary>UNIT_TYPE that may mimic.</summary>
        internal static int SlimeType => SlimeFixPlugin.CfgMimicSlimeType?.Value ?? (int)UNIT_TYPE.s_zako;

        /// <summary>UNIT_TYPE whose ability is borrowed as the shell.</summary>
        internal static int ShellType => SlimeFixPlugin.CfgMimicShellType?.Value ?? (int)UNIT_TYPE.s_antimagic;

        /// <summary>Percent of the copied unit's level the mimic ends up at.</summary>
        internal static int LevelPercent => Math.Max(1, Math.Min(100, SlimeFixPlugin.CfgMimicLevelPercent?.Value ?? 70));

        /// <summary>Percent chance that the borrowed shell also breaks the target's shield.</summary>
        internal static int ShieldBreakPercent =>
            Math.Max(0, Math.Min(100, SlimeFixPlugin.CfgMimicShieldBreak?.Value ?? 5));

        /// <summary>Tint drawn over a mimicked unit, so it is recognisable on the field.</summary>
        internal static Color Tint => new Color(
            SlimeFixPlugin.CfgMimicTintR?.Value ?? 0.25f,
            SlimeFixPlugin.CfgMimicTintG?.Value ?? 1f,
            SlimeFixPlugin.CfgMimicTintB?.Value ?? 0.25f,
            1f);

        /// <summary>ABILITY_TYPE borrowed as the shell, read from the anti-magic slime.</summary>
        internal static ABILITY_TYPE ShellAbility
        {
            get
            {
                try { return UnitTypeTable.GetAbilityType((UNIT_TYPE)ShellType); }
                catch (Exception) { return ABILITY_TYPE.none; }
            }
        }

        /// <summary>Whether this unit is a mimic caster that has not mimicked yet.</summary>
        internal static bool IsCaster(UnitData unit)
        {
            if (!Active || unit == null) return false;
            if ((int)unit.nType != SlimeType) return false;
            if (Mimicked.Contains(unit.myOffs)) return false;

            ABILITY_TYPE shell = ShellAbility;
            return shell != ABILITY_TYPE.none && unit.m_nAbility == shell;
        }

        /// <summary>
        /// UNIT_TYPE values the mimic refuses. The split slimes are on it by default: a copy of one can
        /// split as well, which is far stronger than a slime that only copies.
        /// </summary>
        internal static bool IsForbidden(UNIT_TYPE type)
        {
            string configured = SlimeFixPlugin.CfgMimicForbidTypes?.Value;
            if (string.IsNullOrEmpty(configured)) return false;

            foreach (string part in configured.Split(','))
            {
                int value;
                if (!int.TryParse(part.Trim(), out value)) continue;
                if (value == (int)type) return true;
            }
            return false;
        }

        /// <summary>
        /// Whether a unit-box record belongs to a slime whose shape this plugin copied. The record is
        /// matched by the battle unit it currently points at.
        /// </summary>
        internal static bool IsMimicBody(UserUnitData record)
        {
            if (record == null) return false;

            int offs = record.nUnitDataOffs;
            return offs >= 0 && Mimicked.Contains(offs);
        }

        /// <summary>Forgets the battle's mimics.</summary>
        internal static void ResetBattle()
        {
            Mimicked.Clear();
            Pending.Clear();
            SavedBoxes.Clear();
        }

        /// <summary>
        /// Records the copy the ability just asked for.
        ///
        /// The request is keyed by caster and applied from the ability call itself, where the acting
        /// unit is unambiguous. A deferred queue keyed on "whoever is not acting" applied another
        /// slime's request, so a slime asked to copy a slime came out as that other slime's target.
        /// </summary>
        internal static void Request(int casterOffs, int targetOffs)
        {
            Pending[casterOffs] = targetOffs;
        }

        /// <summary>Applies the request of one specific caster, right after its ability ran.</summary>
        internal static void ApplyFor(BattleFlow battle, int casterOffs)
        {
            int targetOffs;
            if (!Pending.TryGetValue(casterOffs, out targetOffs)) return;

            Pending.Remove(casterOffs);
            if (Copy(battle, casterOffs, targetOffs)) return;

            SlimeFixPlugin.Warn("mimic: the copy could not be applied.");
        }

        /// <summary>
        /// Applies a requested copy once the acting unit's action has finished.
        ///
        /// Applying it while the turn start was being processed left the battle unable to continue:
        /// destroying the acting unit there left the flow pointing at an empty slot. An action that
        /// has just been closed is the point between two units, which is where the digestion feature
        /// also changes the field.
        /// </summary>
        internal static void ApplyPending(BattleFlow battle)
        {
            var units = battle == null ? null : battle.UnitDataArray;
            if (units == null) return;
            if (Pending.Count == 0) return;

            int current = battle.nCurrentUnitOffs;

            foreach (var entry in new List<KeyValuePair<int, int>>(Pending))
            {
                int casterOffs = entry.Key;

                // Not the unit whose action just ended: the shape must not be built while that unit is
                // still being processed.
                if (casterOffs == current) continue;

                if (casterOffs < 0 || casterOffs >= units.Length) { Pending.Remove(casterOffs); continue; }

                var caster = units[casterOffs];
                if (caster == null || !caster.IsValidUnit()) { Pending.Remove(casterOffs); continue; }

                Pending.Remove(casterOffs);
                if (!Copy(battle, casterOffs, entry.Value))
                    SlimeFixPlugin.Warn("mimic: the pending copy could not be applied.");
                return;
            }
        }

        /// <summary>
        /// Turns the caster into a copy of the target. Returns false when the target cannot be copied.
        /// </summary>
        internal static bool Copy(BattleFlow battle, int casterOffs, int targetOffs)
        {
            var units = battle == null ? null : battle.UnitDataArray;
            if (units == null) return false;
            if (casterOffs < 0 || casterOffs >= units.Length) return false;
            if (targetOffs < 0 || targetOffs >= units.Length) return false;
            if (casterOffs == targetOffs) return false;

            var caster = units[casterOffs];
            var target = units[targetOffs];
            if (caster == null || target == null) return false;
            if (!target.IsValidUnit()) return false;

            SaveBox(caster);

            var copiedType = target.nType;
            int level = Math.Max(1, (int)((long)target.nLevel * LevelPercent / 100));

            if (IsForbidden(copiedType))
            {
                SlimeFixPlugin.Instance?.Log.LogInfo(
                    $"mimic: refused to copy {Name(copiedType)} Lv.{target.nLevel}; that type is on the"
                  + " forbidden list.");
                return false;
            }

            // The shape is a body of the copied type that the slime takes over, built the same way the
            // digestion feature builds its skeleton. ChangeUnitType on the live slime instead left the
            // unit in a state the per-frame update walked off the end of, and it cannot produce the
            // possessed entry a non-slime body needs.
            // The shape is stacked on the slime's own cell, which is what the caller asked for; the
            // slime's body is then removed, so nothing is actually sharing the cell afterwards.
            int spawnX = caster.nPosX, spawnZ = caster.nPosZ;

            int created = battle.CreateNewUnit(copiedType, target.nAttr, level, spawnX, spawnZ,
                                               caster.nLookDir, -1, -1, copiedType);
            if (created < 0)
            {
                SlimeFixPlugin.Warn("mimic: no vacant unit slot, the slime stays as it is.");
                return false;
            }

            var body = battle.UnitDataArray[created];
            body.SetUnitPos(spawnX, spawnZ);

            // Only a shape whose species is not a slime needs to be possessed: that state is what makes
            // a non-slime fight for the player. A slime shape is ours by species, and keeping it out of
            // the occupied state is also what leaves it its own slime actions, suck and possession
            // among them.
            if (UnitTypeTable.IsSlimeUnit(copiedType))
            {
                body.m_nSideType = SIDE_TYPE.slime;
                body.m_nOccupyState = OCCUPY_STATE.normal;
                body.m_nUserUnitOffs = caster.m_nUserUnitOffs;
                body.m_nBattleInfoOffs = caster.m_nBattleInfoOffs;
                body.bSlime = true;
            }
            else
            {
                body.SetOccupied(casterOffs);   // ours, possessed, and linked to the slime's unit box

                // An occupied body is asked which slime is inside it, and that offset is used to index
                // the unit array. Leaving it at -1 is what made the game throw while ending the action.
                body.m_nSuckUnitOffs = casterOffs;
            }

            // SetUserUnitParam points the unit box at the body. That is needed only when the slime's own
            // body is removed, and it is also what made a later battle deploy the copied shape from the
            // box: the box then holds the shape instead of the slime. With the slime kept, the box stays
            // on the slime and the body shares it, which is the arrangement the digestion feature uses.
            bool replaceCaster = SlimeFixPlugin.CfgMimicReplaceCaster != null && SlimeFixPlugin.CfgMimicReplaceCaster.Value;
            if (replaceCaster) body.SetUserUnitParam();

            // The copied type's own skill set first, then the target's own ability is written over it.
            // Staying with the type's entry left some copies without a skill and others with zero uses,
            // because the type data does not always carry a usable ability.
            body.SetAbilityParam();

            try
            {
                body.m_nAbility = target.m_nAbility;
                body.m_nAbilityLen = target.m_nAbilityLen;
                body.m_nAbilityRange = target.m_nAbilityRange;
                body.m_nAbilityCommandType = target.m_nAbilityCommandType;
                body.m_nAbilityTargetType = target.m_nAbilityTargetType;
                body.m_nAbilityTime = target.m_nAbilityTime;
                body.m_nAbilityRegType = target.m_nAbilityRegType;

                int uses = Math.Max(1, target.m_nAbilityCount);
                body.m_nAbilityCount = uses;
                body.m_nAbilityCountMax = uses;

                SlimeFixPlugin.Verbose($"mimic: copied ability '{Strs.GetAbilityName(target.m_nAbility)}'"
                                     + $" with {uses} use(s).");
            }
            catch (Exception e) { SlimeFixPlugin.Warn("mimic: copying the target's ability failed: " + e.Message); }

            // Level from the copy, so HP, attack and defence follow the game's own level tables.
            body.nLevel = level;
            body.nMAXHP = UnitTypeTable.GetMaxHP(copiedType, level);
            body.nHP = body.nMAXHP;
            body.nAttackPow = UnitTypeTable.GetAttackPower(copiedType, level);
            body.nDefencePow = UnitTypeTable.GetDefencePower(copiedType, level);

            // True forces the plain sheet. A possessed body normally draws TEX_NAME + "_g", and the
            // copied type may not have one - the game throws on the missing name instead of falling back.
            // The divided child is created by this same call and animates, so nothing here re-drives the
            // sheet or the animation: only the two things BattleFlow.DivideSlime does to its child. The
            // forced plain sheet stays on the occupied path, where the possessed sheet name may not exist.
            if (!UnitTypeTable.IsSlimeUnit(copiedType)) body.SetUnitQuadTexture(true);

            body.SetUnitAction(UNIT_ACTION.wait_time);
            if (battle.m_bUserTurn) body.SetActionEnd(true);



            Mimicked.Add(created);
            Paint(body);

            // The slime's own body is removed and the flow is moved onto the shape. The unit box
            // already points at the shape because SetUserUnitParam ran above, which is what the
            // earlier attempts were missing when this ended in an unusable unit.
            if (replaceCaster)
            {
                SlimeFixPlugin.Verbose($"mimic: moving the turn onto slot {created} and removing slot {casterOffs}.");
                battle.nCurrentUnitOffs = created;
                caster.DestroyObject();
            }

            SlimeFixPlugin.Instance?.Log.LogInfo(
                $"mimic: {Name((UNIT_TYPE)SlimeType)} took the shape of {Name(copiedType)} Lv.{target.nLevel}"
              + $" at {LevelPercent}% -> Lv.{level} (HP {body.nHP}, AT {body.nAttackPow}, DF {body.nDefencePow},"
              + $" cell {spawnX},{spawnZ})"
              + (replaceCaster ? "; the zako's own body is gone."
                               : "; the zako itself stays on the field, and nothing is written to its unit box."));
            return true;
        }

        /// <summary>Draws the mimic tint, and re-states the level-derived stats.</summary>
        internal static void Paint(UnitData unit)
        {
            if (!Active || unit == null) return;
            if (!Mimicked.Contains(unit.myOffs)) return;

            try
            {
                if (unit.materialUnitQuad != null) unit.materialUnitQuad.SetColor("_Color", Tint);

                if (unit.unitQuad != null)
                {
                    var renderer = unit.unitQuad.GetComponent<Renderer>();
                    if (renderer != null && renderer.material != null)
                        renderer.material.SetColor("_Color", Tint);
                }
            }
            catch (Exception e) { SlimeFixPlugin.Warn("mimic: tinting failed: " + e.Message); }
        }

        /// <summary>Re-states the tint on the turns after the copy.</summary>
        internal static void RefreshTint(UnitData unit)
        {
            if (unit == null) return;
            if (!Mimicked.Contains(unit.myOffs)) return;

            Paint(unit);
        }

        private static string Name(UNIT_TYPE type)
        {
            try { return Strs.GetUnitTypeName(type); }
            catch (Exception) { return "?"; }
        }
    }

    /// <summary>Gives the mimic slime the borrowed shell ability.</summary>
    [HarmonyPatch(typeof(UnitTypeTable), nameof(UnitTypeTable.GetAbilityType))]
    internal static class Patch_UnitTypeTable_GetAbilityType_Mimic
    {
        private static bool announced;

        private static void Postfix(UNIT_TYPE type, ref ABILITY_TYPE __result)
        {
            if (!Mimic.Active) return;

            // The unit tables do not exist while the plugin loads, so the shell is resolved and
            // reported here instead: this runs when the game first asks about a unit type.
            if (!announced)
            {
                announced = true;
                try
                {
                    var shell = Mimic.ShellAbility;
                    SlimeFixPlugin.Instance?.Log.LogInfo(
                        $"Mimic ready: type {Mimic.SlimeType} mimics with the shell ability"
                      + $" '{Strs.GetAbilityName(shell)}' ({(int)shell}) borrowed from type {Mimic.ShellType},"
                      + $" result level {Mimic.LevelPercent}% of the target, shield break"
                      + $" {Mimic.ShieldBreakPercent}%.");
                }
                catch (Exception e) { SlimeFixPlugin.Warn("mimic: resolving the shell ability failed: " + e.Message); }
            }

            if ((int)type != Mimic.SlimeType) return;

            var ability = Mimic.ShellAbility;
            if (ability != ABILITY_TYPE.none) __result = ability;
        }
    }

    /// <summary>Gives it one use.</summary>
    [HarmonyPatch(typeof(UnitTypeTable), nameof(UnitTypeTable.GetAbilityNum))]
    internal static class Patch_UnitTypeTable_GetAbilityNum_Mimic
    {
        private static void Postfix(UNIT_TYPE type, ref int __result)
        {
            if (!Mimic.Active) return;
            if ((int)type != Mimic.SlimeType) return;

            __result = 1;
        }
    }

    /// <summary>
    /// Widens the mimic's reach to the whole field and to every unit: the shell's own data points at
    /// one target kind within one tile, which is not what a mimic needs.
    /// </summary>
    [HarmonyPatch(typeof(UnitData), nameof(UnitData.SetAbilityParam))]
    internal static class Patch_UnitData_SetAbilityParam_Mimic
    {
        private static void Postfix(UnitData __instance)
        {
            if (!Mimic.Active || __instance == null) return;
            if ((int)__instance.nType != Mimic.SlimeType) return;

            try
            {
                __instance.m_nAbilityTargetType = UNIT_TARGET.all;
                __instance.m_nAbilityRange = 99;
                __instance.m_nAbilityLen = 99;
            }
            catch (Exception e) { SlimeFixPlugin.Warn("mimic: widening the reach failed: " + e.Message); }
        }
    }

    /// <summary>Accepts every unit as a target, slime or human, ours or theirs.</summary>
    [HarmonyPatch(typeof(UnitData), nameof(UnitData.IsAbilityEffective))]
    internal static class Patch_UnitData_IsAbilityEffective_Mimic
    {
        private static void Postfix(UnitData __instance, int __0, ref bool __result)
        {
            if (!Mimic.Active) return;

            try
            {
                // A forbidden target must not be selectable at all. Refusing it after the ability was
                // spent only made the ability look broken.
                if (Mimic.IsCaster(__instance))
                {
                    var battle = Mimic.LastBattle;
                    var units = battle == null ? null : battle.UnitDataArray;
                    if (units != null && __0 >= 0 && __0 < units.Length)
                    {
                        var target = units[__0];
                        if (target != null && Mimic.IsForbidden(target.nType)) { __result = false; return; }
                    }

                    __result = true;
                    return;
                }

                if (__result) return;
            }
            catch (Exception e) { SlimeFixPlugin.Warn("mimic: widening the target check failed: " + e.Message); }
        }
    }

    /// <summary>
    /// Copies the target after the ability has run.
    ///
    /// BattleFlow.StartUseAbilityProcess calls UseAbility and then continues with the ability's own
    /// animation and state sequence, so skipping the call leaves that sequence waiting forever and
    /// the battle stops responding. The game's effect therefore runs as usual and the copy happens
    /// afterwards.
    ///
    /// The borrowed shell normally removes the target's protection. That is what the shield break
    /// chance is for here, so the protection is captured before the call and put back when the roll
    /// does not pass.
    /// </summary>
    [HarmonyPatch(typeof(BattleFlow), nameof(BattleFlow.UseAbility))]
    internal static class Patch_BattleFlow_UseAbility_Mimic
    {
        private static readonly System.Random Dice = new System.Random();

        private static void Prefix(BattleFlow __instance, int __0, out bool __state)
        {
            __state = false;
            if (!Mimic.Active) return;

            try
            {
                var units = __instance == null ? null : __instance.UnitDataArray;
                if (units == null || __0 < 0 || __0 >= units.Length) return;

                var target = units[__0];
                if (target != null) __state = target.m_bProtected;
            }
            catch (Exception e) { SlimeFixPlugin.Warn("mimic: reading the target's protection failed: " + e.Message); }
        }

        private static void Postfix(BattleFlow __instance, int __0, bool __result, bool __state)
        {
            if (!Mimic.Active) return;
            if (!__result) return;

            try
            {
                var units = __instance == null ? null : __instance.UnitDataArray;
                if (units == null) return;
                if (__0 < 0 || __0 >= units.Length) return;

                Mimic.LastBattle = __instance;

                int casterOffs = __instance.nCurrentUnitOffs;
                if (casterOffs < 0 || casterOffs >= units.Length) return;
                if (!Mimic.IsCaster(units[casterOffs])) return;

                var target = units[__0];
                bool broke = target != null && Mimic.ShieldBreakPercent > 0
                          && Dice.Next(100) < Mimic.ShieldBreakPercent;

                if (!broke && __state && target != null)
                {
                    try { target.SetProtected(); }
                    catch (Exception e) { SlimeFixPlugin.Warn("mimic: restoring protection failed: " + e.Message); }
                }

                Mimic.Request(casterOffs, __0);
                Mimic.ApplyFor(__instance, casterOffs);
            }
            catch (Exception e) { SlimeFixPlugin.Warn("mimic failed: " + e.Message); }
        }
    }

    /// <summary>
    /// Accepts every unit on the target block table as well. IsAbilityEffective alone was not enough:
    /// reachable cells showed up while no unit on them could be confirmed.
    /// </summary>
    [HarmonyPatch(typeof(UnitData), nameof(UnitData.CheckUnitTargetType))]
    internal static class Patch_UnitData_CheckUnitTargetType_Mimic
    {
        private static void Postfix(UnitData __instance, ref bool __result)
        {
            if (__result) return;
            if (!Mimic.Active) return;

            try
            {
                if (Mimic.IsCaster(__instance)) __result = true;
            }
            catch (Exception e) { SlimeFixPlugin.Warn("mimic: widening the target type check failed: " + e.Message); }
        }
    }

    /// <summary>
    /// Keeps a mimic body out of the unit box. The box records what a slime is carrying, so writing the
    /// copied shape there made the zako come back as that shape in the next battle instead of as itself.
    /// </summary>
    [HarmonyPatch(typeof(UserUnitData), nameof(UserUnitData.SetBattleResult_occupying))]
    internal static class Patch_UserUnitData_SetBattleResult_occupying_Mimic
    {
        private static bool Prefix(UserUnitData __instance)
        {
            if (!Mimic.Active) return true;

            try
            {
                return !Mimic.IsMimicBody(__instance);
            }
            catch (Exception e)
            {
                SlimeFixPlugin.Warn("mimic: keeping the shape out of the unit box failed: " + e.Message);
                return true;
            }
        }
    }

    /// <summary>Keeps the tint on the turns after the copy.</summary>
    [HarmonyPatch(typeof(UnitData), nameof(UnitData.TurnStartCheck))]
    internal static class Patch_UnitData_TurnStartCheck_Mimic
    {
        private static void Postfix(UnitData __instance)
        {
            if (!Mimic.Active) return;

            try { Mimic.RefreshTint(__instance); }
            catch (Exception e) { SlimeFixPlugin.Warn("mimic: refreshing the tint failed: " + e.Message); }
        }
    }

    /// <summary>
    /// Re-states the tint every frame. The occupation display plugin repaints these units white, and a
    /// priority of Last puts this after it, so the colour survives instead of being wiped immediately.
    /// </summary>
    [HarmonyPatch(typeof(UnitData), nameof(UnitData.UpdateUnit))]
    [HarmonyPriority(Priority.Last)]
    internal static class Patch_UnitData_UpdateUnit_Mimic
    {
        private static void Postfix(UnitData __instance)
        {
            if (!Mimic.Active) return;

            try { Mimic.RefreshTint(__instance); }
            catch (Exception) { /* per-frame path: a failed repaint is not worth a log line each frame */ }
        }
    }

    /// <summary>
    /// Names the borrowed shell on the mimic's own ability entry. The shell is the anti-magic slime's
    /// shield breaker, so the name is only replaced while the battle's current unit is a mimic that has
    /// not copied anything yet; every other unit keeps the game's own name for it.
    /// </summary>
    [HarmonyPatch(typeof(Strs), nameof(Strs.GetAbilityName))]
    internal static class Patch_Strs_GetAbilityName_Mimic
    {
        private static void Postfix(ABILITY_TYPE type, ref string __result)
        {
            if (!Mimic.Active) return;
            if (Mimic.ShellAbility == ABILITY_TYPE.none) return;
            if (type != Mimic.ShellAbility) return;

            try
            {
                var battle = Mimic.LastBattle;
                var units = battle == null ? null : battle.UnitDataArray;
                if (units == null) return;

                int offs = battle.nCurrentUnitOffs;
                if (offs < 0 || offs >= units.Length) return;
                if (!Mimic.IsCaster(units[offs])) return;

                __result = "拟态";
            }
            catch (Exception e) { SlimeFixPlugin.Warn("mimic: renaming the ability failed: " + e.Message); }
        }
    }

    /// <summary>
    /// Also keeps the shape out of the unit box on the plain result path. The occupation path was
    /// already guarded; this covers the record a mimic body would otherwise be written as.
    /// </summary>
    [HarmonyPatch(typeof(UserUnitData), nameof(UserUnitData.SetBattleResult))]
    internal static class Patch_UserUnitData_SetBattleResult_Mimic
    {
        private static bool Prefix(UserUnitData __instance)
        {
            if (!Mimic.Active) return true;

            try
            {
                if (!Mimic.IsMimicBody(__instance)) return true;

                SlimeFixPlugin.Instance?.Log.LogInfo(
                    "mimic: keeping the copied shape out of the unit box record, so the slime returns as"
                  + " itself next battle.");
                return false;
            }
            catch (Exception e)
            {
                SlimeFixPlugin.Warn("mimic: guarding the unit box record failed: " + e.Message);
                return true;
            }
        }
    }

    /// <summary>
    /// Keeps the copied shape out of the unit box on the battle-result path as well. The box is written
    /// straight from UnitData.SetBattleResult, which the record-level guards cannot see.
    /// </summary>
    [HarmonyPatch(typeof(UnitData), nameof(UnitData.SetBattleResult))]
    internal static class Patch_UnitData_SetBattleResult_Mimic
    {
        private static void Postfix(UnitData __instance)
        {
            if (!Mimic.Active) return;
            if (!Mimic.IsMimicUnit(__instance)) return;

            try { Mimic.RestoreBox(__instance); }
            catch (Exception e) { SlimeFixPlugin.Warn("mimic: restoring the unit box failed: " + e.Message); }
        }
    }

    /// <summary>
    /// Cleans the slime's unit box when the save is loaded.
    ///
    /// Guarding every write path did not stop a copied shape from surviving in the unit box, so the
    /// record is corrected at the other end instead: a zako slime cannot legitimately carry anything
    /// (the carrying passive belongs to another type), so any occupying data on its record is left
    /// over from a copy and is cleared here, before the battle deploys its units.
    /// </summary>
    [HarmonyPatch(typeof(UserUnitData), nameof(UserUnitData.Load))]
    internal static class Patch_UserUnitData_Load_Mimic
    {
        private static void Postfix(UserUnitData __instance, int __0)
        {
            if (!Mimic.Active) return;
            if (__instance == null) return;

            // Temporary census: every unit-box record as it is loaded, so a leaked shape can be seen
            // where it lives instead of guessed at.
            try
            {
                SlimeFixPlugin.Verbose($"mimic: box {__0} loaded type={(int)__instance.nType} '"
                                     + $"{Strs.GetUnitTypeName(__instance.nType)}' Lv.{__instance.nLevel}"
                                     + $" occupyTurn={__instance.nOccupyingTurn} occupyType={(int)__instance.nOccupyUnitType}"
                                     + $" occupyLv={__instance.nOccupyUnitLevel} unitDataOffs={__instance.nUnitDataOffs}");
            }
            catch (Exception e) { SlimeFixPlugin.Warn("mimic: logging a unit box failed: " + e.Message); }

            if ((int)__instance.nType != Mimic.SlimeType) return;

            try
            {
                bool dirty = __instance.nOccupyingTurn != 0
                          || (int)__instance.nOccupyUnitType != 0
                          || (int)__instance.nOccupyUnitOriginalType != 0;
                if (!dirty) return;

                __instance.nOccupyingTurn = 0;
                __instance.nOccupyUnitType = 0;
                __instance.nOccupyUnitLevel = 0;
                __instance.nOccupyUnitAttr = 0;
                __instance.nOccupyUnitOriginalType = 0;

                string typeName;
                try { typeName = Strs.GetUnitTypeName((UNIT_TYPE)Mimic.SlimeType); }
                catch (Exception) { typeName = "?"; }

                SlimeFixPlugin.Instance?.Log.LogInfo(
                    $"mimic: cleared a leftover copied shape from the unit box of {typeName}"
                  + " Lv." + __instance.nLevel + " while loading the save.");
            }
            catch (Exception e) { SlimeFixPlugin.Warn("mimic: cleaning the unit box on load failed: " + e.Message); }
        }
    }

    /// <summary>
    /// Censors and repairs battle units as they are restored from a battle save.
    ///
    /// A copied shape is a battle unit, so a mid-battle save keeps it: resuming that battle brought the
    /// shape back, which is what looked like the next deployment turning a slime into its mimic. The
    /// unit box is already clean by then, so the repair happens on the battle unit itself - a unit whose
    /// box is a zako's but whose type is not a zako can only be one of these copies.
    /// </summary>
    [HarmonyPatch(typeof(UnitData), nameof(UnitData.Load))]
    internal static class Patch_UnitData_Load_Mimic
    {
        private static void Postfix(UnitData __instance, int __0)
        {
            if (!Mimic.Active) return;
            if (__instance == null) return;
            if (!__instance.IsValidUnit()) return;

            try
            {
                string loadedName;
                try { loadedName = Strs.GetUnitTypeName(__instance.nType); }
                catch (Exception) { loadedName = "?"; }

                SlimeFixPlugin.Verbose($"mimic: battle unit {__0} loaded type={(int)__instance.nType}"
                                     + $" '{loadedName}' side={(int)__instance.m_nSideType}"
                                     + $" box={__instance.m_nUserUnitOffs}"
                                     + $" state={(int)__instance.m_nOccupyState} Lv.{__instance.nLevel}");

                int boxOffs = __instance.m_nUserUnitOffs;
                if (boxOffs < 0) return;

                var records = UserUnitTable.DataArray;
                if (records == null || boxOffs >= records.Length) return;

                var record = records[boxOffs];
                if (record == null) return;
                if ((int)record.nType != Mimic.SlimeType) return;
                if ((int)__instance.nType == Mimic.SlimeType) return;

                // This unit claims a zako's unit box but is not a zako: it is a copy that outlived the
                // battle it was made in. Put the slime back the way the box describes it.
                var type = (UNIT_TYPE)record.nType;
                __instance.ChangeUnitType(type, 0);
                __instance.m_nSideType = SIDE_TYPE.slime;
                __instance.m_nOccupyState = OCCUPY_STATE.normal;
                __instance.m_nSuckUnitOffs = -1;
                __instance.nLevel = record.nLevel;
                __instance.nAttr = record.nAttr;
                __instance.nMAXHP = UnitTypeTable.GetMaxHP(type, record.nLevel);
                __instance.nHP = __instance.nMAXHP;
                __instance.bSlime = UnitTypeTable.IsSlimeUnit(type);
                __instance.SetUnitQuadTexture(true);
                __instance.SetUnitTexAnimType();
                __instance.PlayWaitAnime(false);

                string restoredName;
                try { restoredName = Strs.GetUnitTypeName(type); }
                catch (Exception) { restoredName = "?"; }

                SlimeFixPlugin.Instance?.Log.LogInfo(
                    $"mimic: restored battle unit {__0} to {restoredName} Lv.{record.nLevel}; it was a"
                  + " copied shape left in the battle save.");
            }
            catch (Exception e) { SlimeFixPlugin.Warn("mimic: repairing a loaded battle unit failed: " + e.Message); }
        }
    }

    /// <summary>Forgets the battle's mimics, next to the other per-battle bookkeeping.</summary>
    internal static class MimicBattleReset
    {
        internal static void Reset()
        {
            Mimic.ResetBattle();
        }
    }
}
