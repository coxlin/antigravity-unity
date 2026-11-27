using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Coxlin.Antigravity.Editor
{
    public static class AntigravityLauncher
    {
        public const string CustomArgumentsPrefKey = "Antigravity_CustomArguments";
        public const string DefaultFileArguments = "\"$(ProjectPath)\" -g \"$(File)\":$(Line):$(Column)";

        public static readonly string[] SupportedExtensions =
        {
            ".cs",
            ".shader",
            ".compute",
            ".hlsl",
            ".cginc",
            ".uss",
            ".uxml",
            ".json",
            ".asmdef",
            ".asmref",
            ".md",
            ".txt"
        };

        public static string CustomArguments
        {
            get { return EditorPrefs.GetString(CustomArgumentsPrefKey, DefaultFileArguments); }
            set { EditorPrefs.SetString(CustomArgumentsPrefKey, value ?? string.Empty); }
        }

        public static void ResetCustomArguments()
        {
            CustomArguments = DefaultFileArguments;
        }

        public static bool IsSupportedFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            string extension;
            try
            {
                extension = Path.GetExtension(filePath);
            }
            catch (ArgumentException)
            {
                return false;
            }

            foreach (string supportedExtension in SupportedExtensions)
            {
                if (string.Equals(extension, supportedExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
        }

        public static bool OpenProject(string executablePath, string filePath, int line, int column)
        {
            string resolvedExecutable = ResolveExecutable(executablePath);
            if (string.IsNullOrEmpty(resolvedExecutable))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return OpenFolder(resolvedExecutable, GetProjectRoot());
            }

            string fullFilePath;
            try
            {
                string rawRoot = GetProjectRoot();
                string candidate = !Path.IsPathRooted(filePath) && !string.IsNullOrEmpty(rawRoot)
                    ? Path.Combine(rawRoot, filePath)
                    : filePath;
                fullFilePath = Path.GetFullPath(candidate);
            }
            catch (Exception exception) when (IsPathException(exception))
            {
                UnityEngine.Debug.LogError(
                    $"[Antigravity] Invalid file path '{filePath}': {exception.Message}");
                return false;
            }

            line = line < 1 ? 1 : line;
            column = column < 1 ? 1 : column;

            string argumentsTemplate = CustomArguments;
            if (string.IsNullOrWhiteSpace(argumentsTemplate))
            {
                argumentsTemplate = DefaultFileArguments;
            }

            string projectRoot = ToQuotedFolderPath(GetProjectRoot());
            string arguments = argumentsTemplate
                .Replace("$(ProjectPath)", projectRoot)
                .Replace("$(File)", fullFilePath)
                .Replace("$(Line)", line.ToString())
                .Replace("$(Column)", column.ToString());

            return TryStart(resolvedExecutable, arguments);
        }

        public static bool OpenFolder(string executablePath, string folderPath)
        {
            string resolvedExecutable = ResolveExecutable(executablePath);
            if (string.IsNullOrEmpty(resolvedExecutable) || string.IsNullOrWhiteSpace(folderPath))
            {
                return false;
            }

            string fullFolderPath;
            try
            {
                fullFolderPath = Path.GetFullPath(folderPath);
            }
            catch (Exception exception) when (IsPathException(exception))
            {
                UnityEngine.Debug.LogError(
                    $"[Antigravity] Invalid folder path '{folderPath}': {exception.Message}");
                return false;
            }

            string arguments = "\"" + ToQuotedFolderPath(fullFolderPath) + "\"";
            return TryStart(resolvedExecutable, arguments);
        }

        public static bool OpenFileInWorkspace(string executablePath, string projectRoot, string filePath)
        {
            string resolvedExecutable = ResolveExecutable(executablePath);
            if (string.IsNullOrEmpty(resolvedExecutable) || string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            string fullProjectRoot;
            string fullFilePath;
            try
            {
                fullProjectRoot = Path.GetFullPath(projectRoot);
                fullFilePath = Path.GetFullPath(filePath);
            }
            catch (Exception exception) when (IsPathException(exception))
            {
                UnityEngine.Debug.LogError(
                    $"[Antigravity] Invalid workspace path '{projectRoot}' or file path '{filePath}': {exception.Message}");
                return false;
            }

            string arguments = "\"" + ToQuotedFolderPath(fullProjectRoot) + "\" \"" + fullFilePath + "\"";
            return TryStart(resolvedExecutable, arguments);
        }

        private static string ResolveExecutable(string executablePath)
        {
            string resolved = AntigravityDiscovery.ResolveExecutablePath(executablePath);
            if (string.IsNullOrEmpty(resolved))
            {
                resolved = AntigravityDiscovery.FindBestExecutablePath();
            }

            return resolved;
        }

        private static bool TryStart(string executablePath, string arguments)
        {
            try
            {
                string fileName = executablePath;
                string finalArguments = arguments;
                if (executablePath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
                    executablePath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
                {
                    // Windows cannot start .cmd/.bat directly with UseShellExecute = false.
                    fileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
                    finalArguments = $"/d /s /c \"\"{executablePath}\" {arguments}\"";
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = finalArguments,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(startInfo))
                {
                    return process != null;
                }
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError(
                    $"[Antigravity] Failed to launch '{executablePath}' with arguments '{arguments}': {exception.Message}");
                return false;
            }
        }

        private static string ToQuotedFolderPath(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
            {
                return folderPath;
            }

            string trimmed = folderPath.TrimEnd('\\', '/');

            // A drive root such as "C:\" must keep its separator, doubled so the Windows
            // command-line parser does not read it as escaping the closing quote.
            if (trimmed.Length == 2 && char.IsLetter(trimmed[0]) && trimmed[1] == ':')
            {
                return trimmed + Path.DirectorySeparatorChar + Path.DirectorySeparatorChar;
            }

            return trimmed.Length == 0 ? folderPath : trimmed;
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
