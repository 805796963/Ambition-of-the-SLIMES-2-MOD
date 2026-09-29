using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SlimesItems
{
    // -------------------------------------------------------------------- unit box capacity

    /// <summary>
    /// Raises how many slimes the unit box holds, when the configuration asks for more than the game's 32.
    ///
    /// The whole limit hangs off one field. <c>UserUnitTable.Prepare</c> sizes <c>DataArray</c> from
    /// <c>Defs.USER_UNIT_MAX</c> and allocates one <c>UserUnitData</c> per slot, <c>SetUserUnitNum</c>, <c>Save</c>
    /// and <c>Load</c> loop to the same field, <c>SaveData/SDS_UserUnitTable</c> sizes its own record array from it,
    /// and the 目前单位�?/ 最大单位数 panel prints it, so writing it once moves storage, the counter and every
    /// check together. The array has to be built at the new size, which is why this runs as a prefix on Prepare
    /// rather than at plugin load: Prepare both allocates the array and resets the slots, so it must not have run
    /// yet. Writing through the field also forces <c>Defs</c>' static constructor, which is what would otherwise
    /// overwrite a write made any earlier.
    ///
    /// This is a save-format change, which is why the configuration defaults to the game's own value and why the
    /// comment above the setting carries the warning: the record count is implied by this field at read time, not
    /// stored in the file, so a save written with one value cannot be read with another.
    /// </summary>
    [HarmonyPatch(typeof(UserUnitTable), "Prepare")]
    internal static class UnitBoxCapacity
    {
        private const int VanillaUnitMax = 32;

        private static void Prefix()
        {
            var configured = ItemsPlugin.CfgUnitBoxMax;
            if (configured == null) return;

            int wanted = configured.Value;

            try
            {
                if (Defs.USER_UNIT_MAX == wanted) return;

                Defs.USER_UNIT_MAX = wanted;
                ItemsPlugin.Instance.Log.LogWarning(
                    $"unit box capacity is {wanted} instead of {VanillaUnitMax}. The save format stores as many unit "
                  + "records as this value says, so only saves written with the same value load correctly: an older "
                  + "save will desynchronise, and a save written now cannot be loaded by the unmodded game. Keep this "
                  + "value fixed for the whole save.");
            }
            catch (Exception e)
            {
                ItemsPlugin.Warn($"raise the unit box capacity failed: {e.Message}");
            }
        }
    }

    // -------------------------------------------------------------------- items

    /// <summary>
    /// Stage-clear item rewards. ResultHandler.SetResultData asks BattleInfoTable.GetPrize for the prize of this
    /// clear (nMapClearCount is 0 on the first clear, 1 or 2 on repeats, which is why the item differs between
    /// runs). UpdatePrizeStart then dispatches to DispSlimePrize for a unit prize or DispItemPrize otherwise, and
    /// DispItemPrize is the only place the vanilla code calls GameData.AddItemNum. Hooking those two display
    /// methods grants one of every ITEM_TYPE on every clear, independent of first-clear versus repeat.
    /// </summary>
    internal static class ItemRewards
    {
        private const int PrizeTypeItem = 2;   // PRIZE_TYPE: none=0, unit=1, item=2

        internal static int MaxItemType => (int)ITEM_TYPE.max;

        internal static string DescribeCounts()
        {
            var parts = new List<string>();
            for (int i = 0; i < MaxItemType; i++)
                parts.Add($"{(ITEM_TYPE)i}={GameData.GetItemNum((ITEM_TYPE)i)}");
            return string.Join(" ", parts);
        }

        internal static void GrantAll(ResultHandler handler, bool skipPrizeAlreadyGranted)
        {
            int prize = NativeMember.GetInt(handler, "m_nPrize", -1);
            int prizeType = NativeMember.GetInt(handler, "m_nPrizeType", -1);
            bool skipPrize = skipPrizeAlreadyGranted && prizeType == PrizeTypeItem;

            // Logged at info level: one line per stage clear is worth having in the log by default.
            string before = DescribeCounts();

            int granted = 0;
            for (int i = 0; i < MaxItemType; i++)
            {
                if (skipPrize && i == prize) continue;   // vanilla already granted this one
                GameData.AddItemNum((ITEM_TYPE)i);
                granted++;
            }

            ItemsPlugin.Instance.Log.LogInfo(
                $"item reward: granted {granted} type(s), prizeType={prizeType} prize={prize} | {before} -> {DescribeCounts()}");
        }

        private static bool _selfTestDone;

        /// <summary>
        /// Grants one of every item type, logs the counts, then writes the original counts back. Runs once from a
        /// hook that only fires after the save has been loaded, because GameData.m_nItemNum does not exist before
        /// then; failures retry on the next call.
        /// </summary>
        internal static void SelfTestIfPending()
        {
            if (_selfTestDone || ItemsPlugin.CfgSelfTest == null || !ItemsPlugin.CfgSelfTest.Value) return;

            try
            {
                int max = MaxItemType;
                if (max <= 0) return;

                var before = new int[max];
                for (int i = 0; i < max; i++) before[i] = GameData.GetItemNum((ITEM_TYPE)i);

                for (int i = 0; i < max; i++) GameData.AddItemNum((ITEM_TYPE)i);

                var after = new int[max];
                for (int i = 0; i < max; i++) after[i] = GameData.GetItemNum((ITEM_TYPE)i);

                int written = 0;
                if (GameData.m_nItemNum is IList<int> slots)
                {
                    int n = Math.Min(max, slots.Count);
                    for (int i = 0; i < n; i++) { slots[i] = before[i]; written++; }
                }

                var restored = new int[max];
                for (int i = 0; i < max; i++) restored[i] = GameData.GetItemNum((ITEM_TYPE)i);

                ItemsPlugin.Instance.Log.LogInfo($"selftest before   : {Join(before)}");
                ItemsPlugin.Instance.Log.LogInfo($"selftest after +1 : {Join(after)}");
                ItemsPlugin.Instance.Log.LogInfo($"selftest restored : {Join(restored)}  ({written} slot(s) written back)");
                _selfTestDone = true;
            }
            catch (Exception e)
            {
                ItemsPlugin.Warn($"selftest deferred (GameData not ready?): {e.Message}");
            }
        }

        private static string Join(int[] values)
        {
            var parts = new List<string>();
            for (int i = 0; i < values.Length; i++) parts.Add($"{(ITEM_TYPE)i}={values[i]}");
            return string.Join(" ", parts);
        }
    }

    [HarmonyPatch(typeof(ResultHandler), "DispItemPrize")]
    internal static class Patch_DispItemPrize
    {
        [HarmonyPostfix]
        private static void Postfix(ResultHandler __instance)
        {
            if (!ItemsPlugin.CfgEnabled.Value || !ItemsPlugin.CfgGrantAllItemsOnItemPrize.Value) return;
            ItemRewards.GrantAll(__instance, skipPrizeAlreadyGranted: true);
        }
    }

    [HarmonyPatch(typeof(ResultHandler), "DispSlimePrize")]
    internal static class Patch_DispSlimePrize
    {
        [HarmonyPostfix]
        private static void Postfix(ResultHandler __instance)
        {
            if (!ItemsPlugin.CfgEnabled.Value || !ItemsPlugin.CfgGrantAllItemsOnUnitPrize.Value) return;
            ItemRewards.GrantAll(__instance, skipPrizeAlreadyGranted: false);
        }
    }

    /// <summary>
    /// Per-slime item restrictions, both decided by UserUnitData.IsAbleUseItem:
    ///   if (nOccupyingTurn &gt; 0)                       -> item_ng_occupying (kept)
    ///   if (itemType == reset) { if (UseItemTurn == 0) -> item_reset_ng (kept) }
    ///   else                   { if (nRestItemTurn == 0) -> item_no_rest }   budget (removed)
    ///   switch (itemType) { ... case transform: if (nType != s_zako) bRet = false; } (removed)
    ///
    /// The budget is raised so the game's own decrement never empties it; the transform gate is removed by
    /// impersonating s_zako only while the transform item is judged, then restoring nType.
    /// </summary>
    [HarmonyPatch(typeof(UserUnitData), "IsAbleUseItem")]
    internal static class Patch_IsAbleUseItem
    {
        private const int ItemTransform = 6;        // ITEM_TYPE.transform
        private const int UnitZako = 1;             // UNIT_TYPE.s_zako
        private const int FallbackHistorySlots = 10;
        private const int UnlimitedItemUses = 9999;

        private static bool _typeFaked;
        private static int _savedType;
        private static int? _maxItemUse;
        private static readonly HashSet<int> ReportedLevels = new HashSet<int>();

        /// <summary>
        /// UserUnitData.UseItemHistory holds Defs.MAX_ITEM_USE (10) entries, and UserUnitTable.ExecUseItem writes
        /// DataArray[offs].UseItemHistory[UseItemTurn] through the bounds-checked SetAt. Patch_ExecUseItem keeps
        /// UseItemTurn on the last slot, which is what lets the budget exceed the array here.
        /// </summary>
        internal static int HistorySlots => MaxItemUse;

        private static int MaxItemUse
        {
            get
            {
                if (_maxItemUse.HasValue) return _maxItemUse.Value;

                int value = FallbackHistorySlots;
                try
                {
                    var defsType = AccessTools.TypeByName("Defs");
                    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                    MemberInfo member = defsType?.GetProperty("MAX_ITEM_USE", flags);
                    if (member == null) member = defsType?.GetField("MAX_ITEM_USE", flags);
                    if (member is PropertyInfo property) value = Convert.ToInt32(property.GetValue(null));
                    else if (member is FieldInfo field) value = Convert.ToInt32(field.GetValue(null));
                }
                catch (Exception e)
                {
                    ItemsPlugin.Warn($"could not read Defs.MAX_ITEM_USE: {e.Message}");
                }

                if (value <= 0) value = FallbackHistorySlots;
                _maxItemUse = value;
                return value;
            }
        }

        [HarmonyPrefix]
        private static void Prefix(UserUnitData __instance, ITEM_TYPE itemType)
        {
            if (!ItemsPlugin.CfgEnabled.Value) return;

            if (ItemsPlugin.CfgUnlimitedItemUse.Value)
            {
                int rest = NativeMember.GetInt(__instance, "nRestItemTurn", 0);
                if (rest < UnlimitedItemUses)
                {
                    NativeMember.SetInt(__instance, "nRestItemTurn", UnlimitedItemUses);
                    // One line per distinct level keeps the evidence without flooding the log.
                    int level = NativeMember.GetInt(__instance, "nLevel", -1);
                    if (ReportedLevels.Add(level))
                        ItemsPlugin.Instance.Log.LogInfo(
                            $"item budget lifted: level {level} had {rest} use(s) left -> {UnlimitedItemUses} "
                          + $"(history writes clamped to {HistorySlots} slots)");
                }
            }

            if (ItemsPlugin.CfgTransformAnySlime.Value && (int)itemType == ItemTransform)
            {
                int type = NativeMember.GetInt(__instance, "nType", -1);
                if (type != UnitZako)
                {
                    _typeFaked = true;
                    _savedType = type;
                    NativeMember.SetInt(__instance, "nType", UnitZako);
                    ItemsPlugin.Instance.Log.LogInfo($"IsAbleUseItem(transform): slime type {type} treated as s_zako");
                }
            }
        }

        [HarmonyPostfix]
        private static void Postfix(UserUnitData __instance)
        {
            if (!_typeFaked) return;
            _typeFaked = false;

            NativeMember.SetInt(__instance, "nType", _savedType);
            ItemsPlugin.Verbose($"IsAbleUseItem(transform): nType restored to {_savedType}");
        }
    }

    /// <summary>
    /// Keeps the item history write inside its array while the budget runs higher than the array length.
    /// UserUnitTable.ExecUseItem does
    ///   DataArray[offs].UseItemHistory[UseItemTurn] = itemType;   // bounds-checked SetAt
    ///   DataArray[offs].UseItemTurn++;
    /// so pinning UseItemTurn to the last slot first makes further uses overwrite that slot instead of throwing
    /// IndexOutOfRangeException. The reset item then walks at most that many entries while refunding.
    /// </summary>
    [HarmonyPatch(typeof(UserUnitTable), "ExecUseItem")]
    internal static class Patch_ExecUseItem
    {
        [HarmonyPrefix]
        private static void Prefix(int offs)
        {
            if (!ItemsPlugin.CfgEnabled.Value || !ItemsPlugin.CfgUnlimitedItemUse.Value) return;

            var units = UserUnitTable.DataArray as IList<UserUnitData>;
            if (units == null || offs < 0 || offs >= units.Count) return;

            var unit = units[offs];
            if (unit == null) return;

            int lastSlot = Patch_IsAbleUseItem.HistorySlots - 1;
            int turn = NativeMember.GetInt(unit, "UseItemTurn", 0);
            if (turn > lastSlot)
            {
                NativeMember.SetInt(unit, "UseItemTurn", lastSlot);
                ItemsPlugin.Verbose($"item history wrapped: UseItemTurn {turn} -> {lastSlot}");
            }
        }
    }
}
