#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Margin.EditorTools
{
    /// <summary>
    /// Gets the project ready the first time it opens: main scene in Build Settings,
    /// iOS settings (portrait orientation, iOS 16+ for Screen Time, bundle identifier).
    /// Also available from the menu Margin › Set up project.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string DoneKey = "Margin.ProjectSetup.v1";

        static ProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!SessionState.GetBool(DoneKey, false) && !File.Exists(ScenePath)) Run();
                SessionState.SetBool(DoneKey, true);
            };
        }

        [MenuItem("Margin/Set up project")]
        public static void Run()
        {
            // Empty scene: MarginApp creates itself on start (RuntimeInitializeOnLoadMethod).
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                var cam = Object.FindAnyObjectByType<Camera>();
                if (cam != null)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color32(0xF4, 0xEF, 0xE6, 0xFF); // --paper
                }
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.productName = "Margin";
            PlayerSettings.companyName = "Margin";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.margin.reading");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.statusBarHidden = false;
            PlayerSettings.SplashScreen.backgroundColor = new Color32(0xF4, 0xEF, 0xE6, 0xFF);

            AssetDatabase.SaveAssets();
            Debug.Log("[Margin] Project set up. Press Play or use File › Build Settings › iOS.");
        }
    }
}
#endif
