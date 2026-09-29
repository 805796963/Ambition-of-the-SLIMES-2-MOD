using System;
using HarmonyLib;

namespace SlimesSlimeFix
{
    /// <summary>
    /// Gives a possessed unit the attribute of the slime that possessed it.
    ///
    /// Possession runs through UserUnitData.SetBattleResult_occupying at the end of the battle and
    /// through UnitData.SetOccupied on the field: the unit being taken over has OCCUPY_STATE.occupied
    /// set, takes the slime's side, position, movement and ability, and keeps the slime's unit-box
    /// offset, while the slime's own unit is destroyed a few action steps later. Its own attribute,
    /// however, stays whatever its unit type ships with, and that attribute drives the attribute
    /// advantage in damage and the attribute icon on every panel.
    ///
    /// This patch writes the configured attribute into that one field, before the game derives
    /// anything from it, and only when the possessing slime's attribute is the configured source
    /// attribute. Everything else about the unit is untouched. The same write also reaches the
    /// carried-over unit-box record, because the battle-result step copies the field.
    /// </summary>
    internal static class PossessAttr
    {
        /// <summary>Whether the possession attribute patch may change anything at all.</summary>
        internal static bool Active =>
            SlimeFixPlugin.CfgEnabled != null && SlimeFixPlugin.CfgEnabled.Value
            && SlimeFixPlugin.CfgPossessEnabled != null && SlimeFixPlugin.CfgPossessEnabled.Value;

        /// <summary>
        /// Attribute the possessing slime must have for the patch to apply, or a negative value to
        /// apply for a slime of any attribute.
        /// </summary>
        internal static int SourceAttribute => SlimeFixPlugin.CfgPossessSlimeAttr?.Value ?? (int)ATTR_TYPE.sun;

        /// <summary>Attribute written into the possessed unit.</summary>
        internal static int GrantAttribute => SlimeFixPlugin.CfgPossessGrantAttr?.Value ?? (int)ATTR_TYPE.sun;

        internal static void Note(UNIT_TYPE targetType, int slimeAttribute, int before, int after) =>
            SlimeFixPlugin.Verbose($"possess attribute: slime {Describe(slimeAttribute)},"
                                 + $" unit {targetType} '{Name(targetType)}'"
                                 + $" {Describe(before)} -> {Describe(after)}");

        /// <summary>
        /// Attribute of the slime that took over <paramref name="target"/>, or null when it cannot be
        /// read. <paramref name="slimeUnitOffs"/> is the slime's battle offset, or -1 when a unit
        /// carried from an earlier battle is being deployed, in which case the unit-box record holds
        /// the slime's own attribute.
        /// </summary>
        internal static int? SlimeAttribute(UnitData target, int slimeUnitOffs)
        {
            if (target == null) return null;

            if (slimeUnitOffs >= 0)
            {
                var battle = target.BattleFlowComponent;
                if (battle == null) return null;

                var units = battle.UnitDataArray;
                if (units == null || slimeUnitOffs >= units.Length) return null;

                var slime = units[slimeUnitOffs];
                return slime == null ? (int?)null : (int)slime.nAttr;
            }

            var box = UserUnitTable.DataArray;
            int offs = target.m_nUserUnitOffs;
            if (box == null || offs < 0 || offs >= box.Length) return null;

            var record = box[offs];
            return record == null ? (int?)null : (int)record.nAttr;
        }

        internal static string Describe(int attribute)
        {
            try { return $"{attribute}:{EtcFunc.GetAttrSpriteName((ATTR_TYPE)attribute)}"; }
            catch (Exception) { return attribute.ToString(); }
        }

        private static string Name(UNIT_TYPE type)
        {
            try { return Strs.GetUnitTypeName(type); }
            catch (Exception) { return "?"; }
        }
    }

    /// <summary>
    /// Writes the configured attribute into a unit that is being occupied.
    ///
    /// The write happens in a prefix so the game's own follow-up work (the hyper-mode check, the
    /// ability parameters derived from "slime attribute versus occupied attribute") already sees the
    /// new attribute instead of the one the unit type ships with.
    /// </summary>
    [HarmonyPatch(typeof(UnitData), nameof(UnitData.SetOccupied))]
    internal static class Patch_UnitData_SetOccupied
    {
        private static void Prefix(UnitData __instance, int slimeUnitOffs)
        {
            if (!PossessAttr.Active) return;

            try
            {
                int? slimeAttribute = PossessAttr.SlimeAttribute(__instance, slimeUnitOffs);
                if (slimeAttribute == null) return;

                int before = (int)__instance.nAttr;
                int requested = PossessAttr.SourceAttribute;
                bool applies = requested < 0 || slimeAttribute.Value == requested;
                int after = applies ? PossessAttr.GrantAttribute : before;

                PossessAttr.Note(__instance.nType, slimeAttribute.Value, before, after);
                if (after != before) __instance.nAttr = (ATTR_TYPE)after;
            }
            catch (Exception e)
            {
                SlimeFixPlugin.Warn("possess attribute patch failed: " + e.Message);
            }
        }
    }
}
