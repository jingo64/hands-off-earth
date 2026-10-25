using System;
using System.Linq;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;

namespace HandsOffEarth.Priorities
{
    // Gives each opted-in nation the preset its first matching rule names. Control points that already
    // have that preset's pips are left alone; nothing happens when no rule matches
    public static class RuleApplier
    {
        // Returns true if any control point changed
        public static bool Apply(TINationState nation, TIFactionState faction)
        {
            if (!Main.enabled || nation == null || !TIGameState.Valid(faction) || !OptIns.IsOn(nation))
                return false;
            var index = PriorityRules.Evaluate(nation, faction, out var preset);
            if (index < 0)
                return false;
            var changed = 0;
            foreach (var cp in nation.controlPoints.Where(cp => cp.faction == faction).ToList())
            {
                if (preset.MatchesPreset(cp.controlPointPriorities, nation.InvalidPriorities))
                    continue;
                faction.playerControl.StartAction(new ApplyPriorityPresetToControlPoint(cp, faction, preset.dataName));
                changed++;
            }
            if (changed > 0)
                Main.Log($"{nation.displayName}: rule {index + 1} matches, applied {preset.displayName} to {changed} control point(s)");
            return changed > 0;
        }

        public static void ApplyAll(TIFactionState faction)
        {
            if (!Main.enabled || !TIGameState.Valid(faction))
                return;
            foreach (var nation in GameStateManager.AllNations().Where(OptIns.IsOn))
            {
                try
                {
                    Apply(nation, faction);
                }
                catch (Exception e)
                {
                    Main.Error($"Applying priority rules to {nation.displayName} failed: {e}");
                }
            }
        }
    }

    [HarmonyPatch(typeof(TIFactionState), "MonthlyFactionUpdate")]
    static class MonthlyFactionUpdate_Patch
    {
        static void Postfix(TIFactionState __instance)
        {
            if (__instance == GameControl.control.activePlayer)
                RuleApplier.ApplyAll(__instance);
        }
    }
}
