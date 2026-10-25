using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;
using PavonisInteractive.TerraInvicta.Tasks;
using UnityEngine;

namespace HandsOffEarth.Automation
{
    // Replaces the game's automate choice for the player's councilors. It's a port of
    // TICouncilorState.SelectPermanentDefenseModeMission with two changes to the scores:
    //  - Defend Interests and Protect only score fully where something threatens the target
    //  - repeating last phase's mission on the same target scores lower when nothing threatens it
    // It can also allow a few extra missions, restricted to nations where the player holds control points.
    public static class CouncilorBrain
    {
        const float UnthreatenedDefense = 0.2f;
        const float Repeat = 0.5f;

        static readonly HashSet<string> GuardMissions = new HashSet<string> { "DefendInterests", "Protect" };

        // Returns false when it has nothing to offer, so the game's own choice runs instead
        public static bool Select(TICouncilorState councilor)
        {
            var faction = councilor.faction;
            var possible = new Dictionary<AIForcedMissionEntry, float>();
            var notes = new Dictionary<AIForcedMissionEntry, string>();
            var extras = ExtraMissions();
            var missions = councilor.GetPossibleMissionList(filterForCouncilorConditions: true)
                .Where(x => (x.allowedForAutoDefense || extras.Contains(x.dataName))
                    && !councilor.missionsExcludedFromDefenseMode.Contains(x.dataName))
                .ToList();
            if (missions.Count == 0)
                return false;

            float campaignDuration_years = TITimeState.CampaignDuration_years_Exact();
            TIRegionState recentAlienSite = faction.MostRecentAlienSite().ref_region;
            float timeSinceAlienSite_days = faction.MostRecentAlienSiteAge_days();
            bool force = faction.isActivePlayer && AICouncilorMissionPlanner.cachedPayoffFrame != TIFrameCounter.FrameCount;
            AICouncilorMissionPlanner.singleton.SetRawNationPayoffsByFaction(faction, force);
            AICouncilorMissionPlanner.singleton.SetPayoffValues(faction, force);
            bool noControlPoints = faction.controlPoints.Count == 0;
            var threats = new Threats(faction);

            foreach (var mission in missions)
            {
                if (!mission.CanAfford(faction))
                    continue;
                bool extra = !mission.allowedForAutoDefense;
                foreach (var target in mission.GetValidTargets(councilor))
                {
                    if (!TargetAllowed(councilor, mission, target, extra, noControlPoints))
                        continue;

                    int sameMission = 0;
                    bool doubledUp = false;
                    foreach (var other in faction.councilors)
                    {
                        if (other == councilor || other.activeMission == null)
                            continue;
                        if (AIEvaluators.AI_ShouldAvoidDoublingUpMissionTarget(other, other.activeMission.missionTemplate, other.activeMission.target,
                                other.activeMission.GetSuccessChance(), councilor, mission, target))
                        {
                            doubledUp = true;
                            break;
                        }
                        if (other.activeMission.missionTemplate == mission && other.activeMission.target == target)
                            sameMission++;
                    }
                    if (doubledUp)
                        continue;

                    var key = new AIForcedMissionEntry(councilor, target, mission);
                    float score = AICouncilorMissionPlanner.singleton.GetPayoffForMissionTarget(faction, mission, councilor, target,
                        new List<TIMissionTemplate>(), new List<TIMissionTemplate>(), null, new List<CampaignMilestone>(), campaignDuration_years,
                        huntingForAlienActivity: false, 0f, new List<TIFactionState>(), recentAlienSite, timeSinceAlienSite_days, capturingNeutralNations: false);
                    if (sameMission > 0)
                        score /= sameMission + 8;
                    if (mission.ContestedMission)
                    {
                        float chance = mission.resolutionMethod.GetSuccessChance(mission, councilor, target);
                        score *= chance * chance;
                    }

                    string note = "";
                    bool threatened = threats.Threatened(target);
                    if (GuardMissions.Contains(mission.dataName) && !threatened)
                    {
                        score *= UnthreatenedDefense;
                        note += " quiet";
                    }
                    if (!threatened && mission.dataName == councilor.priorMissionTemplateName && target == councilor.priorMissionTarget)
                    {
                        score *= Repeat;
                        note += " repeat";
                    }
                    if (score > 0f)
                    {
                        possible.Add(key, score);
                        notes.Add(key, note);
                    }
                }
            }
            if (possible.Count == 0)
                return false;

            float best = possible.Values.Max();
            var pick = possible.Where(x => x.Value == best).SelectRandomItem().Key;
            float resources = Resources(councilor, pick, best);
            Main.Log($"{councilor.displayName}: {pick.mission.dataName} on {Name(pick.target)} (score {best:0.##}{notes[pick]})"
                + $" over {possible.Count - 1} other options");
            faction.playerControl.StartAction(new AssignCouncilorToMission(councilor, pick.mission, pick.target, resources));
            return true;
        }

        static HashSet<string> ExtraMissions()
        {
            var s = Main.settings;
            var extras = new HashSet<string>();
            if (s.allowGainInfluence) extras.Add("GainInfluence");
            if (s.allowCrackdown) extras.Add("Crackdown");
            if (s.allowPurge) extras.Add("Purge");
            if (s.allowDetain) extras.Add("Detain");
            return extras;
        }

        static bool TargetAllowed(TICouncilorState councilor, TIMissionTemplate mission, TIGameState target, bool extra, bool noControlPoints)
        {
            var faction = councilor.faction;
            if (extra)
            {
                // Extra missions stay housekeeping: only in nations where we already hold control points,
                // and only against factions that aren't permanent allies
                var nation = target.ref_nation;
                if (nation == null || nation.CountFactionControlPoints(faction, includeDisabled: true, includePermanentAllies: false, includeDefended: true) == 0)
                    return false;
                if (target.isControlPointState)
                {
                    var owner = target.ref_controlPoint.faction;
                    return owner != null && owner != faction && !faction.permanentAlly(owner);
                }
                if (target.isCouncilorState)
                {
                    var other = target.ref_councilor.faction;
                    return other != null && other != faction && !faction.permanentAlly(other);
                }
                return target.isNationState;
            }

            // The game's own filters
            if (target.isNationState || target.isRegionState || target.isControlPointState)
            {
                if (noControlPoints)
                {
                    if (target.ref_nation.controlPoints.Any(x => x.faction != null))
                        return false;
                }
                else if (target.ref_nation.CountFactionControlPoints(faction, includeDisabled: true, includePermanentAllies: false, includeDefended: true) == 0)
                    return false;
            }
            else if (target.isCouncilorState)
            {
                if (target.ref_councilor.faction != faction)
                    return false;
            }
            else if (target.isRegionAlienEntity && (!target.isRegionXenoformingState
                || target.ref_nation.CountFactionControlPoints(faction, includeDisabled: true, includePermanentAllies: false, includeDefended: true) == 0))
                return false;
            if ((councilor.OnAShip && (target.isHabState || target.isHabModuleState || target.isHabSiteState))
                || (councilor.InAHab && (target.isSpaceShipState || target.isHabState)))
                return false;
            return true;
        }

        // How much to spend on the mission, as the game does it
        static float Resources(TICouncilorState councilor, AIForcedMissionEntry pick, float score)
        {
            var faction = councilor.faction;
            var mission = pick.mission;
            if (mission.ContestedMission && mission.cost.resourceType != FactionResource.None)
            {
                float spend = 0f;
                float bestValue = 0f;
                int steps = councilor.CurrentMaxSliderSteps(mission);
                float budget = Mathf.Min(faction.GetCurrentResourceAmount(mission.primaryResource), faction.GetDailyIncome(mission.primaryResource) * 7f);
                for (int step = 0; step <= steps; step++)
                {
                    float cost = mission.cost.GetCost(step, councilor);
                    if (cost > budget)
                        continue;
                    float costValue = cost * AIEvaluators.GetAIRelativeValuation(mission.cost.resourceType) * 5f;
                    float chance = mission.resolutionMethod.GetSuccessChance(mission, pick.councilor, pick.target, cost);
                    float value = chance < 1f / 3f ? float.Epsilon : score * chance - costValue;
                    if (value > bestValue)
                    {
                        spend = (int)cost;
                        bestValue = value;
                    }
                }
                return spend;
            }
            if (mission.hasCost)
                return mission.cost.GetCost(0f, pick.councilor, pick.target);
            return 0f;
        }

        static string Name(TIGameState target)
        {
            if (target.isControlPointState)
                return $"{target.ref_nation.displayName} control point";
            return target.displayName;
        }

        // Whether something threatens a target the player might want to guard
        class Threats
        {
            readonly TIFactionState faction;
            readonly HashSet<TINationState> nations = new HashSet<TINationState>();

            public Threats(TIFactionState faction)
            {
                this.faction = faction;
                foreach (var nation in Phase.AutoFinalize.LostControlPointNations(faction))
                    nations.Add(nation);
                foreach (var nation in faction.nationsWithMyControlPoints)
                {
                    if (nation.controlPoints.Any(cp => cp.faction != null && cp.faction != faction && !faction.permanentAlly(cp.faction)))
                        nations.Add(nation);
                }
                foreach (var other in GameStateManager.IterateByClass<TICouncilorState>())
                {
                    if (other.faction == null || other.faction == faction || faction.permanentAlly(other.faction) || !other.active)
                        continue;
                    var nation = other.ref_nation;
                    if (nation != null && faction.HasIntelOnCouncilorLocation(other))
                        nations.Add(nation);
                }
            }

            public bool Threatened(TIGameState target)
            {
                if (target.isCouncilorState && Phase.AutoFinalize.WasTargeted(target.ref_councilor))
                    return true;
                var nation = target.ref_nation;
                // Space targets (fleets, habs) have no nation; leave those to the game's scoring
                return nation == null || nations.Contains(nation);
            }
        }
    }

    [HarmonyPatch(typeof(TICouncilorState), nameof(TICouncilorState.SelectPermanentDefenseModeMission))]
    static class SelectPermanentDefenseModeMission_Patch
    {
        static bool Prefix(TICouncilorState __instance, ref bool __result)
        {
            if (!Main.enabled || !Main.settings.smartAutomation || __instance.faction != GameControl.control.activePlayer)
                return true;
            try
            {
                if (!CouncilorBrain.Select(__instance))
                    return true;
                __result = true;
                return false;
            }
            catch (Exception e)
            {
                Main.Error($"Automation failed for {__instance.displayName}, using the game's choice: {e}");
                return true;
            }
        }
    }
}
