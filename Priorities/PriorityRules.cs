using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using PavonisInteractive.TerraInvicta;
using UnityEngine;

namespace HandsOffEarth.Priorities
{
    // A nation stat that rules can test
    public class Metric
    {
        public string id;
        public string label;
        public Func<TINationState, float> value;
        public string format;

        public string Format(TINationState nation) => value(nation).ToString(format);

        public static readonly List<Metric> All = new List<Metric>
        {
            new Metric { id = "GDP", label = "GDP ($ billion)", value = n => (float)(n.GDP / 1e9), format = "N0" },
            new Metric { id = "GDPPerCapita", label = "GDP per capita ($)", value = n => n.perCapitaGDP, format = "N0" },
            new Metric { id = "Population", label = "Population (millions)", value = n => n.population_Millions, format = "N0" },
            new Metric { id = "Research", label = "Research per month", value = n => n.research_month, format = "N1" },
            new Metric { id = "MissionControl", label = "Mission control", value = n => n.missionControl, format = "N0" },
            new Metric { id = "MilitaryTech", label = "Military tech", value = n => n.militaryTechLevel, format = "N2" },
            // Shown the way the nation screen shows it: higher is cleaner
            new Metric { id = "Sustainability", label = "Sustainability", value = n => n.sustainability <= 0f ? 10f : Mathf.Min(10f, 1f / n.sustainability), format = "N2" },
            new Metric { id = "Cohesion", label = "Cohesion", value = n => n.cohesion, format = "N2" },
            new Metric { id = "Unrest", label = "Unrest", value = n => n.unrest, format = "N2" },
            new Metric { id = "Democracy", label = "Democracy", value = n => n.democracy, format = "N2" },
            new Metric { id = "Education", label = "Education", value = n => n.education, format = "N2" },
            new Metric { id = "Inequality", label = "Inequality", value = n => n.inequality, format = "N2" },
        };

        public static Metric Find(string id) => All.FirstOrDefault(m => m.id == id);
    }

    [Serializable]
    public class Condition
    {
        public string metric = "GDPPerCapita";
        public bool lessThan = true;
        public float value;

        public bool Test(TINationState nation)
        {
            var m = Metric.Find(metric);
            if (m == null)
                return false;
            var v = m.value(nation);
            return lessThan ? v < value : v >= value;
        }
    }

    [Serializable]
    public class Rule
    {
        // The preset's display name, so rules carry over to campaigns where you saved a preset with the same name
        public string preset = "";
        public List<Condition> conditions = new List<Condition>();

        public bool Matches(TINationState nation) => conditions.All(c => c.Test(nation));
    }

    [Serializable]
    public class RuleSet
    {
        public List<Rule> rules = new List<Rule>();
    }

    // The rule list, saved with Newtonsoft.Json (Unity's JsonUtility can't see types in mod assemblies
    // and writes "{}"). Kept outside the mod folder, so Workshop updates don't wipe it, and shared by all campaigns
    public static class PriorityRules
    {
        public static RuleSet current = new RuleSet();

        static string FilePath => Path.Combine(Application.persistentDataPath, "HandsOffEarth", "PriorityRules.json");

        public static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    current = JsonConvert.DeserializeObject<RuleSet>(File.ReadAllText(FilePath)) ?? new RuleSet();
            }
            catch (Exception e)
            {
                Main.Error("Couldn't read priority rules: " + e);
            }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(current, Formatting.Indented));
            }
            catch (Exception e)
            {
                Main.Error("Couldn't save priority rules: " + e);
            }
        }

        // Presets the player can pick for this nation, by display name
        public static TIPriorityPresetTemplate FindPreset(string displayName, TINationState nation, TIFactionState faction) =>
            TemplateManager.IterateByClass<TIPriorityPresetTemplate>()
                .FirstOrDefault(p => p.displayName == displayName && p.ValidPreset(nation, faction));

        public static List<string> PresetNames(TIFactionState faction) =>
            TemplateManager.IterateByClass<TIPriorityPresetTemplate>()
                .Where(p => p.ValidPresetForFaction(faction))
                .OrderByDescending(p => p.customDesign)
                .Select(p => p.displayName)
                .Distinct()
                .ToList();

        // The first rule, top to bottom, whose conditions all hold and whose preset exists. -1 if none
        public static int Evaluate(TINationState nation, TIFactionState faction, out TIPriorityPresetTemplate preset)
        {
            for (int i = 0; i < current.rules.Count; i++)
            {
                var rule = current.rules[i];
                if (!rule.Matches(nation))
                    continue;
                preset = FindPreset(rule.preset, nation, faction);
                if (preset != null)
                    return i;
            }
            preset = null;
            return -1;
        }
    }
}
