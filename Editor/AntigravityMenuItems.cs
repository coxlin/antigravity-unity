using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Coxlin.Antigravity.Editor
{
    /// <summary>
    /// Editor menu items for opening assets and folders in Google Antigravity,
    /// regenerating project files, and scaffolding C# workspace config files.
    /// </summary>
    public static class AntigravityMenuItems
    {
        private const string OpenInAntigravityMenu = "Assets/Open in Antigravity";
        private const string NotFoundDialogTitle = "Google Antigravity Not Found";
        private const string NotFoundDialogMessage =
            "Could not locate Antigravity.exe. Please configure Google Antigravity in Edit > Preferences > External Tools.";

        [MenuItem(OpenInAntigravityMenu, false, 1000)]
        public static void OpenSelectedInAntigravity()
        {
            string exe = AntigravityDiscovery.FindBestExecutablePath();
            if (string.IsNullOrEmpty(exe))
            {
                EditorUtility.DisplayDialog(NotFoundDialogTitle, NotFoundDialogMessage, "OK");
                return;
            }

            string projectRoot = AntigravityLauncher.GetProjectRoot();
            string fullPath = ResolveSelectedAssetFullPath(projectRoot);

            if (!string.IsNullOrEmpty(fullPath))
            {
                if (Directory.Exists(fullPath))
                {
                    AntigravityLauncher.OpenFolder(exe, fullPath);
                    return;
                }

                if (File.Exists(fullPath))
                {
                    AntigravityLauncher.OpenFileInWorkspace(exe, projectRoot, fullPath);
                    return;
                }
            }

            // Nothing valid is selected (empty space / fallback): open the Assets folder.
            AntigravityLauncher.OpenFolder(exe, Application.dataPath);
        }

        [MenuItem(OpenInAntigravityMenu, true)]
        public static bool ValidateOpenSelectedInAntigravity()
        {
            if (Selection.activeObject == null)
            {
                return false;
            }

            string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(assetPath) || IsBuiltInVirtualAssetPath(assetPath))
            {
                return false;
            }

            string fullPath = ResolveSelectedAssetFullPath(AntigravityLauncher.GetProjectRoot());
            return !string.IsNullOrEmpty(fullPath) && (File.Exists(fullPath) || Directory.Exists(fullPath));
        }

        [MenuItem("Window/Antigravity/Open Project Folder", false, 100)]
        public static void OpenProjectFolder()
        {
            string exe = AntigravityDiscovery.FindBestExecutablePath();
            if (string.IsNullOrEmpty(exe))
            {
                EditorUtility.DisplayDialog(NotFoundDialogTitle, NotFoundDialogMessage, "OK");
                return;
            }

            AntigravityLauncher.OpenFolder(exe, AntigravityLauncher.GetProjectRoot());
        }

        [MenuItem("Window/Antigravity/Open Assets Folder", false, 101)]
        public static void OpenAssetsFolder()
        {
            string exe = AntigravityDiscovery.FindBestExecutablePath();
            if (string.IsNullOrEmpty(exe))
            {
                EditorUtility.DisplayDialog(NotFoundDialogTitle, NotFoundDialogMessage, "OK");
                return;
            }

            AntigravityLauncher.OpenFolder(exe, Application.dataPath);
        }

        [MenuItem("Window/Antigravity/Regenerate Project Files", false, 200)]
        public static void RegenerateProjectFiles()
        {
            AntigravityScriptEditor.RegenerateProjectFiles();
        }

        [MenuItem("Window/Antigravity/Generate C# Config Files", false, 201)]
        public static void GenerateConfigFiles()
        {
            string projectRoot = AntigravityLauncher.GetProjectRoot();
            string rootName = Path.GetFileName(
                projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string solutionFileName = $"{rootName}.sln";

            ConfigScaffolder.GenerateConfigFiles(projectRoot, solutionFileName, true);
        }

        /// <summary>
        /// Resolves the full on-disk path of the currently selected asset, or null when the
        /// selection is invalid, virtual (built-in resources), or outside the project root.
        /// </summary>
        private static string ResolveSelectedAssetFullPath(string projectRoot)
        {
            if (Selection.activeObject == null || string.IsNullOrEmpty(projectRoot))
            {
                return null;
            }

            string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(assetPath) || IsBuiltInVirtualAssetPath(assetPath))
            {
                return null;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
                if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                {
                    // Support Embedded / Local UPM assets via PackageManager.PackageInfo.
                    var pkgInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);
                    if (pkgInfo != null && !string.IsNullOrEmpty(pkgInfo.resolvedPath) && !string.IsNullOrEmpty(pkgInfo.assetPath))
                    {
                        string prefix = pkgInfo.assetPath.Replace('\\', '/').TrimEnd('/') + "/";
                        string normAsset = assetPath.Replace('\\', '/');
                        if (normAsset.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            string rel = normAsset.Substring(prefix.Length).Replace('/', Path.DirectorySeparatorChar);
                            fullPath = Path.GetFullPath(Path.Combine(pkgInfo.resolvedPath, rel));
                            return (File.Exists(fullPath) || Directory.Exists(fullPath)) ? fullPath : null;
                        }
                    }
                }

                // Guard against paths that escape the project root (e.g. via "..").
                string normalizedRoot = NormalizeForComparison(projectRoot);
                string normalizedFull = NormalizeForComparison(fullPath);
                if (!normalizedFull.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }
            catch (Exception)
            {
                return null;
            }

            return fullPath;
        }

        private static bool IsBuiltInVirtualAssetPath(string assetPath)
        {
            string normalized = assetPath.Replace('\\', '/');
            return normalized.IndexOf("unity_builtin_extra", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf("unity default resources", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string NormalizeForComparison(string path)
        {
            string full = Path.GetFullPath(path);
            return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
        }
    }
}
