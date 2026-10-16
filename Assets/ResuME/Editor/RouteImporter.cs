using System;
using System.Collections.Generic;
using System.IO;
using GGS.Resume.Data;
using UnityEditor;
using UnityEngine;

namespace GGS.Resume.Editor
{
    /// <summary>
    /// Turns <c>Datas/route.json</c> (hand-edited, no compilation needed) into the Route asset the
    /// game reads. Runs from the menu and automatically whenever the JSON file is saved.
    /// Unknown skill or trait ids abort the import with an explicit error.
    /// </summary>
    public static class RouteImporter
    {
        #region Fields

        public const string JSON_PATH = "Assets/ResuME/Datas/route.json";
        public const string ASSET_PATH = "Assets/ResuME/Datas/Route.asset";

        #endregion

        #region Data Structures

        /// <summary>Root of route.json: sizes, pacing, travel, contact, places, catalogues and stops.</summary>

        [Serializable] private class RouteFile
        {
            public float screenWidthUnits = 20f;
            public float buildingHeightUnits = 6f;
            public float roadHeightUnits = 2f;
            public float secondsPerScreen = 3f;
            public float gapScreens = 0.5f;
            public int masteryScaleMax = 5;
            public ContactFile contact = new ContactFile();
            public TravelFile travel = new TravelFile();
            public float verticalSpanScreens = 1.2f;
            public List<PlaceFile> places = new List<PlaceFile>();
            public List<StringFile> strings = new List<StringFile>();
            public List<SkillFile> skills = new List<SkillFile>();
            public List<TraitFile> traits = new List<TraitFile>();
            public List<StopFile> stops = new List<StopFile>();
        }

        /// <summary>A text in both languages as written in the JSON.</summary>
        [Serializable] private class LocalizedTextFile { public string fr; public string en; }
        /// <summary>A word of the interface in both languages.</summary>
        [Serializable] private class StringFile { public string id; public string fr; public string en; }
        /// <summary>A place of the map of France, with its ground tint.</summary>

        [Serializable] private class PlaceFile { public string id; public LocalizedTextFile label; public float x; public float y; public string groundColor; }
        /// <summary>The contact card: name, e-mail, LinkedIn, CV link, closing line.</summary>
        [Serializable] private class ContactFile { public string name; public string email; public string linkedin; public string cvUrl; public LocalizedTextFile closingLine; }
        /// <summary>Vehicle rides: extra road and speed factors.</summary>
        [Serializable] private class TravelFile { public float rideScreens = 2f; public float carSpeedFactor = 2.5f; public float motorbikeSpeedFactor = 3.5f; }
        /// <summary>A skill of the catalogue, with its short label and category.</summary>
        [Serializable] private class SkillFile { public string id; public LocalizedTextFile label; public LocalizedTextFile @short; public string category; public LocalizedTextFile description; }
        /// <summary>A trait of the catalogue.</summary>
        [Serializable] private class TraitFile { public string id; public LocalizedTextFile label; public LocalizedTextFile description; }
        /// <summary>A class change or a passive granted by a stop.</summary>
        [Serializable] private class RewardFile { public string id; public LocalizedTextFile title; public LocalizedTextFile description; }
        /// <summary>A project of a stop: the skills it used and its platforms.</summary>
        [Serializable] private class ProjectFile { public string id; public LocalizedTextFile title; public LocalizedTextFile description; public List<string> skills = new List<string>(); public List<string> platforms = new List<string>(); }

        /// <summary>A stop of the timeline with its rewards and projects.</summary>

        [Serializable] private class StopFile
        {
            public string id;
            public LocalizedTextFile title;
            public LocalizedTextFile years;
            public string place;
            public int level;
            public string kind;
            public float widthScreens = 1f;
            public float gapAfterScreens = -1f;
            public string travelAfter;
            public float rideScreens = -1f;
            public bool founder;
            public List<string> traitsBefore = new List<string>();
            public List<string> traitsAfter = new List<string>();
            public RewardFile classChange;
            public RewardFile passive;
            public List<ProjectFile> projects = new List<ProjectFile>();
            public string notes;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Menu entry: imports the JSON and saves the asset.
        /// </summary>
        [MenuItem("ResuME/Route/Import route.json")]
        public static void ImportFromMenu()
        {
            RouteDefinition route = Import();
            if (route != null) AssetDatabase.SaveAssets();
        }

        /// <summary>Imports the JSON into the Route asset (created if missing). Returns null on error.</summary>
        public static RouteDefinition Import()
        {
            if (!File.Exists(JSON_PATH))
            {
                Debug.LogError($"[RouteImporter] {JSON_PATH} not found.");
                return null;
            }

            RouteFile file;
            try
            {
                file = JsonUtility.FromJson<RouteFile>(File.ReadAllText(JSON_PATH));
            }
            catch (Exception e)
            {
                Debug.LogError($"[RouteImporter] {JSON_PATH} is not valid JSON: {e.Message}");
                return null;
            }

            List<string> errors = new List<string>();
            _missingEnglish = 0;
            Dictionary<string, TraitFile> traits = new Dictionary<string, TraitFile>();
            foreach (TraitFile trait in file.traits)
            {
                if (string.IsNullOrEmpty(trait.id)) errors.Add("a trait has no id");
                else if (!traits.TryAdd(trait.id, trait)) errors.Add($"duplicate trait id '{trait.id}'");
            }

            List<SkillDefinition> skills = new List<SkillDefinition>();
            HashSet<string> skillIds = new HashSet<string>();
            foreach (SkillFile skill in file.skills)
            {
                if (string.IsNullOrEmpty(skill.id)) { errors.Add("a skill has no id"); continue; }
                if (!skillIds.Add(skill.id)) { errors.Add($"duplicate skill id '{skill.id}'"); continue; }
                if (!Enum.TryParse(skill.category, true, out SkillCategory category))
                {
                    errors.Add($"skill '{skill.id}': unknown category '{skill.category}'");
                    category = SkillCategory.None;
                }
                LocalizedText shortLabel = skill.@short != null && !string.IsNullOrEmpty(skill.@short.fr) ? Text(skill.@short, $"skill '{skill.id}' short", errors) : Text(skill.label, $"skill '{skill.id}' label", errors);
                skills.Add(new SkillDefinition { id = skill.id, label = Text(skill.label, $"skill '{skill.id}' label", errors), shortLabel = shortLabel, category = category, description = Text(skill.description, $"skill '{skill.id}' description", errors) });
            }

            // Places of the map: ids must be unique and every stop must point to one of them.
            List<PlaceDefinition> places = new List<PlaceDefinition>();
            HashSet<string> placeIds = new HashSet<string>();
            foreach (PlaceFile place in file.places)
            {
                if (string.IsNullOrEmpty(place.id)) { errors.Add("a place has no id"); continue; }
                if (!placeIds.Add(place.id)) { errors.Add($"duplicate place id '{place.id}'"); continue; }
                places.Add(new PlaceDefinition { id = place.id, label = Text(place.label, $"place '{place.id}' label", errors), x = Mathf.Clamp01(place.x), y = Mathf.Clamp01(place.y), groundColor = place.groundColor });
            }

            List<StopDefinition> stops = new List<StopDefinition>();
            HashSet<string> stopIds = new HashSet<string>();
            foreach (StopFile s in file.stops)
            {
                if (string.IsNullOrEmpty(s.id)) { errors.Add("a stop has no id"); continue; }
                if (!stopIds.Add(s.id)) errors.Add($"duplicate stop id '{s.id}'");
                if (!Enum.TryParse(s.kind, true, out StopKind kind))
                {
                    errors.Add($"stop '{s.id}': unknown kind '{s.kind}'");
                    kind = StopKind.Traverse;
                }

                if (!string.IsNullOrEmpty(s.place) && !placeIds.Contains(s.place)) errors.Add($"stop '{s.id}': unknown place '{s.place}'");

                // Travel mode to the next stop: on foot unless the JSON names a vehicle.
                TravelMode travel = TravelMode.Walk;
                if (!string.IsNullOrEmpty(s.travelAfter) && !Enum.TryParse(s.travelAfter, true, out travel))
                {
                    errors.Add($"stop '{s.id}': unknown travel mode '{s.travelAfter}'");
                    travel = TravelMode.Walk;
                }

                StopDefinition stop = new StopDefinition
                {
                    id = s.id, title = Text(s.title, $"stop '{s.id}' title", errors), years = Text(s.years, $"stop '{s.id}' years", errors), place = s.place, level = s.level, kind = kind,
                    widthScreens = s.widthScreens <= 0f ? 1f : s.widthScreens, gapAfterScreens = s.gapAfterScreens,
                    travelAfter = travel, rideScreens = s.rideScreens, founder = s.founder, notes = s.notes
                };

                foreach (string traitId in s.traitsBefore) AddTrait(stop.traitsBefore, traitId, traits, s.id, errors);
                foreach (string traitId in s.traitsAfter) AddTrait(stop.traitsAfter, traitId, traits, s.id, errors);

                if (s.classChange != null && !string.IsNullOrEmpty(s.classChange.id))
                    stop.rewards.Add(new RewardDefinition { kind = RewardKind.ClassChange, id = s.classChange.id, label = Text(s.classChange.title, $"class '{s.classChange.id}' title", errors), description = Text(s.classChange.description, $"class '{s.classChange.id}' description", errors) });
                if (s.passive != null && !string.IsNullOrEmpty(s.passive.id))
                    stop.rewards.Add(new RewardDefinition { kind = RewardKind.Passive, id = s.passive.id, label = Text(s.passive.title, $"passive '{s.passive.id}' title", errors), description = Text(s.passive.description, $"passive '{s.passive.id}' description", errors) });

                foreach (ProjectFile p in s.projects)
                {
                    if (string.IsNullOrEmpty(p.id)) { errors.Add($"stop '{s.id}': a project has no id"); continue; }
                    ProjectDefinition project = new ProjectDefinition { id = p.id, title = Text(p.title, $"project '{p.id}' title", errors), description = Text(p.description, $"project '{p.id}' description", errors), platforms = new List<string>(p.platforms) };
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
                Debug.LogError($"[RouteImporter] {errors.Count} error(s) in {JSON_PATH}, asset not updated:\n- " + string.Join("\n- ", errors));
                return null;
            }

            RouteDefinition route = AssetDatabase.LoadAssetAtPath<RouteDefinition>(ASSET_PATH);
            if (route == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ASSET_PATH));
                route = ScriptableObject.CreateInstance<RouteDefinition>();
                AssetDatabase.CreateAsset(route, ASSET_PATH);
            }

            route.screenWidthUnits = file.screenWidthUnits;
            route.buildingHeightUnits = file.buildingHeightUnits;
            route.roadHeightUnits = file.roadHeightUnits;
            route.secondsPerScreen = file.secondsPerScreen;
            route.gapScreens = file.gapScreens;
            route.rideScreens = file.travel != null ? file.travel.rideScreens : 2f;
            route.carSpeedFactor = file.travel != null ? file.travel.carSpeedFactor : 2.5f;
            route.motorbikeSpeedFactor = file.travel != null ? file.travel.motorbikeSpeedFactor : 3.5f;
            route.masteryScaleMax = file.masteryScaleMax;
            route.verticalSpanScreens = file.verticalSpanScreens;
            route.places = places;
            route.contactName = file.contact != null ? file.contact.name : "";
            route.contactEmail = file.contact != null ? file.contact.email : "";
            route.contactLinkedIn = file.contact != null ? file.contact.linkedin : "";
            route.contactCvUrl = file.contact != null ? file.contact.cvUrl : "";
            route.closingLine = file.contact != null ? Text(file.contact.closingLine, "contact closingLine", errors) : new LocalizedText();
            route.strings = new List<UiStringDefinition>();
            HashSet<string> stringIds = new HashSet<string>();
            foreach (StringFile entry in file.strings)
            {
                if (string.IsNullOrEmpty(entry.id)) { errors.Add("a UI string has no id"); continue; }
                if (!stringIds.Add(entry.id)) { errors.Add($"duplicate UI string id '{entry.id}'"); continue; }
                if (string.IsNullOrEmpty(entry.fr)) errors.Add($"UI string '{entry.id}': French text missing");
                if (string.IsNullOrEmpty(entry.en)) _missingEnglish++;
                route.strings.Add(new UiStringDefinition { id = entry.id, fr = entry.fr, en = entry.en });
            }
            if (errors.Count > 0)
            {
                Debug.LogError($"[RouteImporter] {errors.Count} error(s) in the UI strings of {JSON_PATH}, asset not updated:\n- " + string.Join("\n- ", errors));
                return null;
            }
            if (_missingEnglish > 0) Debug.LogWarning($"[RouteImporter] {_missingEnglish} text(s) without English: French will be shown instead.");
            route.skills = skills;
            route.stops = stops;
            EditorUtility.SetDirty(route);

            Debug.Log($"[RouteImporter] {ASSET_PATH} updated from {JSON_PATH}: {stops.Count} stops, {route.CountProjects()} projects, " +
                      $"{route.CountGains()} mastery points over {route.CountDistinctSkills()} skills, {route.CountRewards(RewardKind.Trait)} traits.");
            return route;
        }

        #endregion

        #region Private Methods

        /// <summary>Texts whose English is empty, counted during an import.</summary>
        private static int _missingEnglish;

        /// <summary>
        /// Converts a bilingual JSON text to the asset form: French required, English optional (counted when missing).
        /// </summary>
        /// <param name="text">JSON text, possibly null.</param>
        /// <param name="where">Name of the field for the error message.</param>
        /// <param name="errors">Error list of the import.</param>
        /// <returns>The localized text, never null.</returns>
        private static LocalizedText Text(LocalizedTextFile text, string where, List<string> errors)
        {
            if (text == null || string.IsNullOrEmpty(text.fr))
            {
                errors.Add($"{where}: French text missing (expected an object {{\"fr\": ..., \"en\": ...}})");
                return new LocalizedText { fr = "", en = "" };
            }
            if (string.IsNullOrEmpty(text.en)) _missingEnglish++;
            return new LocalizedText { fr = text.fr, en = text.en ?? "" };
        }

        /// <summary>
        /// Resolves a trait id against the catalogue and appends it as a reward, or records an error.
        /// </summary>
        private static void AddTrait(List<RewardDefinition> list, string traitId, Dictionary<string, TraitFile> traits, string stopId, List<string> errors)
        {
            if (!traits.TryGetValue(traitId, out TraitFile trait))
            {
                errors.Add($"stop '{stopId}': unknown trait '{traitId}'");
                return;
            }
            list.Add(new RewardDefinition { kind = RewardKind.Trait, id = trait.id, label = Text(trait.label, $"trait '{trait.id}' label", errors), description = Text(trait.description, $"trait '{trait.id}' description", errors), category = SkillCategory.Relationnel });
        }

        #endregion
    }

    /// <summary>Re-imports the Route asset every time route.json is saved, so content changes need no compilation.</summary>
    public sealed class RouteJsonWatcher : AssetPostprocessor
    {
        /// <summary>
        /// Re-imports the route when route.json is among the imported assets.
        /// </summary>
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in imported)
            {
                if (path != RouteImporter.JSON_PATH) continue;
                RouteDefinition route = RouteImporter.Import();
                if (route != null) AssetDatabase.SaveAssetIfDirty(route);
                return;
            }
        }
    }
}
