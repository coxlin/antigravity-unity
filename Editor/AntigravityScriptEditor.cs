using System;
using Coxlin.Antigravity.Editor.ProjectGeneration;
using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;

namespace Coxlin.Antigravity.Editor
{
    [InitializeOnLoad]
    public class AntigravityScriptEditor : IExternalCodeEditor
    {
        private static readonly AntigravityScriptEditor _instance = new AntigravityScriptEditor();

        private IGenerator _projectGeneration;

        static AntigravityScriptEditor()
        {
            CodeEditor.Register(_instance);
        }

        public AntigravityScriptEditor()
        {
            _projectGeneration = new ProjectGeneration.ProjectGeneration();
        }

        public AntigravityScriptEditor(IGenerator projectGeneration)
        {
            _projectGeneration = projectGeneration ?? new ProjectGeneration.ProjectGeneration();
        }

        public CodeEditor.Installation[] Installations => AntigravityDiscovery.GetInstallations();

        public static void RegenerateProjectFiles()
        {
            _instance.SyncAll();
        }

        public bool TryGetInstallationForPath(string editorPath, out CodeEditor.Installation installation)
        {
            if (AntigravityDiscovery.IsAntigravityPath(editorPath))
            {
                string resolvedPath = AntigravityDiscovery.ResolveExecutablePath(editorPath);
                if (!string.IsNullOrEmpty(resolvedPath))
                {
                    AntigravityDiscovery.SavedCustomPath = resolvedPath;
                    installation = new CodeEditor.Installation
                    {
                        Name = AntigravityDiscovery.EditorName,
                        Path = resolvedPath
                    };
                    return true;
                }
            }

            installation = default;
            return false;
        }

        public void Initialize(string editorInstallationPath)
        {
            EnsureGenerator();
        }

        public void OnGUI()
        {
            EnsureGenerator();

            EditorGUILayout.LabelField("Generate .csproj files for:", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            SettingsFlagToggle(ProjectGenerationFlag.Embedded, "Embedded packages", "Generate .csproj files for embedded packages.");
            SettingsFlagToggle(ProjectGenerationFlag.Local, "Local packages", "Generate .csproj files for local packages.");
            SettingsFlagToggle(ProjectGenerationFlag.Registry, "Registry packages", "Generate .csproj files for registry packages.");
            SettingsFlagToggle(ProjectGenerationFlag.Git, "Git packages", "Generate .csproj files for Git packages.");
            SettingsFlagToggle(ProjectGenerationFlag.PlayerAssemblies, "Player projects", "Generate separate .csproj files for player assemblies.");
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Regenerate project files", GUILayout.Width(180f)))
                {
                    SyncAll();
                }
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Custom CLI Arguments", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                string currentArgs = AntigravityLauncher.CustomArguments;
                string updatedArgs = EditorGUILayout.TextField(currentArgs);
                if (!string.Equals(updatedArgs, currentArgs, StringComparison.Ordinal))
                {
                    AntigravityLauncher.CustomArguments = updatedArgs;
                }

                if (GUILayout.Button("Reset", GUILayout.Width(64f)))
                {
                    GUI.FocusControl(null);
                    AntigravityLauncher.ResetCustomArguments();
                }
            }

            EditorGUILayout.HelpBox(
                "Placeholders: $(ProjectPath), $(File), $(Line), $(Column)",
                MessageType.None);
        }

        public bool OpenProject(string filePath = "", int line = -1, int column = -1)
        {
            if (!string.IsNullOrEmpty(filePath) && !AntigravityLauncher.IsSupportedFile(filePath))
            {
                return false;
            }

            EnsureGenerator();
            if (!_projectGeneration.HasSolutionBeenGenerated())
            {
                SyncAll();
            }

            return AntigravityLauncher.OpenProject(CodeEditor.CurrentEditorInstallation, filePath, line, column);
        }

        public void SyncAll()
        {
            EnsureGenerator();
            _projectGeneration.Sync();
        }

        public void SyncIfNeeded(string[] addedFiles, string[] deletedFiles, string[] movedFiles, string[] movedFromFiles, string[] importedFiles)
        {
            EnsureGenerator();
            _projectGeneration.SyncIfNeeded(addedFiles, deletedFiles, movedFiles, movedFromFiles, importedFiles);
        }

        private void EnsureGenerator()
        {
            if (_projectGeneration == null)
            {
                _projectGeneration = new ProjectGeneration.ProjectGeneration();
            }
        }

        private void SettingsFlagToggle(ProjectGenerationFlag flag, string label, string tooltip)
        {
            ProjectGenerationFlag currentFlags = _projectGeneration.GenerationFlags;
            bool enabled = (currentFlags & flag) != 0;
            bool next = EditorGUILayout.Toggle(new GUIContent(label, tooltip), enabled);
            if (next != enabled)
            {
                _projectGeneration.GenerationFlags = next ? (currentFlags | flag) : (currentFlags & ~flag);
            }
        }
    }
}
