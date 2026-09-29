using System;
using HarmonyLib;
using UnityEngine;

namespace SlimesSlimeFix
{
    /// <summary>
    /// Lets the melty slime digest the unit in its stomach, from the same little window that offers
    /// "possess" and "keep as is".
    ///
    /// ActionButtonHandler.SetButtons builds that window by activating two slots in a row
    /// (ACTION_BUTTON_TYPE.occupy then not_occupy) and putting the cursor on slot 1, and the OK
    /// press is hardcoded as "cursor 0 = possess, anything else = keep as is". This patch adds a
    /// third slot next to them when the current slime can digest, and takes over the keep-as-is
    /// handler for cursor slot 2, so the ability button stays exactly what it always was.
    ///
    /// Digesting spends one ability use, destroys the swallowed unit, and puts a skeleton of that
    /// unit's level on the slime's own cell on the player's side. The skeleton is created the way
    /// BattleFlow.DivideSlime creates a divided slime, except that it does NOT copy the slime's
    /// unit-box offset: a unit with a box offset gets its level overwritten from that box record by
    /// UnitData.SetUserUnitParam, which is what made the skeleton show the slime's level instead of
    /// the swallowed unit's. Units without a box offset are skipped by the battle-end result pass
    /// (see Patch_UnitData_SetBattleResult_Reset), so nothing indexes an out-of-range record.
    /// </summary>
    internal static class Digest
    {
        /// <summary>Label put on the added window slot.</summary>
        private const string DigestLabel = "消化";

        /// <summary>Whether the digest patch may change anything at all.</summary>
        internal static bool Active =>
            SlimeFixPlugin.CfgEnabled != null && SlimeFixPlugin.CfgEnabled.Value
            && SlimeFixPlugin.CfgDigestEnabled != null && SlimeFixPlugin.CfgDigestEnabled.Value;

        /// <summary>UNIT_TYPE whose ability button digests its swallowed unit.</summary>
        internal static int SlimeType => SlimeFixPlugin.CfgDigestSlimeType?.Value ?? (int)UNIT_TYPE.s_melty;

        /// <summary>UNIT_TYPE created by the digest.</summary>
        internal static int SkeletonType => SlimeFixPlugin.CfgDigestSkeletonType?.Value ?? (int)UNIT_TYPE.skelton;

        /// <summary>Label the borrowed button object carries when the digest slot is not shown.</summary>
        private static string borrowedLabel;

        /// <summary>
        /// Set when a digest just ran, so the "keep as is" behaviour it replaced does not also run.
        /// </summary>
        private static bool skipMoveStep;

        /// <summary>Reads and clears <see cref="skipMoveStep"/>.</summary>
        internal static bool ConsumeSkipMoveStep()
        {
            if (!skipMoveStep) return false;
            skipMoveStep = false;
            return true;
        }

        /// <summary>
        /// Puts the borrowed button's own label back before the game lays a window out, so the
        /// ability window shows its usual text again.
        /// </summary>
        internal static void RestoreBorrowedLabel(ActionButtonHandler handler)
        {
            if (handler == null || borrowedLabel == null) return;

            var button = ButtonOf(handler, ActionButtonHandler.ACTION_BUTTON_TYPE.suck);
            if (button != null) button.SetLabel(borrowedLabel);
        }

        private static ActionButton ButtonOf(ActionButtonHandler handler, ActionButtonHandler.ACTION_BUTTON_TYPE type)
        {
            var objects = handler.ActionButtonObj;
            if (objects == null || (int)type >= objects.Length) return null;

            var obj = objects[(int)type];
            if (obj == null) return null;

            return obj.GetComponent<ActionButton>();
        }

        /// <summary>
        /// Adds the digest slot to the possess/keep-as-is window when the current slime can use it.
        /// Called after the game has filled the window in, so the new slot lands after both of its
        /// own entries.
        /// </summary>
        internal static void AddSlotIfAvailable(ActionButtonHandler handler)
        {
            if (handler == null) return;

            var types = handler.m_nActionButtonType;
            int used = handler.m_nActiveButtonNum;
            if (types == null || used < 2) return;
            if ((int)types[0] != (int)ActionButtonHandler.ACTION_BUTTON_TYPE.occupy) return;
            if ((int)types[1] != (int)ActionButtonHandler.ACTION_BUTTON_TYPE.not_occupy) return;
            if (used >= types.Length) return;
            if (FindMeal(handler) == null) return;

            // ACTION_BUTTON_TYPE is used as an index into ActionButtonObj, so the added slot must
            // name a type whose button object exists and that this window does not already show.
            var type = ActionButtonHandler.ACTION_BUTTON_TYPE.suck;
            handler.SetActionButtonActive(used, type);
            handler.m_nActiveButtonNum = used + 1;
            handler.SetActionButtonPos();      // SetButtons laid the buttons out before this slot existed

            var button = ButtonOf(handler, type);
            if (button != null)
            {
                if (borrowedLabel == null) borrowedLabel = button.LabelAction == null ? string.Empty : button.LabelAction.text;
                button.SetLabel(DigestLabel);
            }

            SlimeFixPlugin.Verbose($"digest: window slot {used} added as '{DigestLabel}'.");
        }

        /// <summary>
        /// Digests when the player confirmed the added slot. Returns true when the press was used,
        /// so the caller leaves the keep-as-is behaviour alone.
        /// </summary>
        internal static bool TryDigestFromWindow(ActionButtonHandler handler)
        {
            if (handler == null) return false;
            SlimeFixPlugin.Verbose($"digest: press on cursor {handler.m_nCursorPos},"
                                 + $" rows {handler.m_nActiveButtonNum}.");
            if (handler.m_nCursorPos <= 1) return false;   // slots 0 and 1 keep their own meaning

            var meal = FindMeal(handler);
            if (meal == null) return false;

            var battle = handler.BattleFlowComponent;
            var units = battle == null ? null : battle.UnitDataArray;
            if (units == null) return false;

            int slimeOffs = battle.nCurrentUnitOffs;
            if (slimeOffs < 0 || slimeOffs >= units.Length) return false;

            var slime = units[slimeOffs];
            if (slime == null) return false;

            // On the slime's own cell. Stacking two units there is fine: the earlier report of not
            // being able to select the slime turned out to be a side mismatch, not the stacking.
            int spawnX = slime.nPosX, spawnZ = slime.nPosZ;

            // One ability use is the whole price: the slime keeps its body and goes back to its
            // normal state afterwards.
            slime.m_nAbilityCount = slime.m_nAbilityCount - 1;

            var skeletonType = (UNIT_TYPE)SkeletonType;
            int level = meal.nLevel;
            int attribute = SlimeFixPlugin.CfgDigestCopyAttribute.Value ? (int)meal.nAttr : (int)slime.nAttr;

            int created = battle.CreateNewUnit(skeletonType, (ATTR_TYPE)attribute, level,
                                               spawnX, spawnZ, slime.nLookDir, -1, -1, skeletonType);
            if (created < 0)
            {
                slime.m_nAbilityCount = slime.m_nAbilityCount + 1;
                SlimeFixPlugin.Warn("digest: no vacant unit slot, nothing was digested.");
                return false;
            }

            // A unit whose species is not a slime is on the other side no matter what m_nSideType
            // says - the side the game acts on comes from the species tag, and the only way a
            // non-slime fights for the player is as a possessed body. So the skeleton is put into
            // exactly that state: the game's own possession transition.
            var skeleton = battle.UnitDataArray[created];
            skeleton.SetOccupied(slimeOffs);

            // SetOccupied places the host on the slime's cell; move it onto the blank cell the
            // search picked so the two never share a cell.
            skeleton.SetUnitPos(spawnX, spawnZ);

            // The slime keeps its own body - only the ability use was spent - so instead of the
            // DestroyObject() the game runs after a normal possession, it just goes back to the
            // state it was in before it ate. The two share the slime's unit-box record, which is
            // the arrangement BattleFlow.DivideSlime already creates.
            meal.DestroyObject();
            slime.m_nSuckUnitOffs = -1;
            slime.m_nOccupyState = OCCUPY_STATE.normal;
            slime.SetUnitTexAnimType();
            slime.PlayWaitAnime(false);

            // The skeleton appears in the middle of the player's turn. Marking its action as ended
            // is what the game does for a unit created mid-turn and keeps the balance honest: it
            // takes part from the next turn on, and UnitData.ResetActionEnd clears the flag then.
            skeleton.SetActionEnd(true);

            battle.EndCurrentUnitAction(false);
            skipMoveStep = true;

            SlimeFixPlugin.Instance?.Log.LogInfo(
                $"digest: {Name((UNIT_TYPE)SlimeType)} digested {Name((UNIT_TYPE)meal.nType)} Lv.{level}"
              + $" for one ability use - {Name(skeletonType)} Lv.{level} is now possessed by it at cell"
              + $" {spawnX},{spawnZ}"
              + $"; {slime.m_nAbilityCount} ability use(s) left.");
            return true;
        }

        /// <summary>
        /// The unit inside the current slime, or null when there is nothing to digest. Also the
        /// condition that puts the extra window slot on screen.
        /// </summary>
        private static UnitData FindMeal(ActionButtonHandler handler)
        {
            var battle = handler.BattleFlowComponent;
            if (battle == null) return null;

            var units = battle.UnitDataArray;
            if (units == null) return null;

            int offs = battle.nCurrentUnitOffs;
            if (offs < 0 || offs >= units.Length) return null;

            var slime = units[offs];
            if (slime == null) return null;
            if ((int)slime.nType != SlimeType) return null;
            if ((int)slime.m_nOccupyState != (int)OCCUPY_STATE.sucking) return null;
            if (slime.m_nUserUnitOffs < 0) return null;      // the enemy side keeps the plain window
            if (slime.m_nAbilityCount <= 0) return null;

            int mealOffs = slime.m_nSuckUnitOffs;
            if (mealOffs < 0 || mealOffs >= units.Length) return null;

            var meal = units[mealOffs];
            if (meal == null || !meal.IsSucked()) return null;
            if (meal.m_nUserUnitOffs >= 0) return null;      // never digest one of our own units

            return meal;
        }

        private static string Name(UNIT_TYPE type)
        {
            try { return Strs.GetUnitTypeName(type); }
            catch (Exception) { return "?"; }
        }
    }

    /// <summary>Adds the digest slot to the possess / keep-as-is window while the slime can digest.</summary>
    [HarmonyPatch(typeof(ActionButtonHandler), nameof(ActionButtonHandler.SetButtons))]
    internal static class Patch_ActionButtonHandler_SetButtons
    {
        private static void Prefix(ActionButtonHandler __instance)
        {
            if (!Digest.Active) return;
            Digest.RestoreBorrowedLabel(__instance);
        }

        private static void Postfix(ActionButtonHandler __instance)
        {
            if (!Digest.Active) return;

            try { Digest.AddSlotIfAvailable(__instance); }
            catch (Exception e) { SlimeFixPlugin.Warn("digest: adding the window slot failed: " + e.Message); }
        }
    }

    /// <summary>
    /// Digests on the keep-as-is step. ActionButtonHandler.PushNotOccupyButton only calls
    /// BattleFlow.SetActionStep(ACTION_STEP.move), so the C++ compiler very likely inlines it into
    /// the key dispatcher and the patch on it never runs - which is exactly what happened. This
    /// hook sits on the callee instead, which is far too large to be inlined.
    /// </summary>
    [HarmonyPatch(typeof(BattleFlow), nameof(BattleFlow.SetActionStep))]
    internal static class Patch_BattleFlow_SetActionStep
    {
        private static bool Prefix(BattleFlow __instance, ACTION_STEP step)
        {
            if (!Digest.Active) return true;
            if ((int)step != (int)ACTION_STEP.move) return true;

            if (Digest.ConsumeSkipMoveStep()) return false;

            try { return !Digest.TryDigestFromWindow(__instance.ActionButtonComponent); }
            catch (Exception e)
            {
                SlimeFixPlugin.Warn("digest failed: " + e.Message);
                return true;
            }
        }
    }

    /// <summary>
    /// Redundant second hook on the small handler, in case a future build stops inlining it. It
    /// only performs the digest and lets the call through; the step hook above then skips the move
    /// step that the digest replaced.
    /// </summary>
    [HarmonyPatch(typeof(ActionButtonHandler), nameof(ActionButtonHandler.PushNotOccupyButton))]
    internal static class Patch_ActionButtonHandler_PushNotOccupyButton
    {
        private static bool Prefix(ActionButtonHandler __instance)
        {
            if (!Digest.Active) return true;

            try { Digest.TryDigestFromWindow(__instance); return true; }
            catch (Exception e)
            {
                SlimeFixPlugin.Warn("digest failed: " + e.Message);
                return true;
            }
        }
    }
}
