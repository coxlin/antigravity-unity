using System.Text;

namespace Coxlin.Antigravity.Editor
{
    /// <summary>
    /// Generates the C# workspace configuration files (.vscode/settings.json, .editorconfig,
    /// omnisharp.json) at the Unity project root so that VS Code + OmniSharp behave well with
    /// Unity projects out of the box.
    /// </summary>
    public static class ConfigScaffolder
    {
        private const string VsCodeSettingsPath = ".vscode/settings.json";
        private const string EditorConfigPath = ".editorconfig";
        private const string OmniSharpPath = "omnisharp.json";

        private static readonly UTF8Encoding _utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        /// <summary>
        /// Generates or updates the three C# workspace config files at <paramref name="projectRoot"/>.
        /// When <paramref name="interactive"/> is true and any target file already exists, the user
        /// is asked whether to overwrite, cancel, or keep existing files (writing only missing ones).
        /// </summary>
        public static void GenerateConfigFiles(string projectRoot, string solutionFileName, bool interactive = true)
        {
            if (string.IsNullOrEmpty(projectRoot))
            {
                Debug.LogWarning("[Antigravity] Cannot generate C# config files: project root is empty.");
                return;
            }

            if (!Directory.Exists(projectRoot))
            {
                Debug.LogWarning($"[Antigravity] Cannot generate C# config files: '{projectRoot}' does not exist.");
                return;
            }

            string vscodeSettingsPath = Path.Combine(projectRoot, VsCodeSettingsPath.Replace('/', Path.DirectorySeparatorChar));
            string editorConfigPath = Path.Combine(projectRoot, EditorConfigPath);
            string omnisharpPath = Path.Combine(projectRoot, OmniSharpPath);

            bool vscodeExists = File.Exists(vscodeSettingsPath);
            bool editorConfigExists = File.Exists(editorConfigPath);
            bool omnisharpExists = File.Exists(omnisharpPath);
            bool anyExists = vscodeExists || editorConfigExists || omnisharpExists;

            bool overwriteAll = !anyExists || !interactive;
            bool keepExisting = false;

            if (anyExists && interactive)
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    "Generate C# Config Files",
                    "One or more C# configuration files (.vscode/settings.json, .editorconfig, omnisharp.json) already exist in the project root. How would you like to proceed?",
                    "Overwrite",
                    "Cancel",
                    "Merge / Keep Existing");

                switch (choice)
                {
                    case 0: // Overwrite
                        overwriteAll = true;
                        break;

                    case 1: // Cancel
                        Debug.Log("[Antigravity] C# config file generation cancelled by user.");
                        return;

                    default: // Merge / Keep Existing
                        overwriteAll = false;
                        keepExisting = true;
                        break;
                }
            }

            var created = new StringBuilder();
            var kept = new StringBuilder();
            var overwritten = new StringBuilder();

            Action<string, string, string, bool, bool, bool> writeIf =
                (label, path, contents, exists, overwrite, merge) =>
                {
                    if (exists)
                    {
                        if (overwrite)
                        {
                            WriteFile(path, contents);
                            Append(overwritten, label);
                        }
                        else if (merge)
                        {
                            // Keep the existing file untouched so user settings are preserved.
                            Append(kept, label);
                        }
                        else
                        {
                            WriteFile(path, contents);
                            Append(overwritten, label);
                        }
                    }
                    else
                    {
                        WriteFile(path, contents);
                        Append(created, label);
                    }
                };

            writeIf(VsCodeSettingsPath, vscodeSettingsPath, BuildVsCodeSettings(solutionFileName),
                vscodeExists, overwriteAll, keepExisting);
            writeIf(EditorConfigPath, editorConfigPath, BuildEditorConfig(),
                editorConfigExists, overwriteAll, keepExisting);
            writeIf(OmniSharpPath, omnisharpPath, BuildOmniSharpConfig(),
                omnisharpExists, overwriteAll, keepExisting);

            Debug.Log(FormatSummary(created, overwritten, kept));
        }

        private static string FormatSummary(StringBuilder created, StringBuilder overwritten, StringBuilder kept)
        {
            var summary = new StringBuilder("[Antigravity] C# config files:");
            bool any = false;

            if (created.Length > 0)
            {
                summary.Append(" created ").Append(created);
                any = true;
            }

            if (overwritten.Length > 0)
            {
                if (any)
                {
                    summary.Append(';');
                }

                summary.Append(" updated ").Append(overwritten);
                any = true;
            }

            if (kept.Length > 0)
            {
                if (any)
                {
                    summary.Append(';');
                }

                summary.Append(" kept existing ").Append(kept);
                any = true;
            }

            if (!any)
            {
                summary.Append(" no files changed.");
            }

            return summary.Append('.').ToString();
        }

        private static void Append(StringBuilder builder, string label)
        {
            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(label);
        }

        private static void WriteFile(string path, string contents)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, contents, _utf8NoBom);
        }

        private static string BuildVsCodeSettings(string solutionFileName)
        {
            string excluded = Join(
                "                \"**/*.meta\": true,",
                "                \"**/*.asset\": true,",
                "                \"**/*.prefab\": true,",
                "                \"**/*.unity\": true,",
                "                \"**/*.mat\": true,",
                "                \"Library/\": true,",
                "                \"Temp/\": true,",
                "                \"Obj/\": true,",
                "                \"obj/\": true,",
                "                \"Logs/\": true");

            return Join(
                "{",
                "    \"files.exclude\": {",
                excluded,
                "    },",
                "    \"search.exclude\": {",
                excluded,
                "    },",
                "    \"dotnet.defaultSolution\": \"" + EscapeJson(solutionFileName) + "\",",
                "    \"files.eol\": \"\\n\",",
                "    \"files.trimTrailingWhitespace\": true",
                "}");
        }

        private static string BuildEditorConfig()
        {
            return Join(
                "root = true",
                "",
                "[*]",
                "indent_style = space",
                "indent_size = 4",
                "charset = utf-8",
                "end_of_line = lf",
                "",
                "[*.cs]",
                "# Unity diagnostics that are noisy for generated/serialized code.",
                "dotnet_diagnostic.IDE0051.severity = none",
                "dotnet_diagnostic.IDE0044.severity = none",
                "",
                "# Unity serialized fields use the m_ prefix (camelCase).",
                "dotnet_naming_rule.unity_serialized_fields_m_prefix.severity = suggestion",
                "dotnet_naming_rule.unity_serialized_fields_m_prefix.symbols = unity_serialized_fields",
                "dotnet_naming_rule.unity_serialized_fields_m_prefix.style = m_underscore_camel_case",
                "dotnet_naming_symbols.unity_serialized_fields.applicable_kinds = field",
                "dotnet_naming_symbols.unity_serialized_fields.applicable_accessibilities = public",
                "dotnet_naming_style.m_underscore_camel_case.required_prefix = m_",
                "dotnet_naming_style.m_underscore_camel_case.capitalization = camel_case",
                "",
                "# Private/internal fields use the _ prefix (camelCase).",
                "dotnet_naming_rule.private_internal_fields_underscore_prefix.severity = suggestion",
                "dotnet_naming_rule.private_internal_fields_underscore_prefix.symbols = private_internal_fields",
                "dotnet_naming_rule.private_internal_fields_underscore_prefix.style = underscore_camel_case",
                "dotnet_naming_symbols.private_internal_fields.applicable_kinds = field",
                "dotnet_naming_symbols.private_internal_fields.applicable_accessibilities = private, internal",
                "dotnet_naming_style.underscore_camel_case.required_prefix = _",
                "dotnet_naming_style.underscore_camel_case.capitalization = camel_case",
                "");
        }

        private static string BuildOmniSharpConfig()
        {
            return Join(
                "{",
                "    \"RoslynExtensionsOptions\": {",
                "        \"EnableAnalyzersSupport\": true",
                "    },",
                "    \"FormattingOptions\": {",
                "        \"EnableEditorConfigSupport\": true",
                "    }",
                "}");
        }

        private static string Join(params string[] lines)
        {
            return string.Join("\n", lines);
        }

        /// <summary>
        /// Escapes a string for use inside a double-quoted JSON literal, without any
        /// third-party JSON library (Unity Editor does not guarantee Newtonsoft).
        /// </summary>
        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length + 8);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    default:
                        if (c < ' ')
                        {
                            builder.Append("\\u").Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(c);
                        }

                        break;
                }
            }

            return builder.ToString();
        }
    }
}
