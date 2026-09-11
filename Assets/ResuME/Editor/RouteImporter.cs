using System;
using System.Collections.Generic;
using System.IO;
using ResuME.Data;
using UnityEditor;
using UnityEngine;

namespace ResuME.Editor
{
    /// <summary>
    /// Turns <c>Datas/route.json</c> (hand-edited, no compilation needed) into the Route asset the
    /// game reads. Runs from the menu and automatically whenever the JSON file is saved.
    /// Unknown skill or trait ids abort the import with an explicit error.
    /// </summary>
    public static class RouteImporter
    {
        public const string JsonPath = "Assets/ResuME/Datas/route.json";
        public const string AssetPath = "Assets/ResuME/Datas/Route.asset";

        #region JSON shape (JsonUtility: arrays, no dictionaries, enums as strings)

        [Serializable] private class RouteFile
        {
            public float screenWidthUnits = 20f;
            public float buildingHeightUnits = 6f;
            public float roadHeightUnits = 2f;
            public float secondsPerScreen = 3f;
            public float gapScreens = 0.5f;
            public int masteryScaleMax = 5;
            public ContactFile contact = new ContactFile();
            public List<SkillFile> skills = new List<SkillFile>();
            public List<TraitFile> traits = new List<TraitFile>();
            public List<StopFile> stops = new List<StopFile>();
        }

        [Serializable] private class ContactFile { public string email; public string linkedin; public string cvUrl; public string closingLine; }
        [Serializable] private class SkillFile { public string id; public string label; public string category; public string description; }
        [Serializable] private class TraitFile { public string id; public string label; public string description; }
        [Serializable] private class RewardFile { public string id; public string title; public string description; }
        [Serializable] private class ProjectFile { public string id; public string title; public string description; public List<string> skills = new List<string>(); public List<string> platforms = new List<string>(); }

        [Serializable] private class StopFile
        {
            public string id;
            public string title;
            public string years;
            public int level;
            public string kind;
            public float widthScreens = 1f;
            public float gapAfterScreens = -1f;
            public bool founder;
            public List<string> traitsBefore = new List<string>();
            public List<string> traitsAfter = new List<string>();
            public RewardFile classChange;
            public RewardFile passive;
            public List<ProjectFile> projects = new List<ProjectFile>();
            public string notes;
        }

        #endregion

        [MenuItem("ResuME/Route/Import route.json")]
        public static void ImportFromMenu()
        {
            RouteDefinition route = Import();
            if (route != null) AssetDatabase.SaveAssets();
        }

        /// <summary>Imports the JSON into the Route asset (created if missing). Returns null on error.</summary>
        public static RouteDefinition Import()
        {
            if (!File.Exists(JsonPath))
            {
                Debug.LogError($"[RouteImporter] {JsonPath} not found.");
                return null;
            }

            RouteFile file;
            try
            {
                file = JsonUtility.FromJson<RouteFile>(File.ReadAllText(JsonPath));
            }
            catch (Exception e)
            {
                Debug.LogError($"[RouteImporter] {JsonPath} is not valid JSON: {e.Message}");
                return null;
            }

            var errors = new List<string>();
            var traits = new Dictionary<string, TraitFile>();
            foreach (TraitFile trait in file.traits)
            {
                if (string.IsNullOrEmpty(trait.id)) errors.Add("a trait has no id");
                else if (!traits.TryAdd(trait.id, trait)) errors.Add($"duplicate trait id '{trait.id}'");
            }

            var skills = new List<SkillDefinition>();
            var skillIds = new HashSet<string>();
            foreach (SkillFile skill in file.skills)
            {
                if (string.IsNullOrEmpty(skill.id)) { errors.Add("a skill has no id"); continue; }
                if (!skillIds.Add(skill.id)) { errors.Add($"duplicate skill id '{skill.id}'"); continue; }
                if (!Enum.TryParse(skill.category, true, out SkillCategory category))
                {
                    errors.Add($"skill '{skill.id}': unknown category '{skill.category}'");
                    category = SkillCategory.None;
                }
                skills.Add(new SkillDefinition { id = skill.id, label = skill.label, category = category, description = skill.description });
            }

            var stops = new List<StopDefinition>();
            var stopIds = new HashSet<string>();
            foreach (StopFile s in file.stops)
            {
                if (string.IsNullOrEmpty(s.id)) { errors.Add("a stop has no id"); continue; }
                if (!stopIds.Add(s.id)) errors.Add($"duplicate stop id '{s.id}'");
                if (!Enum.TryParse(s.kind, true, out StopKind kind))
                {
                    errors.Add($"stop '{s.id}': unknown kind '{s.kind}'");
                    kind = StopKind.Traverse;
                }

                StopDefinition stop = new StopDefinition
                {
                    id = s.id, title = s.title, years = s.years, level = s.level, kind = kind,
                    widthScreens = s.widthScreens <= 0f ? 1f : s.widthScreens, gapAfterScreens = s.gapAfterScreens, founder = s.founder, notes = s.notes
                };

                foreach (string traitId in s.traitsBefore) AddTrait(stop.traitsBefore, traitId, traits, s.id, errors);
                foreach (string traitId in s.traitsAfter) AddTrait(stop.traitsAfter, traitId, traits, s.id, errors);

                if (s.classChange != null && !string.IsNullOrEmpty(s.classChange.id))
                    stop.rewards.Add(new RewardDefinition { kind = RewardKind.ClassChange, id = s.classChange.id, label = s.classChange.title, description = s.classChange.description });
                if (s.passive != null && !string.IsNullOrEmpty(s.passive.id))
                    stop.rewards.Add(new RewardDefinition { kind = RewardKind.Passive, id = s.passive.id, label = s.passive.title, description = s.passive.description });

                foreach (ProjectFile p in s.projects)
                {
                    if (string.IsNullOrEmpty(p.id)) { errors.Add($"stop '{s.id}': a project has no id"); continue; }
                    ProjectDefinition project = new ProjectDefinition { id = p.id, title = p.title, description = p.description, platforms = new List<string>(p.platforms) };
                    foreach (string skillId in p.skills)
                    {
                        if (!skillIds.Contains(skillId)) { errors.Add($"project '{p.id}': unknown skill '{skillId}'"); continue; }
                        if (project.skillIds.Contains(skillId)) { errors.Add($"project '{p.id}': skill '{skillId}' listed twice"); continue; }
                        project.skillIds.Add(skillId);
                    }
                    stop.projects.Add(project);
                }
                stops.Add(stop);
            }

            if (errors.Count > 0)
            {
                Debug.LogError($"[RouteImporter] {errors.Count} error(s) in {JsonPath}, asset not updated:\n- " + string.Join("\n- ", errors));
                return null;
            }

            RouteDefinition route = AssetDatabase.LoadAssetAtPath<RouteDefinition>(AssetPath);
            if (route == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
                route = ScriptableObject.CreateInstance<RouteDefinition>();
                AssetDatabase.CreateAsset(route, AssetPath);
            }

            route.screenWidthUnits = file.screenWidthUnits;
            route.buildingHeightUnits = file.buildingHeightUnits;
            route.roadHeightUnits = file.roadHeightUnits;
            route.secondsPerScreen = file.secondsPerScreen;
            route.gapScreens = file.gapScreens;
            route.masteryScaleMax = file.masteryScaleMax;
            route.contactEmail = file.contact != null ? file.contact.email : "";
            route.contactLinkedIn = file.contact != null ? file.contact.linkedin : "";
            route.contactCvUrl = file.contact != null ? file.contact.cvUrl : "";
            route.closingLine = file.contact != null ? file.contact.closingLine : "";
            route.skills = skills;
            route.stops = stops;
            EditorUtility.SetDirty(route);

            Debug.Log($"[RouteImporter] {AssetPath} updated from {JsonPath}: {stops.Count} stops, {route.CountProjects()} projects, " +
                      $"{route.CountGains()} mastery points over {route.CountDistinctSkills()} skills, {route.CountRewards(RewardKind.Trait)} traits.");
            return route;
        }

        private static void AddTrait(List<RewardDefinition> list, string traitId, Dictionary<string, TraitFile> traits, string stopId, List<string> errors)
        {
            if (!traits.TryGetValue(traitId, out TraitFile trait))
            {
                errors.Add($"stop '{stopId}': unknown trait '{traitId}'");
                return;
            }
            list.Add(new RewardDefinition { kind = RewardKind.Trait, id = trait.id, label = trait.label, description = trait.description, category = SkillCategory.Relationnel });
        }
    }

    /// <summary>Re-imports the Route asset every time route.json is saved, so content changes need no compilation.</summary>
    public sealed class RouteJsonWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in imported)
            {
                if (path != RouteImporter.JsonPath) continue;
                RouteDefinition route = RouteImporter.Import();
                if (route != null) AssetDatabase.SaveAssetIfDirty(route);
                return;
            }
        }
    }
}
