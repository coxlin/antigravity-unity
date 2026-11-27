using System;
using System.Collections.Generic;
using System.IO;
using Unity.CodeEditor;
using UnityEditor;

namespace Coxlin.Antigravity.Editor
{
    public static class AntigravityDiscovery
    {
        public const string EditorName = "Google Antigravity";
        public const string IdeEditorName = "Antigravity IDE";
        public const string CustomExePathPrefKey = "Antigravity_CustomExePath";

        private const string ExeFileName = "Antigravity.exe";
        private const string IdeExeFileName = "Antigravity IDE.exe";
        private const string CmdFileName = "antigravity.cmd";
        private const string InstallFolderName = "Antigravity";
        private const string IdeInstallFolderName = "Antigravity IDE";

        private static readonly (string FolderName, string ExeName)[] KnownInstallLocations =
        {
            (IdeInstallFolderName, IdeExeFileName),
            (InstallFolderName, ExeFileName)
        };

        private static readonly string[] SupportedExecutableNames =
        {
            IdeExeFileName,
            ExeFileName,
            CmdFileName
        };

        public static string SavedCustomPath
        {
            get { return EditorPrefs.GetString(CustomExePathPrefKey, string.Empty); }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    EditorPrefs.DeleteKey(CustomExePathPrefKey);
                }
                else
                {
                    EditorPrefs.SetString(CustomExePathPrefKey, value.Trim());
                }
            }
        }

        public static bool IsAntigravityPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                string fileName = Path.GetFileName(path);
                foreach (string supportedName in SupportedExecutableNames)
                {
                    if (string.Equals(fileName, supportedName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public static string ResolveExecutablePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch (Exception exception) when (IsPathException(exception))
            {
                return null;
            }

            if (!File.Exists(fullPath))
            {
                return null;
            }

            if (string.Equals(Path.GetExtension(fullPath), ".cmd", StringComparison.OrdinalIgnoreCase))
            {
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    try
                    {
                        // The launcher next to the bin folder avoids spawning a visible console window.
                        DirectoryInfo parent = Directory.GetParent(directory);
                        foreach (string exeName in new[] { IdeExeFileName, ExeFileName })
                        {
                            if (parent != null)
                            {
                                string parentExe = Path.Combine(parent.FullName, exeName);
                                if (File.Exists(parentExe))
                                {
                                    return parentExe;
                                }
                            }

                            string siblingExe = Path.Combine(directory, exeName);
                            if (File.Exists(siblingExe))
                            {
                                return siblingExe;
                            }
                        }
                    }
                    catch (Exception exception) when (IsPathException(exception))
                    {
                        // Fall through and use the existing .cmd path.
                    }
                }
            }

            return fullPath;
        }

        public static CodeEditor.Installation[] GetInstallations()
        {
            var installations = new List<CodeEditor.Installation>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddCandidate(installations, seenPaths, SavedCustomPath);

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
            {
                localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            }

            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                AddKnownLocations(installations, seenPaths, Path.Combine(localAppData, "Programs"));
            }

            AddKnownLocations(installations, seenPaths, Environment.GetEnvironmentVariable("ProgramFiles"));
            AddKnownLocations(installations, seenPaths, Environment.GetEnvironmentVariable("ProgramFiles(x86)"));

            string pathVariable = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrWhiteSpace(pathVariable))
            {
                string[] directories = pathVariable.Split(Path.PathSeparator);
                foreach (string directory in directories)
                {
                    if (string.IsNullOrWhiteSpace(directory))
                    {
                        continue;
                    }

                    try
                    {
                        foreach (string executableName in SupportedExecutableNames)
                        {
                            AddCandidate(installations, seenPaths, Path.Combine(directory, executableName));
                        }
                    }
                    catch (Exception exception) when (IsPathException(exception))
                    {
                        // Ignore malformed PATH entries.
                    }
                }
            }

            return installations.Count > 0
                ? installations.ToArray()
                : Array.Empty<CodeEditor.Installation>();
        }

        public static string FindBestExecutablePath()
        {
            string current = CodeEditor.CurrentEditorInstallation;
            if (IsAntigravityPath(current))
            {
                string resolvedCurrent = ResolveExecutablePath(current);
                if (!string.IsNullOrEmpty(resolvedCurrent))
                {
                    return resolvedCurrent;
                }
            }

            CodeEditor.Installation[] installations = GetInstallations();
            return installations.Length > 0 ? installations[0].Path : null;
        }

        public static string GetInstallationName(string path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                try
                {
                    if (string.Equals(Path.GetFileName(path), IdeExeFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        return IdeEditorName;
                    }
                }
                catch (ArgumentException)
                {
                    // Fall through to default editor name.
                }
            }

            return EditorName;
        }

        private static void AddKnownLocations(
            List<CodeEditor.Installation> installations,
            HashSet<string> seenPaths,
            string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                return;
            }

            foreach ((string folderName, string exeName) in KnownInstallLocations)
            {
                AddCandidate(
                    installations,
                    seenPaths,
                    SafeCombine(baseDirectory, folderName, exeName));
            }
        }

        private static void AddCandidate(
            List<CodeEditor.Installation> installations,
            HashSet<string> seenPaths,
            string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }

            string resolvedPath = ResolveExecutablePath(candidate);
            if (string.IsNullOrEmpty(resolvedPath))
            {
                return;
            }

            if (!seenPaths.Add(resolvedPath))
            {
                return;
            }

            installations.Add(new CodeEditor.Installation
            {
                Name = GetInstallationName(resolvedPath),
                Path = resolvedPath
            });
        }

        private static string SafeCombine(params string[] parts)
        {
            try
            {
                return Path.Combine(parts);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static bool IsPathException(Exception exception)
        {
            return exception is ArgumentException
                   || exception is NotSupportedException
                   || exception is PathTooLongException
                   || exception is IOException
                   || exception is System.Security.SecurityException;
        }
    }
}
