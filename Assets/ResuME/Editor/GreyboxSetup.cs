using System.Collections.Generic;
using System.IO;
using System.Linq;
using GGS.Resume.Data;
using GGS.Resume.DevTools;
using GGS.Resume.Player;
using GGS.Resume.UI;
using GGS.Resume.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace GGS.Resume.Editor
{
    /// <summary>
    /// Builds everything the greybox needs from code, inside the GGS Core project layout: the
    /// square sprite, the Route asset and the GA_GreyboxScene scene. Safe to run again: assets are overwritten, the scene is recreated.
    /// Also callable in batch mode: <c>-executeMethod GGS.Resume.Editor.GreyboxSetup.BuildAll</c>.
    /// </summary>
    public static class GreyboxSetup
    {
        #region Fields

        private const string ROOT = "Assets/ResuME";
        private const string SPRITE_PATH = ROOT + "/Graphics/Sprites/Greybox/square.png";
        private const string SCENE_PATH = ROOT + "/Scenes/GA_GreyboxScene.unity";
        private const string INPUT_ACTIONS_PATH = "Assets/Settings/InputSystem_Actions.inputactions";
        private const int SPRITE_SIZE = 32;

        #endregion

        #region Public Methods

        /// <summary>
        /// Rebuilds sprite, route asset and scene, then registers the scene first in the Build Settings.
        /// </summary>
        [MenuItem("ResuME/Greybox/Rebuild everything")]
        public static void BuildAll()
        {
            Sprite square = CreateSquareSprite();
            RouteDefinition route = RouteImporter.Import();
            if (route == null)
            {
                Debug.LogError("[ResuME] Greybox not rebuilt: fix route.json first.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            CreateScene(route, square);
            RegisterScene();
            AssetDatabase.SaveAssets();
            Debug.Log($"[ResuME] Greybox rebuilt: {route.stops.Count} stops, {route.TotalScreens:F2} screens, " +
                      $"{route.CountProjects()} projects, {route.CountGains()} mastery points over {route.CountDistinctSkills()} skills, {route.CountRewards(RewardKind.Trait)} traits.");
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Creates the white square sprite once and enforces its import settings (point filter, 32 pixels per unit).
        /// </summary>
        /// <returns>The imported sprite.</returns>
        private static Sprite CreateSquareSprite()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SPRITE_PATH));
            if (!File.Exists(SPRITE_PATH))
            {
                Texture2D texture = new Texture2D(SPRITE_SIZE, SPRITE_SIZE, TextureFormat.RGBA32, false);
                Color[] pixels = new Color[SPRITE_SIZE * SPRITE_SIZE];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                texture.SetPixels(pixels);
                texture.Apply();
                File.WriteAllBytes(SPRITE_PATH, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(SPRITE_PATH, ImportAssetOptions.ForceUpdate);
            }

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(SPRITE_PATH);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = SPRITE_SIZE;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_PATH);
        }

        /// <summary>
        /// Recreates the greybox scene from scratch: camera, global light, player, world, HUD and event system.
        /// </summary>
        /// <param name="route">Route asset the world reads.</param>
        /// <param name="square">White square used by every block.</param>
        private static void CreateScene(RouteDefinition route, Sprite square)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SCENE_PATH));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(INPUT_ACTIONS_PATH);

            // Camera
            GameObject cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = GreyboxPalette.Background;
            camera.GetUniversalAdditionalCameraData();
            cameraGo.AddComponent<AudioListener>();
            FollowCamera follow = cameraGo.AddComponent<FollowCamera>();

            // Global 2D light so lit materials, if any, are never black
            GameObject lightGo = new GameObject("Global Light 2D");
            Light2D light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;

            // Player
            GameObject playerGo = new GameObject("Player");
            GameObject visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(playerGo.transform, false);
            visualGo.transform.localScale = new Vector3(0.8f, 1.6f, 1f);
            SpriteRenderer playerRenderer = visualGo.AddComponent<SpriteRenderer>();
            playerRenderer.sprite = square;
            playerRenderer.color = GreyboxPalette.Player;
            playerRenderer.sortingOrder = 5;

            // World
            GameObject worldGo = new GameObject("World");
            WorldBuilder world = worldGo.AddComponent<WorldBuilder>();
            world.Configure(route, square, font, playerGo.transform);
            follow.Configure(world);

            PlayerMover mover = playerGo.AddComponent<PlayerMover>();
            mover.Configure(actions, world, visualGo.transform);
            PlayerProgress progress = playerGo.AddComponent<PlayerProgress>();
            progress.Configure(world, visualGo.transform, playerRenderer, square);

            // HUD
            GameObject hudGo = new GameObject("HUD", typeof(RectTransform));
            CharacterSheetHud hud = hudGo.AddComponent<CharacterSheetHud>();
            hud.Configure(world, progress, font);
            GreyboxDebugOverlay overlay = hudGo.AddComponent<GreyboxDebugOverlay>();
            overlay.Configure(world, mover);
            MiniMapHud miniMap = hudGo.AddComponent<MiniMapHud>();
            miniMap.Configure(world, font);

            // Event system for the tooltips
            GameObject eventsGo = new GameObject("EventSystem");
            eventsGo.AddComponent<EventSystem>();
            InputSystemUIInputModule module = eventsGo.AddComponent<InputSystemUIInputModule>();
            if (actions != null) module.actionsAsset = actions;

            EditorSceneManager.SaveScene(scene, SCENE_PATH);
        }

        /// <summary>
        /// Keeps the GGS Core base scenes and puts the greybox first so a build starts on it, until the
        /// Bootstrap → Loading → Gameplay flow is wired. Entries whose file no longer exists are dropped.
        /// </summary>
        private static void RegisterScene()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
                .Where(s => File.Exists(s.path) && s.path != SCENE_PATH)
                .ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(SCENE_PATH, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        #endregion
    }
}
