using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;

namespace HandsOffEarth.Priorities
{
    // Which nations follow the priority rules. Stored in a small file next to each save
    // ("<save>.handsoff"), so nothing is added to the save itself
    public static class OptIns
    {
        // Not .json or .gz, which the load menu would list as saves
        const string Extension = ".handsoff";

        static readonly HashSet<string> nations = new HashSet<string>();

        public static bool IsOn(TINationState nation) => nation != null && nations.Contains(nation.templateName);

        public static void Set(TINationState nation, bool on)
        {
            if (on)
                nations.Add(nation.templateName);
            else
                nations.Remove(nation.templateName);
        }

        public static void Clear() => nations.Clear();

        public static void Write(string savePath)
        {
            var path = savePath + Extension;
            try
            {
                if (nations.Count > 0)
                    File.WriteAllLines(path, nations.OrderBy(n => n));
                else if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception e)
            {
                Main.Error($"Couldn't write {path}: {e}");
            }
        }

        public static void Read(string savePath)
        {
            nations.Clear();
            var path = savePath + Extension;
            try
            {
                if (File.Exists(path))
                    nations.UnionWith(File.ReadAllLines(path).Select(l => l.Trim()).Where(l => l.Length > 0));
            }
            catch (Exception e)
            {
                Main.Error($"Couldn't read {path}: {e}");
            }
        }
    }

    [HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.ClearAllGameStates))]
    static class ClearAllGameStates_Patch
    {
        static void Postfix() => OptIns.Clear();
    }

    [HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.LoadAllGameStates))]
    static class LoadAllGameStates_Patch
    {
        static void Postfix(string filepath, bool __result)
        {
            if (__result)
                OptIns.Read(filepath);
        }
    }

    [HarmonyPatch(typeof(GameStateManager), nameof(GameStateManager.SaveAllGameStates))]
    static class SaveAllGameStates_Patch
    {
        static void Postfix(string filepath, bool __result)
        {
            if (__result)
                OptIns.Write(filepath);
        }
    }
}
