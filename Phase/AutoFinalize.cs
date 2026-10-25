using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Actions;

namespace HandsOffEarth.Phase
{
    // Finishes the player's mission phase for them when every councilor already has orders (automated or
    // repeating), unless something happened on Earth since the last phase that the player should look at
    public static class AutoFinalize
    {
        // What the player held when they last finalized a phase. Kept for the session only: after a load the
        // faction is a new object, so the first phase just sets a new baseline.
        static TIFactionState snapshotFaction;
        static HashSet<TIControlPoint> lastControlPoints;
        static HashSet<TICouncilorState> lastCouncilors;

        // Hostile missions resolved against our councilors since the last snapshot
        static readonly List<string> threats = new List<string>();
        static readonly HashSet<TICouncilorState> targeted = new HashSet<TICouncilorState>();

        internal static void OnPhaseStart()
        {
            if (!Main.enabled || !Main.settings.autoFinalize)
                return;
            var faction = GameControl.control.activePlayer;
            if (!TIGameState.Valid(faction) || faction.defeated)
                return;

            var councilors = faction.activeCouncilors;
            var idle = councilors.Where(c => !c.HasMission && !c.permanentDefenseMode).ToList();
            if (councilors.Count == 0 || idle.Count > 0)
            {
                Main.Log($"Mission phase left open: {string.Join(", ", idle.Select(c => c.displayName))} without orders");
                return;
            }

            var alarms = Alarms(faction);
            if (alarms.Count > 0)
            {
                foreach (var alarm in alarms)
                {
                    Main.Log("Mission phase left open: " + alarm);
                    GameStateManager.NotificationQueue().councilorMessages.Enqueue(new CouncilorMessage(councilors[0], alarm));
                }
                return;
            }

            int automated = councilors.Count(c => c.permanentDefenseMode);
            Main.Log($"Finishing mission phase: {automated} automated, {councilors.Count - automated} repeating");
            // Go through the mission screen, as its Confirm button does, so the screen's controls close too;
            // finalizing directly left "Confirm assignments" showing while time ran on
            var screen = UnityEngine.Resources.FindObjectsOfTypeAll<CouncilorMissionCanvasController>().FirstOrDefault();
            if (screen != null)
                screen.PlayerConfirmsMissionAssignments();
            else
                faction.playerControl.StartAction(new FinalizeCouncilorMissions(faction));
        }

        internal static void TakeSnapshot(TIFactionState faction)
        {
            snapshotFaction = faction;
            lastControlPoints = new HashSet<TIControlPoint>(faction.controlPoints);
            lastCouncilors = new HashSet<TICouncilorState>(faction.activeCouncilors);
            threats.Clear();
            targeted.Clear();
        }

        // Nations where the player has lost a control point since the last snapshot
        internal static IEnumerable<TINationState> LostControlPointNations(TIFactionState faction)
        {
            if (faction != snapshotFaction)
                return Enumerable.Empty<TINationState>();
            return lastControlPoints.Where(cp => cp.faction != faction).Select(cp => cp.nation).Distinct();
        }

        internal static bool WasTargeted(TICouncilorState councilor) => targeted.Contains(councilor);

        internal static void OnMissionResolved(TIMissionState mission, MissionResult result)
        {
            var faction = GameControl.control.activePlayer;
            if (faction != snapshotFaction || !result.Attempted)
                return;
            var attacker = mission.councilor;
            var target = mission.target;
            if (!TIGameState.Valid(attacker) || !TIGameState.Valid(target) || !target.isCouncilorState)
                return;
            var victim = target.ref_councilor;
            if (victim.faction != faction || attacker.faction == faction || faction.permanentAlly(attacker.faction)
                || TIFactionState.friendlyCouncilorToCouncilorMissions.Contains(mission.missionTemplate.dataName))
                return;
            targeted.Add(victim);
            threats.Add($"{attacker.faction.displayName} ran {mission.missionTemplate.displayName} against {victim.displayName}");
        }

        static List<string> Alarms(TIFactionState faction)
        {
            var alarms = new List<string>();
            if (faction != snapshotFaction)
                return alarms;

            if (Main.settings.pauseOnLostControlPoint)
            {
                foreach (var cp in lastControlPoints.Where(cp => cp.faction != faction))
                    alarms.Add($"Lost a control point in {cp.nation.displayName}");
            }
            if (Main.settings.pauseOnCouncilorDanger)
            {
                foreach (var c in lastCouncilors.Where(c => !c.active || c.faction != faction))
                    alarms.Add($"{c.displayName} is no longer available ({Reason(c)})");
                alarms.AddRange(threats);
            }
            return alarms;
        }

        static string Reason(TICouncilorState c)
        {
            if (c.status == CouncilorStatus.Dead)
                return "killed";
            if (c.detained)
                return "detained";
            return "left the council";
        }
    }

    // Runs after the game has re-issued repeat orders and picked missions for automated councilors
    [HarmonyPatch(typeof(TIMissionPhaseState), "ContinueNewMissionPhase")]
    static class ContinueNewMissionPhase_Patch
    {
        static void Postfix()
        {
            try
            {
                AutoFinalize.OnPhaseStart();
            }
            catch (Exception e)
            {
                Main.Error("Auto-finalize failed: " + e);
            }
        }
    }

    // Covers both our own finalize and the player clicking the button
    [HarmonyPatch(typeof(FinalizeCouncilorMissions), nameof(FinalizeCouncilorMissions.Execute))]
    static class FinalizeCouncilorMissions_Patch
    {
        static void Postfix(TIFactionState ___faction)
        {
            if (TIGameState.Valid(___faction) && ___faction == GameControl.control.activePlayer)
                AutoFinalize.TakeSnapshot(___faction);
        }
    }

    [HarmonyPatch(typeof(TIMissionState), nameof(TIMissionState.ResolveMission))]
    static class ResolveMission_Patch
    {
        static void Postfix(TIMissionState __instance, MissionResult __result)
        {
            try
            {
                AutoFinalize.OnMissionResolved(__instance, __result);
            }
            catch (Exception e)
            {
                Main.Error("Mission tracking failed: " + e);
            }
        }
    }
}
