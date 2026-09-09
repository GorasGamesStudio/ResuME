using System.IO;
using ResuME.Data;
using ResuME.Player;
using ResuME.UI;
using ResuME.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ResuME.Editor
{
    /// <summary>
    /// Builds everything the greybox needs from code: the square sprite, the Route asset and
    /// the Greybox scene. Safe to run again: assets are overwritten, the scene is recreated.
    /// Also callable in batch mode: <c>-executeMethod ResuME.Editor.GreyboxSetup.BuildAll</c>.
    /// </summary>
    public static class GreyboxSetup
    {
        private const string Root = "Assets/_ResuME";
        private const string SpritePath = Root + "/Art/Greybox/square.png";
        private const string RoutePath = Root + "/Data/Route.asset";
        private const string ScenePath = Root + "/Scenes/Greybox.unity";
        private const string InputActionsPath = "Assets/Settings/InputSystem_Actions.inputactions";

        [MenuItem("ResuME/Greybox/Rebuild everything")]
        public static void BuildAll()
        {
            Sprite square = CreateSquareSprite();
            RouteDefinition route = CreateRouteAsset();
            CreateScene(route, square);
            RegisterScene();
            AssetDatabase.SaveAssets();
            Debug.Log($"[ResuME] Greybox rebuilt: {route.stops.Count} stops, {route.TotalScreens:F2} screens, " +
                      $"{route.CountRewards(RewardKind.Skill)} skills, {route.CountRewards(RewardKind.Trait)} traits.");
        }

        [MenuItem("ResuME/Greybox/Regenerate Route asset only")]
        public static void RegenerateRoute()
        {
            CreateRouteAsset();
            AssetDatabase.SaveAssets();
        }

        private static Sprite CreateSquareSprite()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SpritePath));
            if (!File.Exists(SpritePath))
            {
                const int size = 32;
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Color[] pixels = new Color[size * size];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                texture.SetPixels(pixels);
                texture.Apply();
                File.WriteAllBytes(SpritePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceUpdate);
            }

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }

        private static RouteDefinition CreateRouteAsset()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RoutePath));
            RouteDefinition route = AssetDatabase.LoadAssetAtPath<RouteDefinition>(RoutePath);
            if (route == null)
            {
                route = ScriptableObject.CreateInstance<RouteDefinition>();
                AssetDatabase.CreateAsset(route, RoutePath);
            }
            route.stops = RouteContent.Build();
            EditorUtility.SetDirty(route);
            return route;
        }

        private static void CreateScene(RouteDefinition route, Sprite square)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);

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

            // Event system for the tooltips
            GameObject eventsGo = new GameObject("EventSystem");
            eventsGo.AddComponent<EventSystem>();
            InputSystemUIInputModule module = eventsGo.AddComponent<InputSystemUIInputModule>();
            if (actions != null) module.actionsAsset = actions;

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void RegisterScene()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
