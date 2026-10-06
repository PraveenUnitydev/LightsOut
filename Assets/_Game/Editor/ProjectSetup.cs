using System.IO;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LightsOut.EditorTools
{
    /// <summary>
    /// Builds the generated parts of the project: layers, materials, Player prefab, Game scene and player settings.
    /// Re-running overwrites Game.unity and Player.prefab, so make hand edits elsewhere or re-apply them after.
    /// Headless: Unity.exe -batchmode -quit -nographics -projectPath . -executeMethod LightsOut.EditorTools.ProjectSetup.Run
    /// </summary>
    public static class ProjectSetup
    {
        const string Root = "Assets/_Game";
        const string ScenePath = Root + "/Scenes/Game.unity";
        const string PrefabPath = Root + "/Prefabs/Player.prefab";

        [MenuItem("Lights Out/Rebuild Generated Scene and Prefab")]
        public static void Run()
        {
            Directory.CreateDirectory(Root + "/Resources");
            Directory.CreateDirectory(Root + "/Prefabs");
            Directory.CreateDirectory(Root + "/Scenes");

            SetLayers();
            CreateMaterial(Root + "/Resources/SpriteUnlit.mat", "Universal Render Pipeline/2D/Sprite-Unlit-Default");
            CreateMaterial(Root + "/Resources/Darkness.mat", "LightsOut/Darkness");
            var prefab = CreatePlayerPrefab();
            CreateScene(prefab);
            ConfigurePlayerSettings();

            if (File.Exists("Assets/Scenes/SampleScene.unity")) AssetDatabase.DeleteAsset("Assets/Scenes/SampleScene.unity");
            if (AssetDatabase.IsValidFolder("Assets/Scenes") && Directory.GetFileSystemEntries("Assets/Scenes").Length == 0)
                AssetDatabase.DeleteAsset("Assets/Scenes");

            AssetDatabase.SaveAssets();
            Debug.Log("[LightsOut] Project setup complete.");
        }

        static void SetLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            layers.GetArrayElementAtIndex(GameMap.WallLayer).stringValue = "Walls";
            layers.GetArrayElementAtIndex(GameMap.PlayerLayer).stringValue = "Players";
            layers.GetArrayElementAtIndex(GameMap.FurnitureLayer).stringValue = "Furniture";
            tagManager.ApplyModifiedProperties();
        }

        static void CreateMaterial(string path, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("[LightsOut] Shader not found: " + shaderName);
                return;
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
                EditorUtility.SetDirty(mat);
            }
        }

        static GameObject CreatePlayerPrefab()
        {
            var go = new GameObject("Player") { layer = GameMap.PlayerLayer };
            go.AddComponent<NetworkObject>();

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.32f;

            var nt = go.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            nt.SyncPositionZ = false;
            nt.SyncRotAngleX = nt.SyncRotAngleY = nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            nt.Interpolate = true;

            go.AddComponent<PlayerAvatar>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);

            // NetworkObject only generates its GlobalObjectIdHash in OnValidate on the saved asset, which batch mode
            // doesn't trigger. Without the hash, clients can't match the spawned player to the prefab.
            var netObj = prefab.GetComponent<NetworkObject>();
            typeof(NetworkObject).GetMethod("OnValidate",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                ?.Invoke(netObj, null);
            EditorUtility.SetDirty(prefab);
            PrefabUtility.SavePrefabAsset(prefab);
            return prefab;
        }

        static void CreateScene(GameObject playerPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0, 0, -10);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Background;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            camGo.AddComponent<UniversalAdditionalCameraData>();
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CameraFollow>();

            var nmGo = new GameObject("NetworkManager");
            var nm = nmGo.AddComponent<NetworkManager>();
            var transport = nmGo.AddComponent<UnityTransport>();
            nm.NetworkConfig ??= new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.PlayerPrefab = playerPrefab;
            nm.NetworkConfig.ConnectionApproval = true;
            nm.NetworkConfig.TickRate = 30;
            nm.NetworkConfig.EnableSceneManagement = false;

            new GameObject("Game").AddComponent<GameRoot>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "PraveenUnitydev";
            PlayerSettings.productName = "Lights Out";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.runInBackground = true;

            // Phones: landscape only.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // Android: sideloaded APK, 64-bit (newer phones have no 32-bit support).
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.praveenunitydev.lightsout");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.bundleVersionCode = 1;

            // Windows test builds: small resizable window, so several copies fit on one screen.
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.forceSingleInstance = false;
        }
    }
}
