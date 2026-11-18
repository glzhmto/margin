#if UNITY_EDITOR && UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace Margin.EditorTools
{
    /// <summary>
    /// Tweaks to the generated Xcode project. App blocking is a demo (no Family Controls);
    /// if real Screen Time is used in the future, the frameworks and entitlement would be added here.
    /// </summary>
    public static class iOSPostBuild
    {
        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            var plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetString("CFBundleDisplayName", "Margin");
            plist.root.SetString("CFBundleDevelopmentRegion", "en");
            // "Export my data" writes a CSV to the app's Documents folder; these show it in the Files app.
            plist.root.SetBoolean("UIFileSharingEnabled", true);
            plist.root.SetBoolean("LSSupportsOpeningDocumentsInPlace", true);
            plist.WriteToFile(plistPath);
        }
    }
}
#endif
