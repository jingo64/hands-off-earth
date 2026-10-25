using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace HandsOffEarth
{
    public static class Main
    {
        public static bool enabled;
        public static UnityModManager.ModEntry mod;
        public static Settings settings;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            mod = modEntry;
            settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            Priorities.PriorityRules.Load();
            new Harmony(modEntry.Info.Id).PatchAll(Assembly.GetExecutingAssembly());
            modEntry.Logger.Log("HandsOffEarth loaded");
            return true;
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;
            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            settings.autoFinalize = GUILayout.Toggle(settings.autoFinalize,
                " Finish the mission phase automatically when every councilor has orders");
            GUILayout.Label("Still stop for the mission phase if, since the last one:");
            settings.pauseOnLostControlPoint = GUILayout.Toggle(settings.pauseOnLostControlPoint,
                " a control point was lost");
            settings.pauseOnCouncilorDanger = GUILayout.Toggle(settings.pauseOnCouncilorDanger,
                " a councilor was lost, or an enemy mission targeted one");
            GUILayout.Space(10);
            settings.smartAutomation = GUILayout.Toggle(settings.smartAutomation,
                " Smarter automation: only defend where there's a threat, and don't repeat the same mission on a quiet target");
            GUILayout.Label("Also let automated councilors use (only in nations where you hold control points):");
            settings.allowGainInfluence = GUILayout.Toggle(settings.allowGainInfluence, " Gain Influence");
            settings.allowCrackdown = GUILayout.Toggle(settings.allowCrackdown, " Crackdown");
            settings.allowPurge = GUILayout.Toggle(settings.allowPurge, " Purge");
            settings.allowDetain = GUILayout.Toggle(settings.allowDetain, " Detain");
        }

        static void OnSaveGUI(UnityModManager.ModEntry modEntry) => settings.Save(modEntry);

        public static void Log(string message) => mod?.Logger.Log(message);

        public static void Error(string message) => mod?.Logger.Error(message);
    }

    public class Settings : UnityModManager.ModSettings
    {
        public bool autoFinalize = true;
        public bool pauseOnLostControlPoint = true;
        public bool pauseOnCouncilorDanger = true;
        public bool smartAutomation = true;
        public bool allowGainInfluence;
        public bool allowCrackdown;
        public bool allowPurge;
        public bool allowDetain;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }
}
